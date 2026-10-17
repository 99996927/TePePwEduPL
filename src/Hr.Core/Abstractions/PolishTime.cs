namespace Hr.Core.Abstractions;

/// <summary>Czas lokalny aplikacji (Europe/Warsaw): wpisy czasu pracy są zapisywane jako czas lokalny (ADR-17).</summary>
public static class PolishTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>Czas lokalny na UTC. Czas z luki wiosennej zmiany czasu rzuca <see cref="ArgumentException"/>; czas niejednoznaczny jesienią jest rozstrzygany jako czas standardowy.</summary>
    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
}
