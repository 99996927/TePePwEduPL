using Hr.Rules.Calendar;
using Hr.Rules.Time;

namespace Hr.Rules.Tests.Time;

public class WorkingTimeNormTests
{
    [Fact]
    public void October_2026_is_176h() =>
        Assert.Equal(176m, WorkingTimeNorm.HoursForPeriod(new(2026, 10, 1), new(2026, 10, 31), new WorkCalendar()));

    [Fact]
    public void November_2026_sunday_holiday_does_not_reduce_but_11_Nov_does() =>
        Assert.Equal(160m, WorkingTimeNorm.HoursForPeriod(new(2026, 11, 1), new(2026, 11, 30), new WorkCalendar()));

    [Fact]
    public void December_2026_saturday_holiday_26_Dec_reduces_norm() =>
        Assert.Equal(160m, WorkingTimeNorm.HoursForPeriod(new(2026, 12, 1), new(2026, 12, 31), new WorkCalendar()));

    [Fact]
    public void Absence_hours_reduce_norm() =>
        Assert.Equal(168m, WorkingTimeNorm.HoursForPeriod(new(2026, 10, 1), new(2026, 10, 31), new WorkCalendar(), 8m));

    [Fact]
    public void Throws_when_period_reversed() =>
        Assert.Throws<ArgumentException>(() => WorkingTimeNorm.HoursForPeriod(new(2026, 10, 31), new(2026, 10, 1), new WorkCalendar()));

    [Fact]
    public void Throws_when_absence_negative() =>
        Assert.Throws<ArgumentException>(() => WorkingTimeNorm.HoursForPeriod(new(2026, 10, 1), new(2026, 10, 31), new WorkCalendar(), -1m));
}
