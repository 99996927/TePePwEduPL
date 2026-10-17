using System.Text.Json;
using Hr.Core.Domain;
using Hr.Core.Seeding.Demo;
using Hr.Rules.Calendar;
using Hr.Rules.Leave;

namespace Hr.Core.Tests.Seeding;

public class DemoLeaveTests
{
    private static readonly DemoOptions Options = DemoOptions.Default;
    private static readonly WorkCalendar Calendar = new();
    private static readonly IReadOnlyList<DemoPerson> People = DemoPeople.Build(Options).Item2;

    private static DemoPerson Person(string login) => People.Single(p => p.Login == login);

    private static IReadOnlyList<LeaveRequest> Build() => DemoLeave.Build(Options, People, Calendar);

    [Fact]
    public void Scenario_dates_for_default_anchor()
    {
        var a = Options.AnchorDate;

        Assert.Equal(new DateOnly(2026, 9, 29), DemoScenarios.AnnaDailyRestTuesday(a));
        Assert.Equal(DayOfWeek.Tuesday, DemoScenarios.AnnaDailyRestTuesday(a).DayOfWeek);
        Assert.Equal(new DateOnly(2026, 9, 12), DemoScenarios.AnnaWeeklyRestSaturday(a));
        Assert.Equal(DayOfWeek.Saturday, DemoScenarios.AnnaWeeklyRestSaturday(a).DayOfWeek);
        Assert.Equal(new DateOnly(2026, 6, 1), DemoScenarios.JanOvertimePeriod(a).From);
        Assert.Equal(new DateOnly(2026, 9, 30), DemoScenarios.JanOvertimePeriod(a).To);
        Assert.Equal((new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 17)), DemoScenarios.AnnaLeaveRealized(a));
        Assert.Equal((new DateOnly(2026, 12, 7), new DateOnly(2026, 12, 11)), DemoScenarios.AnnaLeaveFuture(a));
        Assert.Equal((new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 6)), DemoScenarios.AnnaLeavePending(a));
        Assert.Equal((new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 7)), DemoScenarios.JanLeaveRejected(a));
        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9)), DemoScenarios.JanLeaveCancelled(a));
    }

    [Fact]
    public void Anna_has_exactly_realized_future_and_pending_leave()
    {
        var anna = Person("anna.nowak");
        var ewa = Person("ewa.wisniewska");
        var leaves = Build().Where(l => l.EmployeeId == anna.EmployeeId).OrderBy(l => l.From).ToList();

        Assert.Equal(3, leaves.Count);
        Assert.Equal(new[] { LeaveStatus.Approved, LeaveStatus.Pending, LeaveStatus.Approved }, leaves.Select(l => l.Status));
        Assert.Equal(DemoScenarios.AnnaLeaveRealized(Options.AnchorDate), (leaves[0].From, leaves[0].To));
        Assert.Equal(DemoScenarios.AnnaLeavePending(Options.AnchorDate), (leaves[1].From, leaves[1].To));
        Assert.Equal(DemoScenarios.AnnaLeaveFuture(Options.AnchorDate), (leaves[2].From, leaves[2].To));
        Assert.All(leaves, l => Assert.Equal(5, l.Days));
        Assert.All(leaves.Where(l => l.Status == LeaveStatus.Approved), l => Assert.Equal(ewa.EmployeeId, l.DecidedByEmployeeId));
        Assert.Null(leaves[1].DecidedByEmployeeId);

        var entitlement = LeaveEntitlement.AnnualDays(LeaveEntitlement.YearsOfService(anna.HireDate, Options.AnchorDate));
        var used = leaves.Where(l => l.Status is LeaveStatus.Approved or LeaveStatus.Pending && l.From.Year == Options.AnchorDate.Year).Sum(l => l.Days);
        Assert.Equal(5, entitlement - used);
    }

    [Fact]
    public void Jan_has_exactly_rejected_with_reason_and_cancelled()
    {
        var jan = Person("jan.kowalski");
        var ewa = Person("ewa.wisniewska");
        var leaves = Build().Where(l => l.EmployeeId == jan.EmployeeId).OrderBy(l => l.From).ToList();

        Assert.Equal(2, leaves.Count);
        var rejected = leaves[0];
        var cancelled = leaves[1];
        Assert.Equal(LeaveStatus.Rejected, rejected.Status);
        Assert.Equal(DemoScenarios.JanLeaveRejected(Options.AnchorDate), (rejected.From, rejected.To));
        Assert.Equal(DemoScenarios.RejectionReason, rejected.DecisionReason);
        Assert.Equal(ewa.EmployeeId, rejected.DecidedByEmployeeId);
        Assert.Equal(LeaveStatus.Cancelled, cancelled.Status);
        Assert.Equal(DemoScenarios.JanLeaveCancelled(Options.AnchorDate), (cancelled.From, cancelled.To));
        Assert.All(leaves, l => Assert.Equal(Calendar.CountWorkingDays(l.From, l.To), l.Days));
    }

    [Fact]
    public void Other_people_have_one_or_two_past_approved_leaves_within_entitlement_and_without_overlap()
    {
        var all = Build();
        var others = People.Where(p => p.Login is not ("anna.nowak" or "jan.kowalski"));

        foreach (var p in others)
        {
            var leaves = all.Where(l => l.EmployeeId == p.EmployeeId).OrderBy(l => l.From).ToList();
            Assert.InRange(leaves.Count, 1, 2);
            var expectedDecider = p.Role == "Employee" && p.TeamName != DemoPeople.TeamHr
                ? People.Single(x => x.Role == "Manager" && x.TeamName == p.TeamName).EmployeeId
                : Person("piotr.zielinski").EmployeeId;
            Assert.All(leaves, l => Assert.Equal(expectedDecider, l.DecidedByEmployeeId));
            Assert.All(leaves, l =>
            {
                Assert.Equal(LeaveStatus.Approved, l.Status);
                Assert.True(l.To < Options.AnchorDate);
                Assert.InRange(l.Days, 3, 10);
            });
            if (leaves.Count == 2)
            {
                Assert.True(leaves[0].To < leaves[1].From);
            }
        }
    }

    [Fact]
    public void Days_match_working_calendar_for_every_request()
    {
        Assert.All(Build(), l =>
        {
            Assert.True(l.Days > 0);
            Assert.Equal(Calendar.CountWorkingDays(l.From, l.To), l.Days);
        });
    }

    [Fact]
    public void Active_leaves_stay_within_one_year_and_entitlement()
    {
        var all = Build();

        foreach (var p in People)
        {
            var active = all.Where(l => l.EmployeeId == p.EmployeeId && l.Status is LeaveStatus.Approved or LeaveStatus.Pending).ToList();
            Assert.All(active, l => Assert.Equal(l.From.Year, l.To.Year));
            var entitlement = LeaveEntitlement.AnnualDays(LeaveEntitlement.YearsOfService(p.HireDate, Options.AnchorDate));
            foreach (var year in active.GroupBy(l => l.From.Year))
            {
                Assert.True(year.Sum(l => l.Days) <= entitlement, $"{p.Login} {year.Key}");
            }
        }
    }

    [Fact]
    public void Leave_ids_and_timestamps_are_deterministic()
    {
        var first = JsonSerializer.Serialize(Build());
        var second = JsonSerializer.Serialize(Build());

        Assert.NotEqual("[]", first);
        Assert.Equal(first, second);
        Assert.Equal(Build().Count, Build().Select(l => l.Id).Distinct().Count());
    }
}
