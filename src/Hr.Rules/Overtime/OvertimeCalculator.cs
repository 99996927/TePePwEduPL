using Hr.Rules.Calendar;
using Hr.Rules.Time;

namespace Hr.Rules.Overtime;

public sealed record OvertimeResult(decimal Hours50, decimal Hours100, IReadOnlyList<string> RuleIds)
{
    public decimal Total => Hours50 + Hours100;

    public bool Equals(OvertimeResult? other) =>
        other is not null && Hours50 == other.Hours50 && Hours100 == other.Hours100 && RuleIds.SequenceEqual(other.RuleIds);

    public override int GetHashCode() => HashCode.Combine(Hours50, Hours100, RuleIds.Count);
}

/// <summary>
/// Nadgodziny w ujęciu dobowym (ADR-22): dzień to data rozpoczęcia wpisu. W dzień roboczy nadgodzinami są godziny
/// powyżej 8 h w dobie (ostatnie godziny dnia), w dni nieroboczne cała praca. Dodatek 100% za porę nocną,
/// niedziele i święta (art. 151¹ § 1 pkt 1), pozostałe 50%.
/// </summary>
public static class OvertimeCalculator
{
    private const int DailyNormMinutes = 8 * 60;

    public static OvertimeResult Calculate(IReadOnlyList<WorkEntry> entries, WorkCalendar calendar, NightWindow night)
    {
        int minutes50 = 0;
        int minutes100 = 0;

        foreach (var day in entries.GroupBy(e => DateOnly.FromDateTime(e.Start)))
        {
            var date = day.Key;
            var ordered = day.OrderByDescending(e => e.Start).ToList();
            int worked = ordered.Sum(e => WorkedMinutes(e));
            int remaining = calendar.IsWorkingDay(date) ? Math.Max(0, worked - DailyNormMinutes) : worked;
            bool premiumDay = date.DayOfWeek == DayOfWeek.Sunday || calendar.IsHoliday(date);

            foreach (var entry in ordered)
            {
                if (remaining == 0)
                {
                    break;
                }

                int take = Math.Min(remaining, WorkedMinutes(entry));
                remaining -= take;

                for (int i = 1; i <= take; i++)
                {
                    var minuteStart = entry.End.AddMinutes(-i);
                    if (premiumDay || night.Contains(TimeOnly.FromDateTime(minuteStart)))
                    {
                        minutes100++;
                    }
                    else
                    {
                        minutes50++;
                    }
                }
            }
        }

        var ruleIds = minutes50 + minutes100 > 0
            ? new[] { "KP-151-1", "KP-151-1-1" }
            : Array.Empty<string>();
        return new OvertimeResult(minutes50 / 60m, minutes100 / 60m, ruleIds);
    }

    private static int WorkedMinutes(WorkEntry e) => (int)(e.End - e.Start - e.Break).TotalMinutes;
}
