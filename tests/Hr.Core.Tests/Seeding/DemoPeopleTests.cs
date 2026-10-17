using System.Text.Json;
using System.Text.RegularExpressions;
using Hr.Core.Abstractions;
using Hr.Core.Seeding.Demo;

namespace Hr.Core.Tests.Seeding;

public class DemoPeopleTests
{
    private static readonly DemoOptions Full = DemoOptions.Default;
    private static readonly DemoOptions Accounts = DemoOptions.Default with { Profile = DemoProfile.Accounts };

    private static int Years(DateOnly hire, DateOnly anchor)
    {
        var years = anchor.Year - hire.Year;
        return anchor < hire.AddYears(years) ? years - 1 : years;
    }

    [Fact]
    public void Full_has_31_people_in_5_teams_with_sizes_7_7_7_7_3()
    {
        var (teams, people) = DemoPeople.Build(Full);

        Assert.Equal(5, teams.Count);
        Assert.Equal(31, people.Count);
        Assert.Equal(
            new[] { 7, 7, 7, 7, 3 },
            new[] { DemoPeople.TeamEwa, DemoPeople.TeamB, DemoPeople.TeamC, DemoPeople.TeamD, DemoPeople.TeamHr }
                .Select(t => people.Count(p => p.TeamName == t)).ToArray());
    }

    [Fact]
    public void Accounts_profile_has_only_six_named_people_in_three_teams()
    {
        var (teams, people) = DemoPeople.Build(Accounts);

        Assert.Equal(3, teams.Count);
        Assert.Equal(6, people.Count);
        Assert.All(people, p => Assert.True(p.IsNamed));
        Assert.Equal(
            new[] { DemoPeople.TeamEwa, DemoPeople.TeamB, DemoPeople.TeamHr }.Order(),
            teams.Select(t => t.Name).Order());
    }

    [Theory]
    [InlineData("anna.nowak", "Employee", "Zespół Ewy", 2019, 3, 1, 7200)]
    [InlineData("jan.kowalski", "Employee", "Zespół Ewy", 2014, 5, 1, 8000)]
    [InlineData("tomasz.lis", "Employee", "Zespół B", 2022, 9, 1, 6800)]
    [InlineData("ewa.wisniewska", "Manager", "Zespół Ewy", 2012, 1, 2, 11000)]
    [InlineData("piotr.zielinski", "HR", "Kadry", 2018, 6, 1, 9000)]
    [InlineData("maria.kaminska", "HRApprover", "Kadry", 2015, 2, 1, 12000)]
    public void Named_accounts_keep_login_role_team_hire_and_salary(
        string login, string role, string team, int y, int m, int d, int salary)
    {
        foreach (var o in new[] { Full, Accounts })
        {
            var p = DemoPeople.Build(o).Item2.Single(x => x.Login == login);

            Assert.True(p.IsNamed);
            Assert.Equal(role, p.Role);
            Assert.Equal(team, p.TeamName);
            Assert.Equal(new DateOnly(y, m, d), p.HireDate);
            Assert.Equal((decimal)salary, p.MonthlySalary);
        }

        Assert.Equal("annanowak123", DemoPeople.Build(Full).Item2.Single(x => x.Login == "anna.nowak").Password);
    }

    [Fact]
    public void Each_business_team_has_exactly_one_manager_and_hr_team_none()
    {
        var (teams, people) = DemoPeople.Build(Full);

        foreach (var team in teams)
        {
            var managers = people.Where(p => p.TeamName == team.Name && p.Role == Roles.Manager).ToList();
            if (team.Name == DemoPeople.TeamHr)
            {
                Assert.Empty(managers);
                Assert.Null(team.ManagerEmployeeId);
            }
            else
            {
                Assert.Single(managers);
                Assert.Equal(managers[0].EmployeeId, team.ManagerEmployeeId);
            }
        }

        var ewa = people.Single(p => p.Login == "ewa.wisniewska");
        Assert.Equal(ewa.EmployeeId, teams.Single(t => t.Name == DemoPeople.TeamEwa).ManagerEmployeeId);
    }

    [Fact]
    public void Last_names_and_logins_are_unique_and_ascii()
    {
        var people = DemoPeople.Build(Full).Item2;

        Assert.Equal(31, people.Select(p => p.Last).Distinct().Count());
        Assert.Equal(31, people.Select(p => p.Login).Distinct().Count());
        Assert.Equal(31, people.Select(p => p.EmployeeId).Distinct().Count());
        Assert.Equal(31, people.Select(p => p.UserId).Distinct().Count());
        Assert.All(people, p =>
        {
            Assert.Matches(@"^[a-z]+\.[a-z]+$", p.Login);
            Assert.Matches(@"^[a-z]+123$", p.Password);
        });
    }

    [Fact]
    public void Generated_salary_and_hire_date_within_ranges()
    {
        var people = DemoPeople.Build(Full).Item2.Where(p => !p.IsNamed).ToList();

        Assert.Equal(25, people.Count);
        foreach (var p in people)
        {
            var (min, max) = DemoPeople.SalaryRange(Years(p.HireDate, Full.AnchorDate), p.Role);
            Assert.InRange(p.MonthlySalary, min, max);
            Assert.Equal(0m, p.MonthlySalary % 50m);
            Assert.InRange(p.HireDate, new DateOnly(2008, 1, 1), Full.WindowStart.AddDays(-1));
        }
    }

    [Fact]
    public void Same_seed_gives_same_people_and_other_seed_gives_other_generated_names()
    {
        var a = DemoPeople.Build(Full);
        var b = DemoPeople.Build(Full);
        var c = DemoPeople.Build(Full with { Seed = Full.Seed + 1 });

        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.NotEqual(
            a.Item2.Where(p => !p.IsNamed).Select(p => p.Last),
            c.Item2.Where(p => !p.IsNamed).Select(p => p.Last));
        Assert.Equal(
            a.Item2.Where(p => p.IsNamed).Select(p => p.Login),
            c.Item2.Where(p => p.IsNamed).Select(p => p.Login));
    }

    [Fact]
    public void DemoIds_are_stable_and_distinct_per_key()
    {
        Assert.Equal(DemoIds.For(1, "x"), DemoIds.For(1, "x"));
        Assert.NotEqual(DemoIds.For(1, "x"), DemoIds.For(1, "y"));
        Assert.NotEqual(DemoIds.For(1, "x"), DemoIds.For(2, "x"));
    }
}
