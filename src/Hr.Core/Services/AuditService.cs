using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Services;

public sealed record AuditQuery(
    string? EntityType = null, string? EntityId = null, Guid? ActorUserId = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null, int Skip = 0, int Take = 50);

public sealed record AuditPage(int Total, IReadOnlyList<AuditLog> Items);

/// <summary>Przegląd dziennika audytu (UC-28): tylko kadry, od najnowszych wpisów, ze stronicowaniem (maksymalnie 200 na stronę).</summary>
public sealed class AuditService(HrDbContext db, ICurrentUser user)
{
    public const int MaxPageSize = 200;

    public async Task<ServiceResult<AuditPage>> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        if (!user.IsInRole(Roles.HR) && !user.IsInRole(Roles.HRApprover))
        {
            return ServiceResult<AuditPage>.Fail(ServiceErrorCode.Forbidden, "Dziennik audytu jest dostępny dla działu kadr.");
        }

        if (query.Skip < 0 || query.Take < 1)
        {
            return ServiceResult<AuditPage>.Fail(ServiceErrorCode.Validation, "Skip nie może być ujemny, a Take musi być dodatni.");
        }

        var entries = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            entries = entries.Where(a => a.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            entries = entries.Where(a => a.EntityId == query.EntityId);
        }

        if (query.ActorUserId is { } actor)
        {
            entries = entries.Where(a => a.ActorUserId == actor);
        }

        if (query.FromUtc is { } from)
        {
            entries = entries.Where(a => a.AtUtc >= from);
        }

        if (query.ToUtc is { } to)
        {
            entries = entries.Where(a => a.AtUtc <= to);
        }

        int total = await entries.CountAsync(ct);
        var items = await entries.OrderByDescending(a => a.Id).Skip(query.Skip).Take(Math.Min(query.Take, MaxPageSize)).ToListAsync(ct);
        return ServiceResult<AuditPage>.Ok(new AuditPage(total, items));
    }
}
