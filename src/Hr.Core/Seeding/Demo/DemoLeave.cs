using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Rules.Calendar;

namespace Hr.Core.Seeding.Demo;

/// <summary>Wnioski urlopowe demo: scenariusze Anny i Jana oraz krótkie zatwierdzone urlopy pozostałych osób.</summary>
public static class DemoLeave
{
    private const int WindowDays = 120;
    private const int Attempts = 200;

    public static IReadOnlyList<LeaveRequest> Build(DemoOptions o, IReadOnlyList<DemoPerson> people, WorkCalendar calendar)
    {
        var result = new List<LeaveRequest>();
        var hr = people.Single(p => p.IsNamed && p.Role == Roles.HR);

        foreach (var p in people)
        {
            var decider = Decider(people, p, hr);
            switch (p.Login)
            {
                case "anna.nowak":
                    result.Add(Scenario(o, calendar, p, 1, DemoScenarios.AnnaLeaveRealized(o.AnchorDate), LeaveStatus.Approved, decider, null));
                    result.Add(Scenario(o, calendar, p, 2, DemoScenarios.AnnaLeaveFuture(o.AnchorDate), LeaveStatus.Approved, decider, null));
                    result.Add(Scenario(o, calendar, p, 3, DemoScenarios.AnnaLeavePending(o.AnchorDate), LeaveStatus.Pending, null, null));
                    break;
                case "jan.kowalski":
                    result.Add(Scenario(o, calendar, p, 1, DemoScenarios.JanLeaveRejected(o.AnchorDate), LeaveStatus.Rejected, decider, DemoScenarios.RejectionReason));
                    result.Add(Scenario(o, calendar, p, 2, DemoScenarios.JanLeaveCancelled(o.AnchorDate), LeaveStatus.Cancelled, null, null));
                    break;
                default:
                    result.AddRange(PastLeaves(o, calendar, p, decider));
                    break;
            }
        }

        return result;
    }

    private static Guid Decider(IReadOnlyList<DemoPerson> people, DemoPerson p, DemoPerson hr)
    {
        if (p.Role == Roles.Employee && p.TeamName != DemoPeople.TeamHr)
        {
            var manager = people.FirstOrDefault(x => x.Role == Roles.Manager && x.TeamName == p.TeamName);
            if (manager is not null)
            {
                return manager.EmployeeId;
            }
        }

        return hr.EmployeeId;
    }

    private static LeaveRequest Scenario(
        DemoOptions o, WorkCalendar calendar, DemoPerson p, int n, (DateOnly From, DateOnly To) range,
        LeaveStatus status, Guid? decidedBy, string? reason) =>
        Create(o, calendar, p, n, range.From, range.To, status, decidedBy, reason);

    private static LeaveRequest Create(
        DemoOptions o, WorkCalendar calendar, DemoPerson p, int n, DateOnly from, DateOnly to,
        LeaveStatus status, Guid? decidedBy, string? reason)
    {
        var local = from.AddDays(-14).ToDateTime(new TimeOnly(8, 0), DateTimeKind.Unspecified);
        return new LeaveRequest
        {
            Id = DemoIds.For(o.Seed, $"leave:{p.Login}:{n}"),
            EmployeeId = p.EmployeeId,
            From = from,
            To = to,
            Days = calendar.CountWorkingDays(from, to),
            Status = status,
            DecidedByEmployeeId = decidedBy,
            DecisionReason = reason,
            CreatedAtUtc = PolishTime.ToUtc(local),
        };
    }

    private static IEnumerable<LeaveRequest> PastLeaves(DemoOptions o, WorkCalendar calendar, DemoPerson p, Guid decider)
    {
        var rng = new Random(o.Seed ^ StableHash(p.Login));
        var count = rng.Next(1, 3);
        var windowStart = o.AnchorDate.AddDays(-WindowDays);
        var taken = new List<(DateOnly From, DateOnly To)>();

        for (var n = 1; n <= count; n++)
        {
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                var from = windowStart.AddDays(rng.Next(0, WindowDays - 14));
                if (!calendar.IsWorkingDay(from) || from < p.HireDate)
                {
                    continue;
                }

                var length = rng.Next(3, 11);
                var to = from;
                var days = 1;
                while (days < length)
                {
                    to = to.AddDays(1);
                    if (calendar.IsWorkingDay(to))
                    {
                        days++;
                    }
                }

                if (to >= o.AnchorDate || to.Year != from.Year || taken.Any(t => from <= t.To && t.From <= to))
                {
                    continue;
                }

                taken.Add((from, to));
                yield return Create(o, calendar, p, n, from, to, LeaveStatus.Approved, decider, null);
                break;
            }
        }
    }

    // string.GetHashCode jest losowy między uruchomieniami, więc używamy własnego skrótu.
    private static int StableHash(string s)
    {
        var h = 17;
        foreach (var c in s)
        {
            h = unchecked(h * 31 + c);
        }

        return h;
    }
}
