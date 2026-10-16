using Hr.Rules.Rest;
using Hr.Rules.Time;

namespace Hr.Rules.Tests.Rest;

public class RestRulesTests
{
    private static WorkEntry W(int day, int h1, int day2, int h2) =>
        new(new(2026, 10, day, h1, 0, 0), new(2026, 10, day2, h2, 0, 0), TimeSpan.Zero);

    [Fact]
    public void UC12_Daily_rest_10h_is_violation()
    {
        var v = RestRules.Check(new[] { W(12, 8, 12, 22), W(13, 8, 13, 16) });
        var d = Assert.Single(v, x => x.RuleId == "KP-132-1");
        Assert.Equal(10m, d.RestHours);
        Assert.Equal(11m, d.RequiredHours);
    }

    [Fact]
    public void UC12_Daily_rest_exactly_11h_is_ok() =>
        Assert.DoesNotContain(RestRules.Check(new[] { W(12, 8, 12, 21), W(13, 8, 13, 16) }), x => x.RuleId == "KP-132-1");

    [Fact]
    public void UC12_Same_day_entries_are_not_checked_for_daily_rest() =>
        Assert.Empty(RestRules.Check(new[] { W(12, 8, 12, 12), W(12, 13, 12, 17) }));

    [Fact]
    public void UC12_Night_shift_then_early_next_entry_violates()
    {
        var v = RestRules.Check(new[] { W(12, 22, 13, 6), W(13, 14, 13, 20) });
        Assert.Contains(v, x => x.RuleId == "KP-132-1" && x.RestHours == 8m);
    }

    [Fact]
    public void UC13_Normal_week_has_no_violation() =>
        Assert.Empty(RestRules.Check(Enumerable.Range(12, 5).Select(d => W(d, 8, d, 16)).ToArray()));

    [Fact]
    public void UC13_Seven_day_week_violates_weekly_rest()
    {
        var v = RestRules.Check(Enumerable.Range(12, 7).Select(d => W(d, 8, d, 16)).ToArray());
        var w = Assert.Single(v, x => x.RuleId == "KP-133-1");
        Assert.Equal(16m, w.RestHours);
        Assert.Equal(35m, w.RequiredHours);
    }

    [Fact]
    public void UC13_Entry_across_Sunday_Monday_boundary_gives_only_daily_violation()
    {
        var entries = new List<WorkEntry> { W(11, 22, 12, 6) };
        entries.AddRange(Enumerable.Range(12, 5).Select(d => W(d, 8, d, 16)));
        var v = RestRules.Check(entries);
        var daily = Assert.Single(v);
        Assert.Equal("KP-132-1", daily.RuleId);
        Assert.Equal(2m, daily.RestHours);
    }

    [Fact]
    public void Unsorted_input_is_sorted() =>
        Assert.Empty(RestRules.Check(new[] { W(14, 8, 14, 16), W(12, 8, 12, 16), W(13, 8, 13, 16) }));

    [Fact]
    public void Overlapping_entries_throw() =>
        Assert.Throws<ArgumentException>(() => RestRules.Check(new[] { W(12, 8, 12, 16), W(12, 15, 12, 20) }));
}
