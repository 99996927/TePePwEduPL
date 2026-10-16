namespace Hr.Core.Domain;

/// <summary>Wpis dziennika audytu, tylko do dopisywania (ADR-13).</summary>
public class AuditLog
{
    public long Id { get; set; }

    public Guid? ActorUserId { get; set; }

    public DateTime AtUtc { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }
}
