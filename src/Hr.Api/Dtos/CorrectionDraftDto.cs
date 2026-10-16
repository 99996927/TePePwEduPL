using Hr.Core.Domain;

namespace Hr.Api.Dtos;

/// <summary>Czasy propozycji to czas lokalny bez strefy (ADR-17); znaczniki utworzenia i decyzji są w UTC.</summary>
public sealed record CorrectionDraftDto(
    Guid Id,
    Guid EmployeeId,
    Guid WorkEntryId,
    DateTime ProposedStart,
    DateTime ProposedEnd,
    int ProposedBreakMinutes,
    string Reason,
    string Status,
    Guid CreatedByUserId,
    Guid? ConversationId,
    Guid? DecidedByUserId,
    DateTime CreatedAtUtc,
    DateTime? DecidedAtUtc,
    string? DecisionNote)
{
    public static CorrectionDraftDto Of(CorrectionDraft d) => new(
        d.Id, d.EmployeeId, d.WorkEntryId, d.ProposedStart, d.ProposedEnd, (int)d.ProposedBreak.TotalMinutes, d.Reason,
        d.Status.ToString(), d.CreatedByUserId, d.ConversationId, d.DecidedByUserId, d.CreatedAtUtc.AsUtc(), d.DecidedAtUtc?.AsUtc(), d.DecisionNote);
}

public sealed record CreateCorrectionDraftRequest(Guid WorkEntryId, DateTime Start, DateTime End, int BreakMinutes, string Reason);

public sealed record DraftDecisionRequest(bool Approve, string? Note);
