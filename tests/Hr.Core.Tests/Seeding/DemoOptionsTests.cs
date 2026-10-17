using Hr.Core.Seeding.Demo;
using Hr.Rules.Calendar;

namespace Hr.Core.Tests.Seeding;

public class DemoOptionsTests
{
    private static readonly WorkCalendar Calendar = new();

    [Fact]
    public void Default_has_anchor_2026_10_16_seed_20261016_profile_full()
    {
        var o = DemoOptions.Default;

        Assert.Equal(new DateOnly(2026, 10, 16), o.AnchorDate);
        Assert.Equal(20261016, o.Seed);
        Assert.Equal(DemoProfile.Full, o.Profile);
    }

    [Theory]
    [InlineData(2026, 10, 16, 2026, 4, 1)]
    [InlineData(2027, 3, 12, 2026, 9, 1)]
    public void Window_starts_on_first_day_six_months_before_anchor_month(
        int ay, int am, int ad, int wy, int wm, int wd)
    {
        var o = DemoOptions.Default with { AnchorDate = new DateOnly(ay, am, ad) };

        Assert.Equal(new DateOnly(wy, wm, wd), o.WindowStart);
    }

    [Fact]
    public void PayrollMonths_are_full_months_before_anchor_month()
    {
        var months = DemoOptions.Default.PayrollMonths;

        Assert.Equal(6, months.Count);
        Assert.Equal((2026, 4), months[0]);
        Assert.Equal((2026, 9), months[^1]);
    }

    [Theory]
    [InlineData("2026-10-17")]
    [InlineData("2026-11-01")]
    public void Parse_rejects_non_working_day_anchor(string anchor)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DemoOptions.Parse(anchor, null, null, Calendar));

        Assert.Contains("dzień roboczy", ex.Message);
    }

    [Theory]
    [InlineData("16.10.2026", null, null, "Hr:DemoAnchorDate")]
    [InlineData(null, "abc", null, "Hr:DemoSeed")]
    [InlineData(null, null, "Mini", "Hr:DemoProfile")]
    public void Parse_rejects_bad_format_seed_and_profile(
        string? anchor, string? seed, string? profile, string key)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DemoOptions.Parse(anchor, seed, profile, Calendar));

        Assert.Contains(key, ex.Message);
    }

    [Fact]
    public void Parse_accepts_values_and_defaults_missing()
    {
        Assert.Equal(DemoOptions.Default, DemoOptions.Parse(null, null, null, Calendar));

        var o = DemoOptions.Parse("2026-11-13", "7", "Accounts", Calendar);

        Assert.Equal(new DateOnly(2026, 11, 13), o.AnchorDate);
        Assert.Equal(7, o.Seed);
        Assert.Equal(DemoProfile.Accounts, o.Profile);
    }
}
