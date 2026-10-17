using Hr.Rules.Calendar;

namespace Hr.Core.Seeding.Demo;

/// <summary>Składa moduły danych demo w jeden <see cref="DemoDataSet"/>: osoby, urlopy, wpisy, zmiany płacowe, rozmowy, szkice korekt.</summary>
public static class DemoDataGenerator
{
    public static DemoDataSet Build(DemoOptions options, WorkCalendar calendar)
    {
        var (teams, people) = DemoPeople.Build(options);
        if (options.Profile == DemoProfile.Accounts)
        {
            return new DemoDataSet { Teams = teams, People = people };
        }

        var leave = DemoLeave.Build(options, people, calendar);
        var entries = DemoTimesheets.Build(options, people, leave, calendar);
        var drafts = DemoCorrectionDrafts.Build(options, people, entries);
        var changes = DemoPayrollChanges.Build(options, people);
        var conversations = DemoConversations.Build(options, people, leave);

        return new DemoDataSet
        {
            Teams = teams,
            People = people,
            LeaveRequests = leave,
            WorkEntries = entries,
            PayrollChanges = changes,
            Conversations = conversations,
            CorrectionDrafts = drafts,
        };
    }
}
