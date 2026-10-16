using Hr.Rules.Calendar;

namespace Hr.Rules.Tests.Calendar;

public class WorkCalendarTests
{
    [Fact]
    public void ForYear_2026_contains_movable_holidays()
    {
        var h = PolishHolidays.ForYear(2026);
        Assert.Contains(new DateOnly(2026, 4, 5), h);   // Wielkanoc
        Assert.Contains(new DateOnly(2026, 4, 6), h);   // Poniedziałek Wielkanocny
        Assert.Contains(new DateOnly(2026, 5, 24), h);  // Zielone Świątki
        Assert.Contains(new DateOnly(2026, 6, 4), h);   // Boże Ciało
        Assert.Equal(14, h.Count);                      // 9 stałych + 24.12 + 4 ruchome
    }

    [Fact]
    public void ForYear_2024_Easter_is_31_March_and_no_Christmas_Eve()
    {
        var h = PolishHolidays.ForYear(2024);
        Assert.Contains(new DateOnly(2024, 3, 31), h);
        Assert.DoesNotContain(new DateOnly(2024, 12, 24), h);
    }

    [Fact]
    public void ForYear_2025_contains_Christmas_Eve() =>
        Assert.Contains(new DateOnly(2025, 12, 24), PolishHolidays.ForYear(2025));

    [Fact]
    public void CountWorkingDays_week_without_holiday_is_5() =>
        Assert.Equal(5, new WorkCalendar().CountWorkingDays(new(2026, 10, 12), new(2026, 10, 16)));

    [Fact]
    public void CountWorkingDays_week_with_11_Nov_is_4() =>
        Assert.Equal(4, new WorkCalendar().CountWorkingDays(new(2026, 11, 9), new(2026, 11, 13)));

    [Fact]
    public void IsWorkingDay_29_Feb_2028_leap_year() =>
        Assert.True(new WorkCalendar().IsWorkingDay(new(2028, 2, 29)));

    [Fact]
    public void IsWorkingDay_false_on_weekend_and_holiday()
    {
        var c = new WorkCalendar();
        Assert.False(c.IsWorkingDay(new(2026, 10, 17)));
        Assert.False(c.IsWorkingDay(new(2026, 10, 18)));
        Assert.False(c.IsWorkingDay(new(2026, 12, 24)));
    }
}
