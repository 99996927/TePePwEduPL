using System.Text.Json;
using Hr.Core.Domain;
using Hr.Core.Seeding.Demo;
using Hr.Rules.Calendar;
using Hr.Rules.Overtime;
using Hr.Rules.Rest;
using Hr.Rules.Time;

namespace Hr.Core.Tests.Seeding;

public class DemoTimesheetsTests
{
    private static readonly DemoOptions Options = DemoOptions.Default;
    private static readonly WorkCalendar Calendar = new();
    private static readonly IReadOnlyList<DemoPerson> People = DemoPeople.Build(Options).Item2;
    private static readonly IReadOnlyList<LeaveRequest> Leave = DemoLeave.Build(Options, People, Calendar);
    private static readonly IReadOnlyList<WorkEntryRecord> Entries = Build();

    private static IReadOnlyList<WorkEntryRecord> Build() => DemoTimesheets.Build(Options, People, Leave, Calendar);

    private static DemoPerson Person(string login) => People.Single(p => p.Login == login);

    private static IReadOnlyList<WorkEntry> Of(DemoPerson p) =>
        Entries.Where(e => e.EmployeeId == p.EmployeeId).OrderBy(e => e.Start).Select(e => e.ToRulesEntry()).ToList();

    private static decimal Overtime(DemoPerson p, DateOnly from, DateOnly to) =>
        OvertimeCalculator.Calculate(
            Of(p).Where(e => DateOnly.FromDateTime(e.Start) >= from && DateOnly.FromDateTime(e.Start) <= to).ToList(),
            Calendar, NightWindow.Default).Total;

    private static bool IsSpecial(DemoPerson p) => p.Login is "anna.nowak" or "jan.kowalski";

    [Fact]
    public void Entries_fall_only_on_working_days_inside_window_except_annas_saturday()
    {
        var saturday = DemoScenarios.AnnaWeeklyRestSaturday(Options.AnchorDate);
        Assert.NotEmpty(Entries);

        foreach (var p in People)
        {
            var approved = Leave.Where(l => l.EmployeeId == p.EmployeeId && l.Status == LeaveStatus.Approved).ToList();
            foreach (var e in Entries.Where(e => e.EmployeeId == p.EmployeeId))
            {
                var d = DateOnly.FromDateTime(e.Start);
                Assert.Equal(DateTimeKind.Unspecified, e.Start.Kind);
                Assert.InRange(d, Options.WindowStart, Options.AnchorDate);
                Assert.DoesNotContain(approved, l => l.From <= d && d <= l.To);
                if (!(p.Login == "anna.nowak" && d == saturday))
                {
                    Assert.True(Calendar.IsWorkingDay(d), $"{p.Login} {d}");
                }
            }
        }

        var annaLeave = Leave.First(l => l.EmployeeId == Person("anna.nowak").EmployeeId && l.Status == LeaveStatus.Approved);
        Assert.Contains(Entries, e => e.EmployeeId == Person("anna.nowak").EmployeeId && DateOnly.FromDateTime(e.Start) == annaLeave.From.AddDays(-3));
        Assert.Contains(Entries, e => DateOnly.FromDateTime(e.Start) == Options.AnchorDate);
    }

    [Fact]
    public void Regular_people_have_no_rest_violations_and_no_weekly_excess()
    {
        var apr = (Options.WindowStart, Options.WindowStart.AddMonths(4).AddDays(-1));
        var jun = DemoScenarios.JanOvertimePeriod(Options.AnchorDate);

        foreach (var p in People.Where(p => !IsSpecial(p)))
        {
            var entries = Of(p);
            Assert.NotEmpty(entries);
            Assert.Empty(RestRules.Check(entries));
            Assert.False(WeeklyAverageRules.Check(entries, apr.Item1, apr.Item2).ExceedsLimit, p.Login);
            Assert.False(WeeklyAverageRules.Check(entries, jun.From, jun.To).ExceedsLimit, p.Login);
        }
    }

    [Fact]
    public void Only_four_generated_people_have_overtime_and_Tomasz_has_none()
    {
        var withOvertime = People
            .Where(p => !IsSpecial(p) && OvertimeCalculator.Calculate(Of(p), Calendar, NightWindow.Default).Total > 0)
            .ToList();
        var generated = People.Where(p => !p.IsNamed).ToList();

        Assert.Equal(4, withOvertime.Count);
        Assert.All(withOvertime, p => Assert.False(p.IsNamed));
        Assert.Equal(
            new[] { generated[3], generated[9], generated[14], generated[20] }.Select(p => p.Login).OrderBy(x => x),
            withOvertime.Select(p => p.Login).OrderBy(x => x));
        foreach (var login in new[] { "tomasz.lis", "ewa.wisniewska", "piotr.zielinski", "maria.kaminska" })
        {
            var p = People.SingleOrDefault(x => x.Login == login) ?? Person(login);
            Assert.Equal(0m, OvertimeCalculator.Calculate(Of(p), Calendar, NightWindow.Default).Total);
        }
    }

