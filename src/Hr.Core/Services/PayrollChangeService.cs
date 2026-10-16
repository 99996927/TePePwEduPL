using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Services;

/// <summary>
/// Zmiany stawek i składników wynagrodzenia z zasadą czterech oczu (UC-18 do UC-20, ADR-08, ADR-15):
/// tworzy i wysyła <c>HR</c>, zatwierdza lub odrzuca <c>HRApprover</c>, nigdy autor.
/// </summary>
public sealed class PayrollChangeService(HrDbContext db, ICurrentUser user, IClock clock)
{
    public async Task<ServiceResult<PayrollChange>> CreateAsync(
        Guid employeeId, PayrollComponent component, decimal value, DateOnly effectiveFrom, string reason, CancellationToken ct = default)
    {
        if (!user.IsInRole(Roles.HR) || user.UserId is not { } userId)
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Forbidden, "Zmiany płacowe tworzy dział kadr.");
        }

        if (!await db.Employees.AnyAsync(e => e.Id == employeeId, ct))
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono pracownika.");
        }

        if (value < 0m)
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Validation, "Wartość nie może być ujemna.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Validation, "Zmiana płacowa wymaga uzasadnienia.");
        }

        var change = new PayrollChange
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            Component = component,
            Value = value,
            EffectiveFrom = effectiveFrom,
            Reason = reason.Trim(),
            Status = PayrollChangeStatus.Draft,
            CreatedByUserId = userId,
            CreatedAtUtc = clock.UtcNow,
        };
        db.PayrollChanges.Add(change);
        await db.SaveChangesAsync(ct);
        return ServiceResult<PayrollChange>.Ok(change);
    }

    public async Task<ServiceResult<PayrollChange>> SubmitAsync(Guid changeId, CancellationToken ct = default)
    {
        if (!user.IsInRole(Roles.HR))
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Forbidden, "Zmiany płacowe wysyła dział kadr.");
        }

        var change = await db.PayrollChanges.Visible(db, user).FirstOrDefaultAsync(c => c.Id == changeId, ct);
        if (change is null)
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono zmiany płacowej.");
        }

        if (change.CreatedByUserId != user.UserId)
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Forbidden, "Zmianę może wysłać tylko jej autor.");
        }

        if (change.Status != PayrollChangeStatus.Draft)
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Conflict, "Wysłać można tylko szkic.");
        }

        change.Status = PayrollChangeStatus.Pending;
        await db.SaveChangesAsync(ct);
        return ServiceResult<PayrollChange>.Ok(change);
    }

    public Task<ServiceResult<PayrollChange>> ApproveAsync(Guid changeId, CancellationToken ct = default) =>
        DecideAsync(changeId, approve: true, reason: null, ct);

    public Task<ServiceResult<PayrollChange>> RejectAsync(Guid changeId, string reason, CancellationToken ct = default) =>
        DecideAsync(changeId, approve: false, reason, ct);

    public async Task<IReadOnlyList<PayrollChange>> ListAsync(Guid? employeeId = null, CancellationToken ct = default)
    {
        var query = db.PayrollChanges.Visible(db, user);
        if (employeeId is { } id)
        {
            query = query.Where(c => c.EmployeeId == id);
        }

        return await query.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);
    }

    private async Task<ServiceResult<PayrollChange>> DecideAsync(Guid changeId, bool approve, string? reason, CancellationToken ct)
    {
        if (!user.IsInRole(Roles.HRApprover) || user.UserId is not { } userId)
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Forbidden, "Zmiany płacowe zatwierdza wyznaczona osoba z kadr.");
        }

        var change = await db.PayrollChanges.Visible(db, user).FirstOrDefaultAsync(c => c.Id == changeId, ct);
        if (change is null)
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono zmiany płacowej.");
        }

        if (change.CreatedByUserId == userId)
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Forbidden, "Autor nie może zatwierdzać ani odrzucać własnej zmiany.");
        }

        if (change.Status != PayrollChangeStatus.Pending)
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Conflict, "Decyzję można podjąć tylko dla zmiany oczekującej.");
        }

        if (!approve && string.IsNullOrWhiteSpace(reason))
        {
            return ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Validation, "Odrzucenie wymaga uzasadnienia.");
        }

        change.Status = approve ? PayrollChangeStatus.Approved : PayrollChangeStatus.Rejected;
        change.DecidedByUserId = userId;
        change.DecisionReason = approve || string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await db.SaveChangesAsync(ct);
        return ServiceResult<PayrollChange>.Ok(change);
    }
}
