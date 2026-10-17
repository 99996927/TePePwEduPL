using System.Text.Json;
using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Seeding.Demo;

namespace Hr.Core.Tests.Seeding;

public class DemoPayrollChangesTests
{
    private static readonly DemoOptions Options = DemoOptions.Default;
    private static readonly IReadOnlyList<DemoPerson> People = DemoPeople.Build(Options).Item2;

    private static DemoPerson Person(string login) => People.Single(p => p.Login == login);

    private static IReadOnlyList<PayrollChange> Build() => DemoPayrollChanges.Build(Options, People);

    private static PayrollChange ChangeOf(string login) => Build().Single(c => c.EmployeeId == Person(login).EmployeeId);

    [Fact]
    public void UC18_Approved_historic_change_is_jans_fixed_allowance_400_from_june_first()
    {
        var change = ChangeOf("jan.kowalski");

        Assert.Equal(PayrollComponent.FixedAllowance, change.Component);
        Assert.Equal(400m, change.Value);
        Assert.Equal(new DateOnly(2026, 6, 1), change.EffectiveFrom);
        Assert.Equal(PayrollChangeStatus.Approved, change.Status);
        Assert.False(string.IsNullOrWhiteSpace(change.Reason));
        Assert.Equal(Person("piotr.zielinski").UserId, change.CreatedByUserId);
        Assert.Equal(Person("maria.kaminska").UserId, change.DecidedByUserId);
    }

    [Fact]
    public void UC18_Pending_change_is_annas_base_salary_7800_from_november_first_created_by_piotr_without_decision()
    {
        var change = ChangeOf("anna.nowak");

        Assert.Equal(PayrollComponent.BaseSalary, change.Component);
        Assert.Equal(7800m, change.Value);
        Assert.Equal(new DateOnly(2026, 11, 1), change.EffectiveFrom);
        Assert.Equal(PayrollChangeStatus.Pending, change.Status);
        Assert.Equal(Person("piotr.zielinski").UserId, change.CreatedByUserId);
        Assert.Null(change.DecidedByUserId);
        Assert.Null(change.DecisionReason);
    }

    [Fact]
    public void UC20_Rejected_change_is_tomaszs_fixed_allowance_300_from_september_first_with_reason()
    {
        var change = ChangeOf("tomasz.lis");

        Assert.Equal(PayrollComponent.FixedAllowance, change.Component);
        Assert.Equal(300m, change.Value);
        Assert.Equal(new DateOnly(2026, 9, 1), change.EffectiveFrom);
        Assert.Equal(PayrollChangeStatus.Rejected, change.Status);
        Assert.Equal(Person("piotr.zielinski").UserId, change.CreatedByUserId);
        Assert.Equal(Person("maria.kaminska").UserId, change.DecidedByUserId);
        Assert.False(string.IsNullOrWhiteSpace(change.DecisionReason));
    }

    [Fact]
    public void UC19_No_change_is_decided_by_its_creator_and_all_dates_within_window_plus_two_months()
    {
        var changes = Build();
        var from = DateOnly.FromDateTime(PolishTime.ToLocal(changes.Min(c => c.CreatedAtUtc)));
        var limit = Options.AnchorDate.AddMonths(2);

        Assert.Equal(3, changes.Count);
        Assert.Equal(3, changes.Select(c => c.Id).Distinct().Count());
        Assert.All(changes, c =>
        {
            Assert.NotEqual(c.CreatedByUserId, c.DecidedByUserId);
            Assert.InRange(c.EffectiveFrom, Options.WindowStart, limit);
            Assert.InRange(DateOnly.FromDateTime(PolishTime.ToLocal(c.CreatedAtUtc)), Options.WindowStart, Options.AnchorDate);
        });
        Assert.True(from >= Options.WindowStart);
    }

    [Fact]
    public void Changes_are_deterministic()
    {
        var first = JsonSerializer.Serialize(Build());
        var second = JsonSerializer.Serialize(DemoPayrollChanges.Build(Options, DemoPeople.Build(Options).Item2));

        Assert.Contains("7800", first);
        Assert.Equal(first, second);
        Assert.NotEqual(first, JsonSerializer.Serialize(DemoPayrollChanges.Build(
            Options with { Seed = 1 }, DemoPeople.Build(Options with { Seed = 1 }).Item2)));
    }
}