    [Fact]
    public void UC12_Anna_has_exactly_one_daily_rest_violation_on_scenario_tuesday()
    {
        var anna = Person("anna.nowak");
        var tuesday = DemoScenarios.AnnaDailyRestTuesday(Options.AnchorDate);
        var violations = RestRules.Check(Of(anna)).Where(v => v.RuleId == "KP-132-1").ToList();

        var single = Assert.Single(violations);
        Assert.Equal(tuesday.ToDateTime(new TimeOnly(22, 30)), single.From);
        var own = Entries.Where(e => e.EmployeeId == anna.EmployeeId).ToList();
        var tue = own.Single(e => DateOnly.FromDateTime(e.Start) == tuesday);
        var wed = own.Single(e => DateOnly.FromDateTime(e.Start) == tuesday.AddDays(1));
        Assert.Equal((tuesday.ToDateTime(new TimeOnly(14, 0)), tuesday.ToDateTime(new TimeOnly(22, 30)), TimeSpan.FromMinutes(30)), (tue.Start, tue.End, tue.Break));
        Assert.Equal((tuesday.AddDays(1).ToDateTime(new TimeOnly(7, 30)), tuesday.AddDays(1).ToDateTime(new TimeOnly(15, 30)), TimeSpan.FromMinutes(30)), (wed.Start, wed.End, wed.Break));
    }

    [Fact]
    public void UC13_Anna_has_exactly_one_weekly_rest_violation_in_scenario_week()
    {
        var anna = Person("anna.nowak");
        var saturday = DemoScenarios.AnnaWeeklyRestSaturday(Options.AnchorDate);
        var monday = saturday.AddDays(-5);
        var violations = RestRules.Check(Of(anna)).Where(v => v.RuleId == "KP-133-1").ToList();

        var single = Assert.Single(violations);
        Assert.InRange(DateOnly.FromDateTime(single.From), monday, monday.AddDays(6));
        var sat = Entries.Single(e => e.EmployeeId == anna.EmployeeId && DateOnly.FromDateTime(e.Start) == saturday);
        Assert.Equal((saturday.ToDateTime(new TimeOnly(14, 0)), saturday.ToDateTime(new TimeOnly(20, 0)), TimeSpan.Zero), (sat.Start, sat.End, sat.Break));
    }

    [Fact]
    public void Anna_has_no_other_violations()
    {
        var violations = RestRules.Check(Of(Person("anna.nowak")));

        Assert.Equal(2, violations.Count);
        Assert.Equal(new[] { "KP-132-1", "KP-133-1" }, violations.Select(v => v.RuleId).OrderBy(x => x));
    }

    [Fact]
    public void UC14_Jan_exceeds_48h_average_over_overtime_period_and_has_overtime_each_month()
    {
        var jan = Person("jan.kowalski");
        var period = DemoScenarios.JanOvertimePeriod(Options.AnchorDate);
        var result = WeeklyAverageRules.Check(Of(jan), period.From, period.To);

        Assert.True(result.ExceedsLimit);
        Assert.True(result.AverageWeeklyHours > 48m);
        foreach (var month in new[] { 6, 7, 8, 9 })
        {
            var from = new DateOnly(2026, month, 1);
            Assert.True(Overtime(jan, from, from.AddMonths(1).AddDays(-1)) > 0m, $"miesiac {month}");
        }

        var day = Entries.Single(e => e.EmployeeId == jan.EmployeeId && DateOnly.FromDateTime(e.Start) == new DateOnly(2026, 6, 2));
        Assert.Equal((new TimeSpan(8, 0, 0), new TimeSpan(18, 30, 0), TimeSpan.FromMinutes(30)), (day.Start.TimeOfDay, day.End.TimeOfDay, day.Break));
        Assert.Empty(RestRules.Check(Of(jan)));
    }

    [Fact]
    public void Tomasz_has_zero_alerts()
    {
        var tomasz = Person("tomasz.lis");
        var entries = Of(tomasz);
        var period = DemoScenarios.JanOvertimePeriod(Options.AnchorDate);

        Assert.NotEmpty(entries);
        Assert.Empty(RestRules.Check(entries));
        Assert.Equal(0m, OvertimeCalculator.Calculate(entries, Calendar, NightWindow.Default).Total);
        Assert.False(WeeklyAverageRules.Check(entries, period.From, period.To).ExceedsLimit);
    }

    [Fact]
    public void Entries_never_overlap_and_ids_are_deterministic()
    {
        foreach (var p in People)
        {
            var own = Entries.Where(e => e.EmployeeId == p.EmployeeId).OrderBy(e => e.Start).ToList();
            for (var i = 1; i < own.Count; i++)
            {
                Assert.True(own[i - 1].End <= own[i].Start, $"{p.Login} {own[i].Start}");
            }
        }

        Assert.Equal(Entries.Count, Entries.Select(e => e.Id).Distinct().Count());
        var options = new JsonSerializerOptions { IncludeFields = true };
        var first = JsonSerializer.Serialize(Entries.Select(e => (e.Id, e.EmployeeId, e.Start, e.End, e.Break)), options);
        var second = JsonSerializer.Serialize(Build().Select(e => (e.Id, e.EmployeeId, e.Start, e.End, e.Break)), options);
        Assert.Equal(first, second);
        Assert.True(first.Length > 1000);
        var other = DemoTimesheets.Build(Options with { Seed = Options.Seed + 1 }, People, Leave, Calendar);
        Assert.NotEqual(Entries[0].Id, other[0].Id);
    }
}
