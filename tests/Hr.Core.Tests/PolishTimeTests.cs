using Hr.Core.Abstractions;

namespace Hr.Core.Tests;

public class PolishTimeTests
{
    [Fact]
    public void ToUtc_converts_summer_time_with_two_hour_offset()
    {
        Assert.Equal(new DateTime(2026, 7, 13, 6, 0, 0), PolishTime.ToUtc(new DateTime(2026, 7, 13, 8, 0, 0)));
    }

    [Fact]
    public void ToUtc_converts_winter_time_with_one_hour_offset()
    {
        Assert.Equal(new DateTime(2026, 1, 13, 7, 0, 0), PolishTime.ToUtc(new DateTime(2026, 1, 13, 8, 0, 0)));
    }

    [Fact]
    public void ToUtc_is_inverse_of_ToLocal()
    {
        var utc = new DateTime(2026, 7, 13, 6, 0, 0, DateTimeKind.Utc);
        Assert.Equal(utc, PolishTime.ToUtc(PolishTime.ToLocal(utc)));
    }
}
