namespace Hr.Rules.Calendar;

/// <summary>Dni wolne od pracy wg ustawy z 18.01.1951 (Dz.U. 2025 poz. 296, art. 1), bez niedziel jako takich.</summary>
public static class PolishHolidays
{
    public static IReadOnlyList<DateOnly> ForYear(int year)
    {
        var easter = EasterSunday(year);
        var days = new List<DateOnly>
        {
            new(year, 1, 1),
            new(year, 1, 6),
            easter,
            easter.AddDays(1),
            new(year, 5, 1),
            new(year, 5, 3),
            easter.AddDays(49),
            easter.AddDays(60),
            new(year, 8, 15),
            new(year, 11, 1),
            new(year, 11, 11),
            new(year, 12, 25),
            new(year, 12, 26),
        };

        if (year >= 2025)
        {
            days.Add(new DateOnly(year, 12, 24));
        }

        days.Sort();
        return days;
    }

    // Algorytm Meeusa/Jonesa/Butchera (kalendarz gregoriański).
    private static DateOnly EasterSunday(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
