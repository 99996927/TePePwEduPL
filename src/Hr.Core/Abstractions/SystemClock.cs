namespace Hr.Core.Abstractions;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => PolishTime.ToLocal(UtcNow);
}
