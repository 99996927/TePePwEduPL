namespace Hr.Rules.Time;

/// <summary>Wpis czasu pracy w czasie lokalnym (ADR-17). Może kończyć się następnego dnia.</summary>
public sealed record WorkEntry
{
    public WorkEntry(DateTime start, DateTime end, TimeSpan breakTime)
    {
        if (end <= start)
        {
            throw new ArgumentException("Koniec wpisu musi być po jego początku.", nameof(end));
        }

        if (breakTime < TimeSpan.Zero || breakTime >= end - start)
        {
            throw new ArgumentException("Przerwa musi być nieujemna i krótsza niż wpis.", nameof(breakTime));
        }

        Start = start;
        End = end;
        Break = breakTime;
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public TimeSpan Break { get; }

    public decimal WorkedHours => (decimal)(End - Start - Break).Ticks / TimeSpan.TicksPerHour;
}
