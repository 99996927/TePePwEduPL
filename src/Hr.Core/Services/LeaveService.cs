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
    internal WorkCalendar Calendar => calendar;

    public async Task<ServiceResult<LeaveBalance>> GetBalanceAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await db.VisibleEmployees(user).FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null)
        {
            return ServiceResult<LeaveBalance>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono pracownika.");
        }

        return ServiceResult<LeaveBalance>.Ok(await BalanceOfAsync(employee, ct));
    }

    private async Task<LeaveBalance> BalanceOfAsync(Employee employee, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        int entitlement = LeaveEntitlement.AnnualDays(LeaveEntitlement.YearsOfService(employee.HireDate, today));
        var yearStart = new DateOnly(today.Year, 1, 1);
        var yearEnd = new DateOnly(today.Year, 12, 31);

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
