namespace Hr.Api.Dtos;

public static class DateTimeExtensions
{
    /// <summary>Znacznik UTC dla czasów odczytanych z bazy (kolumny datetime2 tracą Kind), by JSON kończył się literą Z.</summary>
    public static DateTime AsUtc(this DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
