using Hr.Rules.Rest;
using Hr.Rules.Time;

namespace Hr.Rules.Tests.Rest;

public class WeeklyAverageRulesTests
{
    private static List<WorkEntry> Weeks(DateTime monday, int weeks, TimeSpan length) =>
        Enumerable.Range(0, weeks * 7)
            .Select(i => monday.AddDays(i))
            .Where(d => d.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday)
            .Select(d => new WorkEntry(d.AddHours(8), d.AddHours(8) + length, TimeSpan.Zero))
            .ToList();

    [Fact]
    public void UC14_Average_50h_exceeds()
    {
        var r = WeeklyAverageRules.Check(Weeks(new(2026, 10, 5), 4, TimeSpan.FromHours(10)), new(2026, 10, 5), new(2026, 11, 1));
        Assert.Equal(50m, r.AverageWeeklyHours);
        Assert.True(r.ExceedsLimit);
        Assert.Equal("KP-131-1", r.RuleId);
    }

    [Fact]
    public void UC14_Average_exactly_48h_does_not_exceed()
    {
        var r = WeeklyAverageRules.Check(Weeks(new(2026, 10, 5), 4, TimeSpan.FromMinutes(576)), new(2026, 10, 5), new(2026, 11, 1));
        Assert.Equal(48m, r.AverageWeeklyHours);
        Assert.False(r.ExceedsLimit);
    }

    [Fact]
    public void UC14_Standard_40h_week_is_ok()
    {
        var r = WeeklyAverageRules.Check(Weeks(new(2026, 10, 5), 4, TimeSpan.FromHours(8)), new(2026, 10, 5), new(2026, 11, 1));
        Assert.Equal(40m, r.AverageWeeklyHours);
        Assert.False(r.ExceedsLimit);
    }

    [Fact]
    public void UC14_Overlapping_entries_throw()
    {
        var entries = new List<WorkEntry>
        {
            new(new(2026, 10, 12, 8, 0, 0), new(2026, 10, 12, 16, 0, 0), TimeSpan.Zero),
            new(new(2026, 10, 12, 12, 0, 0), new(2026, 10, 12, 20, 0, 0), TimeSpan.Zero),
        };
        Assert.Throws<ArgumentException>(() => WeeklyAverageRules.Check(entries, new(2026, 10, 5), new(2026, 11, 1)));
    }

    [Fact]
    public void UC14_Throws_when_period_reversed() =>
        Assert.Throws<ArgumentException>(() => WeeklyAverageRules.Check(Array.Empty<WorkEntry>(), new(2026, 11, 1), new(2026, 10, 5)));
}
