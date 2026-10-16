using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;

namespace Hr.Core.Auditing;

public sealed class AuditWriter(HrDbContext db, ICurrentUser actor, IClock clock) : IAuditWriter
{
    public void Record(string eventType, string entityType, string entityId, string? afterJson = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            AtUtc = clock.UtcNow,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            AfterJson = afterJson,
        });
}
