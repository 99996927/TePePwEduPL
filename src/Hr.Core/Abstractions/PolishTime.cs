namespace Hr.Core.Abstractions;

/// <summary>Czas lokalny aplikacji (Europe/Warsaw): wpisy czasu pracy są zapisywane jako czas lokalny (ADR-17).</summary>
public static class PolishTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
