using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Security;
using Hr.Rules.Time;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Services;

/// <summary>
/// Szkice korekt wpisów czasu pracy (UC-26, UC-11): tworzy i przegląda wyłącznie kadra (<c>HR</c>, <c>HRApprover</c>);
/// wpis zmienia dopiero zatwierdzenie, przez <see cref="TimesheetService.CorrectEntryAsync"/>.
/// </summary>
public sealed class CorrectionDraftService(HrDbContext db, ICurrentUser user, IClock clock, TimesheetService timesheet)
{
    private bool IsHrStaff => user.IsInRole(Roles.HR) || user.IsInRole(Roles.HRApprover);

    public async Task<ServiceResult<CorrectionDraft>> CreateAsync(
        Guid workEntryId, DateTime start, DateTime end, TimeSpan breakTime, string reason, Guid? conversationId, CancellationToken ct = default)
    {
        if (!IsHrStaff || user.UserId is not { } userId)
        {
            return ServiceResult<CorrectionDraft>.Fail(ServiceErrorCode.Forbidden, "Szkice korekt przygotowuje dział kadr.");
        }

        var entry = await db.WorkEntries.Visible(db, user).FirstOrDefaultAsync(w => w.Id == workEntryId, ct);
        if (entry is null)
        {
            return ServiceResult<CorrectionDraft>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono wpisu.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return ServiceResult<CorrectionDraft>.Fail(ServiceErrorCode.Validation, "Szkic korekty wymaga uzasadnienia.");
        }

        try
        {
            _ = new WorkEntry(start, end, breakTime);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<CorrectionDraft>.Fail(ServiceErrorCode.Validation, ex.Message);
        }

        if (end > clock.LocalNow)
        {
            return ServiceResult<CorrectionDraft>.Fail(ServiceErrorCode.Validation, "Wpis nie może kończyć się w przyszłości.");
        }

        var draft = new CorrectionDraft
        {
            Id = Guid.NewGuid(),
            EmployeeId = entry.EmployeeId,
            WorkEntryId = entry.Id,
            ProposedStart = start,
            ProposedEnd = end,
            ProposedBreak = breakTime,
            Reason = reason.Trim(),
            Status = CorrectionDraftStatus.Pending,
            CreatedByUserId = userId,
            ConversationId = conversationId,
            CreatedAtUtc = clock.UtcNow,
        };
        db.CorrectionDrafts.Add(draft);
        await db.SaveChangesAsync(ct);
        return ServiceResult<CorrectionDraft>.Ok(draft);
    }

    public async Task<ServiceResult<IReadOnlyList<CorrectionDraft>>> ListAsync(CorrectionDraftStatus? status, CancellationToken ct = default)
    {
        if (!IsHrStaff)
        {
            return ServiceResult<IReadOnlyList<CorrectionDraft>>.Fail(ServiceErrorCode.Forbidden, "Szkice korekt przegląda dział kadr.");
        }

        var query = db.CorrectionDrafts.Visible(db, user);
        if (status is { } s)
        {
            query = query.Where(d => d.Status == s);
        }

        IReadOnlyList<CorrectionDraft> drafts = await query.OrderByDescending(d => d.CreatedAtUtc).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<CorrectionDraft>>.Ok(drafts);
    }

    public async Task<ServiceResult<CorrectionDraft>> DecideAsync(Guid draftId, bool approve, string? note, CancellationToken ct = default)
    {
        if (!IsHrStaff || user.UserId is not { } userId)
        {
            return ServiceResult<CorrectionDraft>.Fail(ServiceErrorCode.Forbidden, "Szkice korekt rozstrzyga dział kadr.");
        }

        var draft = await db.CorrectionDrafts.Visible(db, user).FirstOrDefaultAsync(d => d.Id == draftId, ct);
        if (draft is null)
        {
            return ServiceResult<CorrectionDraft>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono szkicu korekty.");
        }

        if (draft.Status != CorrectionDraftStatus.Pending)
        {
            return ServiceResult<CorrectionDraft>.Fail(ServiceErrorCode.Conflict, "Decyzję można podjąć tylko dla szkicu oczekującego.");
        }

        if (approve)
        {
            var applied = await timesheet.CorrectEntryAsync(
                draft.WorkEntryId, draft.ProposedStart, draft.ProposedEnd, draft.ProposedBreak, draft.Reason, ct);
            if (!applied.IsSuccess)
            {
                return ServiceResult<CorrectionDraft>.Fail(applied.Error!.Code, applied.Error.Message, applied.Error.RuleId);
            }
        }

        draft.Status = approve ? CorrectionDraftStatus.Approved : CorrectionDraftStatus.Rejected;
        draft.DecidedByUserId = userId;
        draft.DecidedAtUtc = clock.UtcNow;
        draft.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await db.SaveChangesAsync(ct);
        return ServiceResult<CorrectionDraft>.Ok(draft);
    }
}
