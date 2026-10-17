using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Seeding.Demo;
using Hr.Rules.Calendar;

namespace Hr.Core.Tests.Seeding;

public class DemoCorrectionDraftsTests
{
    private static readonly DemoOptions Options = DemoOptions.Default;
    private static readonly WorkCalendar Calendar = new();
    private static readonly IReadOnlyList<DemoPerson> People = DemoPeople.Build(Options).Item2;
    private static readonly IReadOnlyList<WorkEntryRecord> Entries =
        DemoTimesheets.Build(Options, People, DemoLeave.Build(Options, People, Calendar), Calendar);

    private static DemoPerson Person(string login) => People.Single(p => p.Login == login);

    private static IReadOnlyList<CorrectionDraft> Build() => DemoCorrectionDrafts.Build(Options, People, Entries);

    [Fact]
    public void UC26_Single_pending_draft_moves_Annas_daily_rest_violation_shift()
    {
        var draft = Assert.Single(Build());
        var anna = Person("anna.nowak");
        var entry = Entries.Single(e => e.Id == draft.WorkEntryId);

        Assert.Equal(anna.EmployeeId, draft.EmployeeId);
        Assert.Equal(anna.EmployeeId, entry.EmployeeId);
        Assert.Equal(new DateOnly(2026, 9, 29), DateOnly.FromDateTime(entry.Start));
        Assert.Equal(new TimeSpan(14, 0, 0), entry.Start.TimeOfDay);
        Assert.Equal(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Unspecified), draft.ProposedStart);
        Assert.Equal(new DateTime(2026, 9, 29, 20, 30, 0, DateTimeKind.Unspecified), draft.ProposedEnd);
        Assert.Equal(DateTimeKind.Unspecified, draft.ProposedStart.Kind);
        Assert.Equal(DateTimeKind.Unspecified, draft.ProposedEnd.Kind);
        Assert.Equal(TimeSpan.FromMinutes(30), draft.ProposedBreak);
        Assert.Contains("art. 132 § 1", draft.Reason);
        Assert.Equal(CorrectionDraftStatus.Pending, draft.Status);
        Assert.Equal(Person("piotr.zielinski").UserId, draft.CreatedByUserId);
        Assert.Null(draft.ConversationId);
        Assert.Null(draft.DecidedByUserId);
        Assert.Equal(PolishTime.ToUtc(new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Unspecified)), draft.CreatedAtUtc);
        Assert.Equal(DemoIds.For(Options.Seed, "draft:anna:1"), draft.Id);
    }

    [Fact]
    public void Ids_are_deterministic_between_builds()
    {
        Assert.Equal(Build().Single().Id, Build().Single().Id);
    }

    [Fact]
    public void Returns_nothing_when_Annas_entry_is_missing()
    {
        var annaId = Person("anna.nowak").EmployeeId;
        var without = Entries.Where(e => e.EmployeeId != annaId).ToList();

        Assert.Empty(DemoCorrectionDrafts.Build(Options, People, without));
    }
}
