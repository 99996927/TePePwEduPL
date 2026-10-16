using Hr.Rules.Time;

namespace Hr.Rules.Tests.Time;

public class WorkEntryTests
{
    [Fact]
    public void WorkedHours_subtracts_break() =>
        Assert.Equal(8.0m, new WorkEntry(new(2026, 10, 12, 8, 0, 0), new(2026, 10, 12, 17, 0, 0), TimeSpan.FromHours(1)).WorkedHours);

    [Fact]
    public void WorkedHours_supports_minutes() =>
        Assert.Equal(9.6m, new WorkEntry(new(2026, 10, 12, 8, 0, 0), new(2026, 10, 12, 17, 36, 0), TimeSpan.Zero).WorkedHours);

    [Fact]
    public void Throws_when_end_not_after_start() =>
        Assert.Throws<ArgumentException>(() => new WorkEntry(new(2026, 10, 12, 8, 0, 0), new(2026, 10, 12, 8, 0, 0), TimeSpan.Zero));

    [Fact]
    public void Throws_when_break_longer_than_entry() =>
        Assert.Throws<ArgumentException>(() => new WorkEntry(new(2026, 10, 12, 8, 0, 0), new(2026, 10, 12, 9, 0, 0), TimeSpan.FromHours(2)));

    [Fact]
    public void Throws_when_break_negative() =>
        Assert.Throws<ArgumentException>(() => new WorkEntry(new(2026, 10, 12, 8, 0, 0), new(2026, 10, 12, 9, 0, 0), TimeSpan.FromMinutes(-1)));

    [Fact]
    public void Supports_entry_across_midnight() =>
        Assert.Equal(8.0m, new WorkEntry(new(2026, 10, 12, 22, 0, 0), new(2026, 10, 13, 6, 0, 0), TimeSpan.Zero).WorkedHours);
}
