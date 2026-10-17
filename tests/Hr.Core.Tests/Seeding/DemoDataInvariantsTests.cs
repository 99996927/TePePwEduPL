using System.Text.Json;
using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Seeding.Demo;
using Hr.Rules.Calendar;
using Hr.Rules.Leave;
using Hr.Rules.Rest;

namespace Hr.Core.Tests.Seeding;

public class DemoDataInvariantsTests
{
    private static readonly WorkCalendar Calendar = new();

    public static IEnumerable<object[]> Cases() =>
        from anchor in new[] { "2026-10-16", "2026-11-13", "2027-03-12" }
        from seed in new[] { 20261016, 7 }
        select new object[] { anchor, seed };

    private static DemoOptions Options(string anchor, int seed) =>
        new(seed, DateOnly.Parse(anchor), DemoProfile.Full);

    private static DemoDataSet Build(string anchor, int seed) => DemoDataGenerator.Build(Options(anchor, seed), Calendar);

    private static bool IsScenario(DemoPerson p) => p.Login is "anna.nowak" or "jan.kowalski";

    [Fact]
    public void Determinism_two_builds_taken_apart_serialize_identically()
    {
        var first = JsonSerializer.Serialize(DemoDataGenerator.Build(DemoOptions.Default, Calendar));
        Thread.Sleep(25);
        var set = DemoDataGenerator.Build(DemoOptions.Default, Calendar);
        var second = JsonSerializer.Serialize(set);

        Assert.Equal(31, set.People.Count);
        Assert.NotEmpty(set.LeaveRequests);
        Assert.NotEmpty(set.WorkEntries);
        Assert.NotEmpty(set.PayrollChanges);
        Assert.NotEmpty(set.Conversations);
        Assert.Contains("anna.nowak", first);
        Assert.Equal(first, second);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Work_entries_stay_in_window_on_working_days_and_outside_approved_leave(string anchor, int seed)
    {
        var o = Options(anchor, seed);
        var set = Build(anchor, seed);
        var anna = set.People.Single(p => p.Login == "anna.nowak");
        var saturday = DemoScenarios.AnnaWeeklyRestSaturday(o.AnchorDate);
        Assert.NotEmpty(set.WorkEntries);

        foreach (var e in set.WorkEntries)
        {
            var d = DateOnly.FromDateTime(e.Start);
            Assert.InRange(d, o.WindowStart, o.AnchorDate);
            Assert.Equal(DateTimeKind.Unspecified, e.Start.Kind);
            Assert.DoesNotContain(set.LeaveRequests,
                l => l.EmployeeId == e.EmployeeId && l.Status == LeaveStatus.Approved && l.From <= d && d <= l.To);
            if (!(e.EmployeeId == anna.EmployeeId && d == saturday))
            {
                Assert.True(Calendar.IsWorkingDay(d), $"{d} nie jest dniem roboczym");
            }
        }

        Assert.Contains(set.WorkEntries, e => e.EmployeeId == anna.EmployeeId && DateOnly.FromDateTime(e.Start) == saturday);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Regular_people_have_no_rest_violations_and_no_average_excess(string anchor, int seed)
    {
        var o = Options(anchor, seed);
        var set = Build(anchor, seed);

        foreach (var p in set.People.Where(p => !IsScenario(p)))
        {
            var entries = set.WorkEntries.Where(e => e.EmployeeId == p.EmployeeId)
                .OrderBy(e => e.Start).Select(e => e.ToRulesEntry()).ToList();
            Assert.NotEmpty(entries);
            Assert.Empty(RestRules.Check(entries));
            Assert.False(WeeklyAverageRules.Check(entries, o.WindowStart, o.AnchorDate).ExceedsLimit, p.Login);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Active_leave_stays_within_entitlement_and_does_not_overlap(string anchor, int seed)
    {
        var o = Options(anchor, seed);
        var set = Build(anchor, seed);
        Assert.NotEmpty(set.LeaveRequests);
        var peopleWithActiveLeave = 0;

        foreach (var p in set.People)
        {
            var active = set.LeaveRequests
                .Where(l => l.EmployeeId == p.EmployeeId && l.Status is LeaveStatus.Approved or LeaveStatus.Pending)
                .OrderBy(l => l.From).ToList();
            if (active.Count > 0)
            {
                peopleWithActiveLeave++;
            }

            Assert.All(active, l =>
            {
                Assert.Equal(l.From.Year, l.To.Year);
                Assert.Equal(Calendar.CountWorkingDays(l.From, l.To), l.Days);
                Assert.True(l.From >= p.HireDate, p.Login);
            });
            for (var i = 1; i < active.Count; i++)
            {
                Assert.True(active[i - 1].To < active[i].From, $"{p.Login} nakładające się urlopy");
            }

            foreach (var year in active.GroupBy(l => l.From.Year))
            {
                var entitlement = LeaveEntitlement.AnnualDays(LeaveEntitlement.YearsOfService(p.HireDate, o.AnchorDate));
                Assert.True(year.Sum(l => l.Days) <= entitlement, $"{p.Login} {year.Key}");
            }
        }

        Assert.True(peopleWithActiveLeave > 0);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void People_have_valid_logins_passwords_managers_hire_dates_and_salaries(string anchor, int seed)
    {
        var o = Options(anchor, seed);
        var set = Build(anchor, seed);

        Assert.Equal(31, set.People.Count);
        Assert.Equal(5, set.Teams.Count);
        Assert.Equal(set.People.Count, set.People.Select(p => p.Login).Distinct().Count());
        Assert.Equal(set.People.Count, set.People.Select(p => p.Last).Distinct().Count());
        Assert.All(set.People, p =>
        {
            Assert.All(p.Login, c => Assert.True(c is >= 'a' and <= 'z' or '.', p.Login));
            Assert.Equal(p.Login.Replace(".", string.Empty) + "123", p.Password);
        });

        foreach (var team in set.Teams)
        {
            var managers = set.People.Where(p => p.TeamName == team.Name && p.Role == Roles.Manager).ToList();
            if (team.Name == DemoPeople.TeamHr)
            {
                Assert.Empty(managers);
                Assert.Null(team.ManagerEmployeeId);
            }
            else
            {
                Assert.Equal(managers.Single().EmployeeId, team.ManagerEmployeeId);
            }
        }

        foreach (var p in set.People.Where(p => !p.IsNamed))
        {
            Assert.InRange(p.HireDate, new DateOnly(2008, 1, 1), o.WindowStart.AddDays(-1));
            var years = LeaveEntitlement.YearsOfService(p.HireDate, o.AnchorDate);
            var (min, max) = DemoPeople.SalaryRange(years, p.Role);
            Assert.InRange(p.MonthlySalary, min, max);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Payroll_changes_are_never_decided_by_their_creator(string anchor, int seed)
    {
        var set = Build(anchor, seed);
        Assert.NotEmpty(set.PayrollChanges);

        Assert.All(set.PayrollChanges, c => Assert.NotEqual(c.CreatedByUserId, c.DecidedByUserId));
        Assert.All(set.PayrollChanges.Where(c => c.Status != PayrollChangeStatus.Pending), c => Assert.NotNull(c.DecidedByUserId));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Ids_are_unique_across_the_whole_set(string anchor, int seed)
    {
        var set = Build(anchor, seed);
        var ids = new List<Guid>();
        ids.AddRange(set.Teams.Select(t => t.Id));
        ids.AddRange(set.People.SelectMany(p => new[] { p.EmployeeId, p.UserId }));
        ids.AddRange(set.LeaveRequests.Select(l => l.Id));
        ids.AddRange(set.WorkEntries.Select(w => w.Id));
        ids.AddRange(set.PayrollChanges.Select(c => c.Id));
        ids.AddRange(set.Conversations.Select(c => c.Id));
        ids.AddRange(set.Conversations.SelectMany(c => c.Messages).Select(m => m.Id));

        Assert.True(ids.Count > 1000);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Accounts_profile_returns_six_people_without_data(string anchor, int seed)
    {
        var set = DemoDataGenerator.Build(Options(anchor, seed) with { Profile = DemoProfile.Accounts }, Calendar);

        Assert.Equal(6, set.People.Count);
        Assert.Equal(3, set.Teams.Count);
        Assert.Empty(set.LeaveRequests);
        Assert.Empty(set.WorkEntries);
        Assert.Empty(set.PayrollChanges);
        Assert.Empty(set.Conversations);
    }
}
