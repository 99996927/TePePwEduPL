using Hr.Rules.Calendar;

namespace Hr.Rules.Leave;

public sealed record LeaveCheck(int Days, bool Sufficient, int RemainingAfter, string RuleId);

public static class LeaveRequestRules
{
    public static LeaveCheck Evaluate(int entitlementDays, int usedDays, DateOnly from, DateOnly to, WorkCalendar calendar)
    {
        if (to < from)
        {
            throw new ArgumentException("Data końcowa nie może być wcześniejsza niż początkowa.", nameof(to));
        }

        int days = calendar.CountWorkingDays(from, to);
        int remaining = entitlementDays - usedDays - days;
        return new LeaveCheck(days, remaining >= 0, remaining, "KP-154-1");
    }
}
