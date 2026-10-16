using Hr.Rules.Calendar;

namespace Hr.Rules.Time;

/// <summary>Wymiar czasu pracy w okresie rozliczeniowym (art. 130 Kodeksu pracy, reguła KP-130-1).</summary>
public static class WorkingTimeNorm
{
    public const string RuleId = "KP-130-1";

    public static decimal HoursForPeriod(DateOnly start, DateOnly endInclusive, WorkCalendar calendar, decimal absenceHours = 0m)
    {
        if (endInclusive < start)
        {
            throw new ArgumentException("Koniec okresu nie może być przed jego początkiem.", nameof(endInclusive));
        }

        if (absenceHours < 0m)
        {
            throw new ArgumentException("Nieobecność nie może być ujemna.", nameof(absenceHours));
        }

        int totalDays = endInclusive.DayNumber - start.DayNumber + 1;
        int fullWeeks = totalDays / 7;
        var remainingStart = start.AddDays(fullWeeks * 7);

        decimal hours = 40m * fullWeeks;
        for (var d = remainingStart; d <= endInclusive; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                hours += 8m;
            }
        }

        for (var d = start; d <= endInclusive; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Sunday && calendar.IsHoliday(d))
            {
                hours -= 8m;
            }
        }

        return hours - absenceHours;
    }
}
