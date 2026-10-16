namespace Hr.Core.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }

    /// <summary>Bieżący czas lokalny (Europe/Warsaw); do porównań z wpisami czasu pracy i wyznaczania „dziś”.</summary>
    DateTime LocalNow { get; }
}
