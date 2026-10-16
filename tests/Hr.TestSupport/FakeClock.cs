using Hr.Core.Abstractions;

namespace Hr.TestSupport;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    public DateTime LocalNow => PolishTime.ToLocal(UtcNow);
}
