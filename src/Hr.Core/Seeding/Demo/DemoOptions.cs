using System.Globalization;
using Hr.Rules.Calendar;

namespace Hr.Core.Seeding.Demo;

public enum DemoProfile { Full, Accounts }

public sealed record DemoOptions(int Seed, DateOnly AnchorDate, DemoProfile Profile)
{
    public static DemoOptions Default { get; } =
        new(20261016, new DateOnly(2026, 10, 16), DemoProfile.Full);

    public DateOnly WindowStart =>
        new DateOnly(AnchorDate.Year, AnchorDate.Month, 1).AddMonths(-6);

    public IReadOnlyList<(int Year, int Month)> PayrollMonths =>
        Enumerable.Range(0, 6)
            .Select(i => WindowStart.AddMonths(i))
            .Select(d => (d.Year, d.Month))
            .ToList();

    public static DemoOptions Parse(string? anchorDate, string? seed, string? profile, WorkCalendar calendar)
    {
        var anchor = Default.AnchorDate;
        if (!string.IsNullOrWhiteSpace(anchorDate))
        {
            if (!DateOnly.TryParseExact(anchorDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out anchor))
                throw new InvalidOperationException(
                    $"Hr:DemoAnchorDate: nieprawidłowa data '{anchorDate}', oczekiwany format yyyy-MM-dd.");
            if (!calendar.IsWorkingDay(anchor))
                throw new InvalidOperationException(
                    $"Hr:DemoAnchorDate: {anchor:yyyy-MM-dd} musi być dniem roboczym (dzień roboczy to nie weekend ani święto).");
        }

        var seedValue = Default.Seed;
        if (!string.IsNullOrWhiteSpace(seed)
            && !int.TryParse(seed.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seedValue))
            throw new InvalidOperationException($"Hr:DemoSeed: '{seed}' nie jest liczbą całkowitą.");

        var profileValue = Default.Profile;
        if (!string.IsNullOrWhiteSpace(profile)
            && (!Enum.TryParse(profile.Trim(), true, out profileValue) || !Enum.IsDefined(profileValue)))
            throw new InvalidOperationException(
                $"Hr:DemoProfile: nieznany profil '{profile}', dozwolone: Full, Accounts.");

        return new DemoOptions(seedValue, anchor, profileValue);
    }
}
