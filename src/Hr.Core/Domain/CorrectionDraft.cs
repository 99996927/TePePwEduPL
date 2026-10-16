using Hr.Core.Auditing;
using Hr.Core.Security;

namespace Hr.Core.Domain;

public enum CorrectionDraftStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>Szkic korekty wpisu czasu pracy przygotowany dla kadr (UC-26); wpis zmienia dopiero zatwierdzenie (UC-11).</summary>
public class CorrectionDraft : IEmployeeOwned, IAuditable
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid WorkEntryId { get; set; }

    public DateTime ProposedStart { get; set; }

    public DateTime ProposedEnd { get; set; }

    public TimeSpan ProposedBreak { get; set; }

    public string Reason { get; set; } = string.Empty;

    public CorrectionDraftStatus Status { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? ConversationId { get; set; }

    public Guid? DecidedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    public string? DecisionNote { get; set; }
}
