using Hr.Core.Abstractions;
using Hr.Core.Domain;

namespace Hr.Core.Seeding.Demo;

/// <summary>
/// Szkice korekt demo (UC-26): jeden oczekujący szkic Piotra dla wpisu Anny z dnia naruszenia odpoczynku dobowego
/// (<see cref="DemoScenarios.AnnaDailyRestTuesday"/>, 14:00-22:30), proponujący przesunięcie zmiany na 12:00-20:30.
/// Przy kolejnym wpisie Anny o 7:30 daje to równo 11 h odpoczynku, więc korekta usuwa naruszenie dla każdej kotwicy.
/// </summary>
public static class DemoCorrectionDrafts
{
    public static IReadOnlyList<CorrectionDraft> Build(DemoOptions o, IReadOnlyList<DemoPerson> people, IReadOnlyList<WorkEntryRecord> entries)
    {
        var anna = people.Single(p => p.Login == "anna.nowak");
        var piotr = people.Single(p => p.Login == "piotr.zielinski");
        var day = DemoScenarios.AnnaDailyRestTuesday(o.AnchorDate);
        var entry = entries.FirstOrDefault(e => e.EmployeeId == anna.EmployeeId && DateOnly.FromDateTime(e.Start) == day);
        if (entry is null)
        {
            return new List<CorrectionDraft>();
        }

        return new List<CorrectionDraft>
        {
            new()
            {
                Id = DemoIds.For(o.Seed, "draft:anna:1"),
                EmployeeId = anna.EmployeeId,
                WorkEntryId = entry.Id,
                ProposedStart = day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified),
                ProposedEnd = day.ToDateTime(new TimeOnly(20, 30), DateTimeKind.Unspecified),
                ProposedBreak = TimeSpan.FromMinutes(30),
                Reason = "Naruszenie odpoczynku dobowego (art. 132 § 1 Kodeksu pracy): po zmianie do 22:30 kolejny dzień zaczyna się o 7:30. "
                    + "Proponowane przesunięcie zmiany na 12:00-20:30 zapewnia co najmniej 11 godzin odpoczynku.",
                Status = CorrectionDraftStatus.Pending,
                CreatedByUserId = piotr.UserId,
                ConversationId = null,
                CreatedAtUtc = PolishTime.ToUtc(o.AnchorDate.AddDays(-8).ToDateTime(new TimeOnly(10, 0), DateTimeKind.Unspecified)),
            },
        };
    }
}
