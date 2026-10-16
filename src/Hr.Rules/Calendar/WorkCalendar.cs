namespace Hr.Rules.Calendar;

public sealed class WorkCalendar
{
    public bool IsHoliday(DateOnly d) => PolishHolidays.ForYear(d.Year).Contains(d);

    /// <summary>Dzień roboczy: poniedziałek–piątek, niebędący świętem.</summary>
    public bool IsWorkingDay(DateOnly d) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsHoliday(d);

    public int CountWorkingDays(DateOnly from, DateOnly toInclusive)
    {
        int count = 0;
        for (var d = from; d <= toInclusive; d = d.AddDays(1))
        {
            if (IsWorkingDay(d))
            {
                count++;
            }
        }

        return count;
    }
}
