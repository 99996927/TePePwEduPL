using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Security;
using Hr.Rules.Calendar;
using Hr.Rules.Leave;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Services;

public sealed record LeaveBalance(int EntitlementDays, int ApprovedDays, int PendingDays, int RemainingDays);

/// <summary>Saldo, wnioski i decyzje urlopowe (UC-05 do UC-09). Liczby pochodzą z <c>Hr.Rules</c>.</summary>
public sealed class LeaveService(HrDbContext db, ICurrentUser user, IClock clock, WorkCalendar calendar)
{
    public async Task<ServiceResult<LeaveBalance>> GetBalanceAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await db.VisibleEmployees(user).FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null)
        {
            return ServiceResult<LeaveBalance>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono pracownika.");
        }

        return ServiceResult<LeaveBalance>.Ok(await BalanceOfAsync(employee, Today.Year, ct));
    }

    /// <summary>Składa wniosek dla siebie (UC-06). Dni i saldo liczy silnik reguł (KP-154-1).</summary>
    public async Task<ServiceResult<LeaveRequest>> SubmitAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (user.EmployeeId is not { } employeeId || !Roles.All.Any(user.IsInRole))
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.Forbidden, "Użytkownik nie jest powiązany z pracownikiem.");
        }

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null)
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.Forbidden, "Użytkownik nie jest powiązany z pracownikiem.");
        }

        if (to < from)
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.Validation, "Data końcowa nie może być wcześniejsza niż początkowa.");
        }

        if (from.Year != to.Year)
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.Validation, "Wniosek musi mieścić się w jednym roku kalendarzowym.");
        }

        await using var transaction = await EmployeeLock.BeginAsync(db, "leave", employeeId, ct);
        bool overlaps = await db.LeaveRequests.AnyAsync(
            l => l.EmployeeId == employeeId && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved)
                 && l.From <= to && l.To >= from, ct);
        if (overlaps)
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.Conflict, "Wniosek nakłada się na istniejący wniosek urlopowy.");
        }

        var balance = await BalanceOfAsync(employee, from.Year, ct);
        var check = LeaveRequestRules.Evaluate(balance.EntitlementDays, balance.ApprovedDays + balance.PendingDays, from, to, calendar);
        if (check.Days == 0)
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.Validation, "Wniosek nie obejmuje żadnego dnia roboczego.");
        }

        if (!check.Sufficient)
        {
            return ServiceResult<LeaveRequest>.Fail(
                ServiceErrorCode.Validation,
                $"Niewystarczające saldo urlopu: brakuje {-check.RemainingAfter} dni.",
                check.RuleId);
        }

        var request = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            From = from,
            To = to,
            Days = check.Days,
            Status = LeaveStatus.Pending,
            CreatedAtUtc = clock.UtcNow,
        };
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<LeaveRequest>.Ok(request);
    }

    /// <summary>Anuluje własny wniosek w statusie oczekującym (UC-09).</summary>
    public async Task<ServiceResult<LeaveRequest>> CancelAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = user.EmployeeId is { } me
            ? await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == requestId && l.EmployeeId == me, ct)
            : null;
        if (request is null)
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono wniosku.");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.Conflict, "Anulować można tylko wniosek oczekujący na decyzję.");
        }

        request.Status = LeaveStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return ServiceResult<LeaveRequest>.Ok(request);
    }

    /// <summary>Zatwierdza lub odrzuca wniosek (UC-08): kierownik zespołu albo kadry, nigdy autor wniosku.</summary>
    public async Task<ServiceResult<LeaveRequest>> DecideAsync(Guid requestId, bool approve, string? reason, CancellationToken ct = default)
    {
        var request = await db.LeaveRequests.Visible(db, user).FirstOrDefaultAsync(l => l.Id == requestId, ct);
        if (request is null)
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono wniosku.");
        }

        bool canDecide = user.IsInRole(Roles.Manager) || user.IsInRole(Roles.HR) || user.IsInRole(Roles.HRApprover);
        if (!canDecide || request.EmployeeId == user.EmployeeId)
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.Forbidden, "Brak uprawnień do podjęcia decyzji w tym wniosku.");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.Conflict, "Wniosek został już rozpatrzony lub anulowany.");
        }

        if (!approve && string.IsNullOrWhiteSpace(reason))
        {
            return ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.Validation, "Odrzucenie wniosku wymaga uzasadnienia.");
        }

        if (approve)
        {
            var employee = await db.Employees.FirstAsync(e => e.Id == request.EmployeeId, ct);
            var balance = await BalanceOfAsync(employee, request.From.Year, ct);
            int usedByOthers = balance.ApprovedDays + balance.PendingDays - request.Days;
            var check = LeaveRequestRules.Evaluate(balance.EntitlementDays, usedByOthers, request.From, request.To, calendar);
            if (!check.Sufficient)
            {
                return ServiceResult<LeaveRequest>.Fail(
                    ServiceErrorCode.Validation,
                    $"Niewystarczające saldo urlopu: brakuje {-check.RemainingAfter} dni.",
                    check.RuleId);
            }
        }

        request.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        request.DecidedByEmployeeId = user.EmployeeId;
        request.DecisionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await db.SaveChangesAsync(ct);
        return ServiceResult<LeaveRequest>.Ok(request);
    }

    /// <summary>Widoczne wnioski, od najnowszych.</summary>
    public async Task<IReadOnlyList<LeaveRequest>> ListAsync(CancellationToken ct = default) =>
        await db.LeaveRequests.Visible(db, user).OrderByDescending(l => l.From).ToListAsync(ct);

    private DateOnly Today => DateOnly.FromDateTime(clock.LocalNow);

    private async Task<LeaveBalance> BalanceOfAsync(Employee employee, int year, CancellationToken ct)
    {
        var today = Today;
        // Staż liczony na dziś dla bieżącego roku, na 1 stycznia dla pozostałych lat.
        var asOf = year == today.Year ? today : new DateOnly(year, 1, 1);
        int entitlement = LeaveEntitlement.AnnualDays(LeaveEntitlement.YearsOfService(employee.HireDate, asOf));
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var days = await db.LeaveRequests
            .Where(l => l.EmployeeId == employee.Id && l.From >= yearStart && l.From <= yearEnd
                        && (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending))
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Days = g.Sum(l => l.Days) })
            .ToListAsync(ct);

        int approved = days.Where(d => d.Status == LeaveStatus.Approved).Sum(d => d.Days);
        int pending = days.Where(d => d.Status == LeaveStatus.Pending).Sum(d => d.Days);
        return new LeaveBalance(entitlement, approved, pending, entitlement - approved - pending);
    }
}
