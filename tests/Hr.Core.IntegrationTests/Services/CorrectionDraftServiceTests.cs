using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class CorrectionDraftServiceTests(MsSqlFixture fixture)
{
    private static DateTime T(int day, int hour) => new(2026, 10, day, hour, 0, 0);

    private static Task<WorkEntryRecord> EntryOf(ServiceTestContext c, Guid employeeId) =>
        c.Db.WorkEntries.AsNoTracking().FirstAsync(w => w.EmployeeId == employeeId);

    [Fact]
    public async Task UC26_HR_creates_pending_draft_without_changing_the_entry()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var entry = await EntryOf(c, c.Org.Anna);
        var conversationId = Guid.NewGuid();

        var result = await c.Drafts(c.As(c.Org.Piotr, Roles.HR)).CreateAsync(entry.Id, T(5, 9), T(5, 17), TimeSpan.FromMinutes(30), "Brak wyjścia na przerwę", conversationId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(CorrectionDraftStatus.Pending, result.Value!.Status);
        Assert.Equal(c.Org.Anna, result.Value.EmployeeId);
        Assert.Equal(conversationId, result.Value.ConversationId);
        var unchanged = await EntryOf(c, c.Org.Anna);
        Assert.Equal(entry.Start, unchanged.Start);
        Assert.Equal(entry.End, unchanged.End);
        Assert.True(await c.Db.AuditLogs.AnyAsync(a => a.EntityType == nameof(CorrectionDraft) && a.EventType == "Added"));
    }

    [Fact]
    public async Task UC26_Employee_and_Manager_cannot_create_draft()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var entry = await EntryOf(c, c.Org.Anna);

        var asAnna = await c.Drafts(c.As(c.Org.Anna, Roles.Employee)).CreateAsync(entry.Id, T(5, 9), T(5, 17), TimeSpan.Zero, "Korekta", null);
        var asEwa = await c.Drafts(c.As(c.Org.Ewa, Roles.Manager)).CreateAsync(entry.Id, T(5, 9), T(5, 17), TimeSpan.Zero, "Korekta", null);

        Assert.Equal(ServiceErrorCode.Forbidden, asAnna.Error!.Code);
        Assert.Equal(ServiceErrorCode.Forbidden, asEwa.Error!.Code);
        Assert.Equal(0, await c.Db.CorrectionDrafts.CountAsync());
    }

    [Fact]
    public async Task UC26_Draft_for_entry_outside_scope_is_NotFound()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Drafts(c.As(c.Org.Piotr, Roles.HR)).CreateAsync(Guid.NewGuid(), T(5, 9), T(5, 17), TimeSpan.Zero, "Korekta", null);

        Assert.Equal(ServiceErrorCode.NotFound, result.Error!.Code);
    }

    [Fact]
    public async Task UC26_Draft_with_invalid_period_is_Validation()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var entry = await EntryOf(c, c.Org.Anna);

        var result = await c.Drafts(c.As(c.Org.Piotr, Roles.HR)).CreateAsync(entry.Id, T(5, 17), T(5, 9), TimeSpan.Zero, "Korekta", null);

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
        Assert.Equal(0, await c.Db.CorrectionDrafts.CountAsync());
    }

    [Fact]
    public async Task UC26_Blank_reason_is_Validation()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var entry = await EntryOf(c, c.Org.Anna);

        var result = await c.Drafts(c.As(c.Org.Piotr, Roles.HR)).CreateAsync(entry.Id, T(5, 9), T(5, 17), TimeSpan.Zero, "  ", null);

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
    }

    private static async Task<CorrectionDraft> DraftAsync(ServiceTestContext c, Guid employeeId, int endHour = 17)
    {
        var entry = await EntryOf(c, employeeId);
        var created = await c.Drafts(c.As(c.Org.Piotr, Roles.HR)).CreateAsync(entry.Id, T(5, 9), T(5, endHour), TimeSpan.FromMinutes(30), "Korekta godzin", null);
        Assert.True(created.IsSuccess, created.Error?.Message);
        return created.Value!;
    }

    [Fact]
    public async Task UC11_HR_approves_draft_applies_correction_with_reason_and_marks_approved()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var draft = await DraftAsync(c, c.Org.Anna);

        var result = await c.Drafts(c.As(c.Org.Maria, Roles.HR)).DecideAsync(draft.Id, approve: true, null);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(CorrectionDraftStatus.Approved, result.Value!.Status);
        Assert.Equal(c.UserIdOf(c.Org.Maria), result.Value.DecidedByUserId);
        var entry = await EntryOf(c, c.Org.Anna);
        Assert.Equal(T(5, 9), entry.Start);
        Assert.Equal(T(5, 17), entry.End);
        Assert.Equal(TimeSpan.FromMinutes(30), entry.Break);
        Assert.True(await c.Db.AuditLogs.AnyAsync(a => a.EventType == "WorkEntryCorrected" && a.AfterJson!.Contains("Korekta godzin")));
    }

    [Fact]
    public async Task UC11_Reject_marks_rejected_and_leaves_entry_untouched()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var draft = await DraftAsync(c, c.Org.Anna);
        var before = await EntryOf(c, c.Org.Anna);

        var result = await c.Drafts(c.As(c.Org.Maria, Roles.HR)).DecideAsync(draft.Id, approve: false, "Nie potwierdzono");

        Assert.Equal(CorrectionDraftStatus.Rejected, result.Value!.Status);
        Assert.Equal("Nie potwierdzono", result.Value.DecisionNote);
        var after = await EntryOf(c, c.Org.Anna);
        Assert.Equal(before.Start, after.Start);
        Assert.Equal(before.End, after.End);
    }

    [Fact]
    public async Task UC11_Deciding_an_already_decided_draft_is_Conflict()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var draft = await DraftAsync(c, c.Org.Anna);
        var hr = c.Drafts(c.As(c.Org.Maria, Roles.HR));
        Assert.True((await hr.DecideAsync(draft.Id, approve: false, "Nie")).IsSuccess);

        var again = await hr.DecideAsync(draft.Id, approve: true, null);

        Assert.Equal(ServiceErrorCode.Conflict, again.Error!.Code);
    }

    [Fact]
    public async Task UC11_Approval_failing_validation_keeps_draft_pending()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var entry = await EntryOf(c, c.Org.Anna);
        // Szkic z końcem 16.10 15:00 jest poprawny o 16:00 czasu lokalnego, ale zatwierdzony o 14:00 kończyłby się w przyszłości.
        c.Clock.UtcNow = new DateTime(2026, 10, 16, 14, 0, 0, DateTimeKind.Utc);
        var draft = await c.Drafts(c.As(c.Org.Piotr, Roles.HR)).CreateAsync(entry.Id, T(16, 9), T(16, 15), TimeSpan.Zero, "Za późno", null);
        Assert.True(draft.IsSuccess, draft.Error?.Message);
        c.Clock.UtcNow = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        var result = await c.Drafts(c.As(c.Org.Maria, Roles.HR)).DecideAsync(draft.Value!.Id, approve: true, null);

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
        Assert.Equal(CorrectionDraftStatus.Pending, (await c.Db.CorrectionDrafts.AsNoTracking().SingleAsync(d => d.Id == draft.Value.Id)).Status);
    }

    [Fact]
    public async Task UC26_List_is_HR_only_and_filters_by_status()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var first = await DraftAsync(c, c.Org.Anna);
        await DraftAsync(c, c.Org.Jan);
        Assert.True((await c.Drafts(c.As(c.Org.Maria, Roles.HR)).DecideAsync(first.Id, approve: false, "Nie")).IsSuccess);

        var asEmployee = await c.Drafts(c.As(c.Org.Anna, Roles.Employee)).ListAsync(null);
        var pending = await c.Drafts(c.As(c.Org.Piotr, Roles.HR)).ListAsync(CorrectionDraftStatus.Pending);
        var all = await c.Drafts(c.As(c.Org.Maria, Roles.HRApprover)).ListAsync(null);

        Assert.Equal(ServiceErrorCode.Forbidden, asEmployee.Error!.Code);
        Assert.Single(pending.Value!);
        Assert.Equal(c.Org.Jan, pending.Value![0].EmployeeId);
        Assert.Equal(2, all.Value!.Count);
    }
}
