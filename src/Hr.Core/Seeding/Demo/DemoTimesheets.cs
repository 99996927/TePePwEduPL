using Hr.Core.Domain;
using Hr.Rules.Calendar;

namespace Hr.Core.Seeding.Demo;

/// <summary>Wpisy czasu pracy demo: standardowe zmiany od początku okna do kotwicy oraz scenariusze Anny i Jana.</summary>
public static class DemoTimesheets
{
    private const int OvertimeEveryNthDay = 20;
    private const int HolidayWeekdaysForLongerShift = 3;
    private static readonly TimeSpan HalfHour = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan[] Starts =
    {
        new(7, 15, 0), new(7, 30, 0), new(7, 45, 0), new(8, 0, 0), new(8, 15, 0), new(8, 30, 0), new(8, 45, 0),
    };

    /// <summary>Indeksy (wśród osób wygenerowanych, bez kont stałych) osób z nadgodzinami.</summary>
    private static readonly int[] OvertimeGeneratedIndexes = { 3, 9, 14, 20 };

    public static IReadOnlyList<WorkEntryRecord> Build(
        DemoOptions o, IReadOnlyList<DemoPerson> people, IReadOnlyList<LeaveRequest> leave, WorkCalendar calendar)
    {
        var result = new List<WorkEntryRecord>();
        var generated = people.Where(p => !p.IsNamed).ToList();
        var overtimeLogins = OvertimeGeneratedIndexes
            .Where(i => i < generated.Count)
            .Select(i => generated[i].Login)
            .ToHashSet();

        foreach (var p in people)
        {
            var start = Starts[new Random(DemoIds.SeedFor(o.Seed, p.Login)).Next(Starts.Length)];
            var approved = leave
                .Where(l => l.EmployeeId == p.EmployeeId && l.Status == LeaveStatus.Approved)
                .ToList();
            var withOvertime = overtimeLogins.Contains(p.Login);
            var workedDays = 0;

            for (var d = o.WindowStart; d <= o.AnchorDate; d = d.AddDays(1))
            {
                if (!calendar.IsWorkingDay(d) || approved.Any(l => l.From <= d && d <= l.To))
                {
                    continue;
                }

                workedDays++;
                var shift = ShiftFor(o, calendar, p, d, start, withOvertime && workedDays % OvertimeEveryNthDay == 0);
                result.Add(Entry(o, p, d, shift.Start, shift.End, shift.Break));
            }

            if (p.Login == "anna.nowak")
            {
                var saturday = DemoScenarios.AnnaWeeklyRestSaturday(o.AnchorDate);
                if (saturday >= o.WindowStart && saturday <= o.AnchorDate)
                {
                    result.Add(Entry(o, p, saturday, new TimeSpan(14, 0, 0), new TimeSpan(20, 0, 0), TimeSpan.Zero));
                }
            }
        }

        return result;
    }

    private static (TimeSpan Start, TimeSpan End, TimeSpan Break) ShiftFor(
        DemoOptions o, WorkCalendar calendar, DemoPerson p, DateOnly d, TimeSpan start, bool overtimeDay)
    {
        if (p.Login == "anna.nowak")
        {
            var tuesday = DemoScenarios.AnnaDailyRestTuesday(o.AnchorDate);
            if (d == tuesday)
            {
                return (new TimeSpan(14, 0, 0), new TimeSpan(22, 30, 0), HalfHour);
            }

            if (d == tuesday.AddDays(1))
            {
                return (new TimeSpan(7, 30, 0), new TimeSpan(15, 30, 0), HalfHour);
            }
        }

        if (p.Login == "jan.kowalski")
        {
            var period = DemoScenarios.JanOvertimePeriod(o.AnchorDate);
            if (d >= period.From && d <= period.To)
            {
                // Okres z wieloma świętami (np. Boże Narodzenie) ma mniej dni roboczych, więc dzień jest o godzinę dłuższy,
                // żeby średnia tygodniowa nadal przekraczała 48 h. Dla domyślnej kotwicy zmiana to 8:00-18:30.
                var end = HolidayWeekdays(calendar, period.From, period.To) >= HolidayWeekdaysForLongerShift
                    ? new TimeSpan(19, 30, 0)
                    : new TimeSpan(18, 30, 0);
                return (new TimeSpan(8, 0, 0), end, HalfHour);
            }
        }

        var hours = overtimeDay ? 9 : 8;
        return (start, start + TimeSpan.FromHours(hours) + HalfHour, HalfHour);
    }

    private static int HolidayWeekdays(WorkCalendar calendar, DateOnly from, DateOnly to)
    {
        var count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !calendar.IsWorkingDay(d))
            {
                count++;
            }
        }

        return count;
    }

    private static WorkEntryRecord Entry(
        DemoOptions o, DemoPerson p, DateOnly d, TimeSpan start, TimeSpan end, TimeSpan breakTime) => new()
    {
        Id = DemoIds.For(o.Seed, $"work:{p.Login}:{d:yyyyMMdd}:1"),
        EmployeeId = p.EmployeeId,
        Start = d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified) + start,
        End = d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified) + end,
        Break = breakTime,
    };
}
