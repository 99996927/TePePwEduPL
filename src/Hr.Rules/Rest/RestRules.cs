using Hr.Rules.Time;

namespace Hr.Rules.Rest;

public sealed record RestViolation(string RuleId, DateTime From, DateTime To, decimal RestHours, decimal RequiredHours);

/// <summary>
/// Odpoczynek dobowy 11 h (art. 132 § 1, KP-132-1) i tygodniowy 35 h (art. 133 § 1, KP-133-1).
/// Uproszczenia opisane w ADR-22: dobowy sprawdzany między wpisami o różnych datach rozpoczęcia,
/// tygodniowy w tygodniu pon.–niedz. z lukami obciętymi do granic tygodnia.
/// </summary>
public static class RestRules
{
    private const decimal DailyRequiredHours = 11m;
    private const decimal WeeklyRequiredHours = 35m;

    public static IReadOnlyList<RestViolation> Check(IReadOnlyList<WorkEntry> entries)
    {
        var sorted = WorkEntries.SortedNonOverlapping(entries);
        var violations = new List<RestViolation>();
        CheckDaily(sorted, violations);
        CheckWeekly(sorted, violations);
        return violations;
    }

    private static void CheckDaily(List<WorkEntry> sorted, List<RestViolation> violations)
    {
        for (int i = 1; i < sorted.Count; i++)
        {
            var prev = sorted[i - 1];
            var next = sorted[i];
            if (prev.Start.Date == next.Start.Date)
            {
                continue;
            }

            decimal rest = Hours(next.Start - prev.End);
            if (rest < DailyRequiredHours)
            {
                violations.Add(new RestViolation("KP-132-1", prev.End, next.Start, rest, DailyRequiredHours));
            }
        }
    }

    private static void CheckWeekly(List<WorkEntry> sorted, List<RestViolation> violations)
    {
        foreach (var week in sorted.GroupBy(e => WeekStart(e.Start)).OrderBy(g => g.Key))
        {
            var weekStart = week.Key;
            var weekEnd = weekStart.AddDays(7);
            var busy = sorted
                .Where(e => e.End > weekStart && e.Start < weekEnd)
                .Select(e => (Start: e.Start < weekStart ? weekStart : e.Start, End: e.End > weekEnd ? weekEnd : e.End))
                .ToList();

            var cursor = weekStart;
            (DateTime From, DateTime To) longest = (weekStart, weekStart);
            foreach (var (start, end) in busy)
            {
                if (start - cursor > longest.To - longest.From)
                {
                    longest = (cursor, start);
                }

                cursor = end > cursor ? end : cursor;
            }

            if (weekEnd - cursor > longest.To - longest.From)
            {
                longest = (cursor, weekEnd);
            }

            decimal rest = Hours(longest.To - longest.From);
            if (rest < WeeklyRequiredHours)
            {
                violations.Add(new RestViolation("KP-133-1", longest.From, longest.To, rest, WeeklyRequiredHours));
            }
        }
    }

    private static DateTime WeekStart(DateTime t)
    {
        int offset = ((int)t.DayOfWeek + 6) % 7;
        return t.Date.AddDays(-offset);
    }

    private static decimal Hours(TimeSpan span) => (decimal)span.Ticks / TimeSpan.TicksPerHour;
}
