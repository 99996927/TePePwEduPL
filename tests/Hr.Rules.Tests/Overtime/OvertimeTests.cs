using Hr.Rules.Calendar;
using Hr.Rules.Overtime;
using Hr.Rules.Time;

namespace Hr.Rules.Tests.Overtime;

public class OvertimeTests
{
    private static OvertimeResult Calc(params WorkEntry[] entries) =>
        OvertimeCalculator.Calculate(entries, new WorkCalendar(), NightWindow.Default);

    private static WorkEntry E(int day, int h1, int m1, int day2, int h2, int m2, int breakMinutes = 0) =>
        new(new(2026, 10, day, h1, m1, 0), new(2026, 10, day2, h2, m2, 0), TimeSpan.FromMinutes(breakMinutes));

    [Fact]
    public void UC10_Within_norm_is_zero() =>
        Assert.Equal(0m, Calc(E(12, 8, 0, 12, 17, 0, 60)).Total);

    [Fact]
    public void UC10_Weekday_two_hours_over_is_50()
    {
        var r = Calc(E(12, 8, 0, 12, 18, 0));
        Assert.Equal(2m, r.Hours50);
        Assert.Equal(0m, r.Hours100);
    }

    [Fact]
    public void UC10_Weekday_late_hours_in_night_are_100()
    {
        var r = Calc(E(12, 10, 0, 12, 22, 0));
        Assert.Equal(3m, r.Hours50);
        Assert.Equal(1m, r.Hours100);
    }

    [Fact]
    public void UC10_Saturday_work_is_50() =>
        Assert.Equal(4m, Calc(E(17, 8, 0, 17, 12, 0)).Hours50);

    [Fact]
    public void UC10_Sunday_work_is_100() =>
        Assert.Equal(4m, Calc(E(18, 8, 0, 18, 12, 0)).Hours100);

    [Fact]
    public void UC10_Holiday_work_is_100()
    {
        var entry = new WorkEntry(new(2026, 11, 11, 8, 0, 0), new(2026, 11, 11, 12, 0, 0), TimeSpan.Zero);
        var r = OvertimeCalculator.Calculate(new[] { entry }, new WorkCalendar(), NightWindow.Default);
        Assert.Equal(4m, r.Hours100);
    }

    [Fact]
    public void UC10_Night_shift_across_midnight_assigned_to_start_date() =>
        Assert.Equal(0m, Calc(E(12, 22, 0, 13, 6, 0)).Total);

    [Fact]
    public void UC10_Two_entries_same_day_sum_over_8h() =>
        Assert.Equal(3m, Calc(E(12, 6, 0, 12, 12, 0), E(12, 14, 0, 12, 19, 0)).Hours50);

    [Fact]
    public void UC16_Result_lists_rule_ids()
    {
        var r = Calc(E(12, 8, 0, 12, 18, 0));
        Assert.Contains("KP-151-1", r.RuleIds);
        Assert.Contains("KP-151-1-1", r.RuleIds);
    }

    [Theory]
    [InlineData(150, false)]
    [InlineData(150.5, true)]
    public void Annual_limit_is_150(double hours, bool exceeded) =>
        Assert.Equal(exceeded, OvertimeLimit.Exceeded((decimal)hours));

    [Fact]
    public void Calculate_is_deterministic()
    {
        var entries = new[] { E(12, 8, 0, 12, 18, 0) };
        Assert.Equal(
            OvertimeCalculator.Calculate(entries, new WorkCalendar(), NightWindow.Default),
            OvertimeCalculator.Calculate(entries, new WorkCalendar(), NightWindow.Default));
    }
}
