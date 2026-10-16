using Hr.Rules.Time;

namespace Hr.Rules.Rest;

public sealed record AverageHoursResult(decimal AverageWeeklyHours, bool ExceedsLimit, string RuleId);

/// <summary>Przeciętny tygodniowy czas pracy łącznie z nadgodzinami, limit 48 h (art. 131 § 1, KP-131-1).</summary>
public static class WeeklyAverageRules
{
    private const decimal LimitHours = 48m;

    public static AverageHoursResult Check(IReadOnlyList<WorkEntry> entries, DateOnly start, DateOnly endInclusive)
    {
        if (endInclusive < start)
        {
            throw new ArgumentException("Koniec okresu nie może być przed jego początkiem.", nameof(endInclusive));
        }

        decimal total = entries
            .Where(e =>
            {
                var date = DateOnly.FromDateTime(e.Start);
                return date >= start && date <= endInclusive;
            })
            .Sum(e => e.WorkedHours);

        decimal weeks = (endInclusive.DayNumber - start.DayNumber + 1) / 7m;
        decimal average = total / weeks;
        return new AverageHoursResult(average, average > LimitHours, "KP-131-1");
    }
}
