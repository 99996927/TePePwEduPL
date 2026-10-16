using Hr.Rules.Calendar;
using Hr.Rules.Leave;

namespace Hr.Rules.Tests.Leave;

public class LeaveRulesTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(9, 20)]
    [InlineData(10, 26)]
    [InlineData(25, 26)]
    public void UC05_AnnualDays_by_service(int years, int days) =>
        Assert.Equal(days, LeaveEntitlement.AnnualDays(years));

    [Fact]
    public void UC05_YearsOfService_counts_full_years()
    {
        Assert.Equal(9, LeaveEntitlement.YearsOfService(new(2016, 10, 17), new(2026, 10, 16)));
        Assert.Equal(10, LeaveEntitlement.YearsOfService(new(2016, 10, 16), new(2026, 10, 16)));
    }

    [Fact]
    public void UC06_Evaluate_week_is_5_days_and_sufficient()
    {
        var r = LeaveRequestRules.Evaluate(20, 10, new(2026, 10, 12), new(2026, 10, 16), new WorkCalendar());
        Assert.Equal(5, r.Days);
        Assert.True(r.Sufficient);
        Assert.Equal(5, r.RemainingAfter);
        Assert.Equal("KP-154-1", r.RuleId);
    }

    [Fact]
    public void UC07_Evaluate_over_balance_is_not_sufficient()
    {
        var r = LeaveRequestRules.Evaluate(20, 18, new(2026, 10, 12), new(2026, 10, 16), new WorkCalendar());
        Assert.False(r.Sufficient);
        Assert.Equal(-3, r.RemainingAfter);
    }

    [Fact]
    public void UC06_Evaluate_skips_weekend_and_holiday() =>
        Assert.Equal(4, LeaveRequestRules.Evaluate(20, 0, new(2026, 11, 9), new(2026, 11, 13), new WorkCalendar()).Days);

    [Fact]
    public void UC06_Evaluate_throws_when_to_before_from() =>
        Assert.Throws<ArgumentException>(() => LeaveRequestRules.Evaluate(20, 0, new(2026, 10, 16), new(2026, 10, 12), new WorkCalendar()));
}
