namespace Hr.Core.Seeding.Demo;

/// <summary>
/// Daty scenariuszy demo liczone względem miesiąca kotwicy M (nie względem stałych dat),
/// dzięki czemu scenariusze zachowują sens przy innej kotwicy. Dla domyślnej kotwicy 2026-10-16 daty pilnuje test.
/// Urlopy: realized = 2. poniedziałek M-3, rejected = 1. poniedziałek M-2, cancelled = 1. poniedziałek M,
/// pending = 1. poniedziałek M+1, future = 1. poniedziałek M+2 (każdy to poniedziałek-piątek).
/// </summary>
public static class DemoScenarios
{
    public const string RejectionReason = "Zbyt wielu pracowników na urlopie w tym tygodniu.";

    /// <summary>Ostatni wtorek miesiąca poprzedzającego miesiąc kotwicy.</summary>
    public static DateOnly AnnaDailyRestTuesday(DateOnly a)
    {
        var d = LastDayOfPreviousMonth(a);
        while (d.DayOfWeek != DayOfWeek.Tuesday)
        {
            d = d.AddDays(-1);
        }

        return d;
    }

    /// <summary>Druga sobota miesiąca poprzedzającego miesiąc kotwicy.</summary>
    public static DateOnly AnnaWeeklyRestSaturday(DateOnly a) =>
        NthWeekday(MonthStart(a).AddMonths(-1), DayOfWeek.Saturday, 2);

    /// <summary>Od 1. dnia miesiąca M-4 do ostatniego dnia miesiąca M-1.</summary>
    public static (DateOnly From, DateOnly To) JanOvertimePeriod(DateOnly a) =>
        (MonthStart(a).AddMonths(-4), LastDayOfPreviousMonth(a));

    public static (DateOnly From, DateOnly To) AnnaLeaveRealized(DateOnly a) => Week(MonthStart(a).AddMonths(-3), 2);

    public static (DateOnly From, DateOnly To) AnnaLeaveFuture(DateOnly a) => Week(MonthStart(a).AddMonths(2), 1);

    public static (DateOnly From, DateOnly To) AnnaLeavePending(DateOnly a) => Week(MonthStart(a).AddMonths(1), 1);

    public static (DateOnly From, DateOnly To) JanLeaveRejected(DateOnly a) => Week(MonthStart(a).AddMonths(-2), 1);

    public static (DateOnly From, DateOnly To) JanLeaveCancelled(DateOnly a) => Week(MonthStart(a), 1);

    private static DateOnly MonthStart(DateOnly a) => new(a.Year, a.Month, 1);

    private static DateOnly LastDayOfPreviousMonth(DateOnly a) => MonthStart(a).AddDays(-1);

    private static (DateOnly From, DateOnly To) Week(DateOnly monthStart, int nthMonday)
    {
        var monday = NthWeekday(monthStart, DayOfWeek.Monday, nthMonday);
        return (monday, monday.AddDays(4));
    }

    private static DateOnly NthWeekday(DateOnly monthStart, DayOfWeek day, int n)
    {
        var d = monthStart;
        while (d.DayOfWeek != day)
        {
            d = d.AddDays(1);
        }

        return d.AddDays(7 * (n - 1));
    }
}
