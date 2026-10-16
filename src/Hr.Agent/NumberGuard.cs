using System.Globalization;
using System.Text.RegularExpressions;

namespace Hr.Agent;

/// <summary>
/// Osłona wyzwania projektu (ADR-04, Q-04): wykrywa w odpowiedzi liczby, których nie ma w wynikach narzędzi ani w pytaniu,
/// czyli takie, które model mógł sam oszacować, zsumować albo przeliczyć. To heurystyka: ostrzega, nie blokuje.
/// </summary>
public static partial class NumberGuard
{
    // Daty (31.10.2026, 2026-10-31): każda część jest porównywana osobno jako liczba całkowita.
    [GeneratedRegex(@"(?<![\d])(\d{4}-\d{1,2}-\d{1,2}|\d{1,2}[./-]\d{1,2}[./-]\d{2,4})(?![\d])")]
    private static partial Regex DatePattern();

    // Liczba z opcjonalnym separatorem tysięcy (spacja) i częścią dziesiętną; nie zaczyna się w środku słowa (np. „Hours50”).
    [GeneratedRegex(@"(?<![\p{L}\d])(\d{1,3}(?:[  ]\d{3})+(?:[.,]\d+)?|\d+(?:[.,]\d+)?)")]
    private static partial Regex NumberPattern();

    // Numer artykułu, paragrafu, ustępu i punktu oraz indeks górny w nawiasach kwadratowych (151[1]) to nie wartości.
    [GeneratedRegex(@"(?:\bart\.?|§|\bust\.?|\bpkt|\blit\.?)\s*\d+[a-z]?(?:\[[0-9a-z]+\])?", RegexOptions.IgnoreCase)]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"^\s*\d+[.)]\s", RegexOptions.Multiline)]
    private static partial Regex ListMarkerPattern();

    public static IReadOnlyList<string> FindUngrounded(string answer, IEnumerable<string> allowedSources)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in allowedSources)
        {
            foreach (var (_, normalized) in Extract(source, ignoreReferences: false))
            {
                allowed.Add(normalized);
            }
        }

        var ungrounded = new List<string>();
        foreach (var (raw, normalized) in Extract(answer, ignoreReferences: true))
        {
            if (!allowed.Contains(normalized) && !ungrounded.Contains(raw))
            {
                ungrounded.Add(raw);
            }
        }

        return ungrounded;
    }

    private static IEnumerable<(string Raw, string Normalized)> Extract(string text, bool ignoreReferences)
    {
        if (ignoreReferences)
        {
            text = ListMarkerPattern().Replace(ReferencePattern().Replace(text, " "), " ");
        }

        // Daty: części jako osobne liczby, a sam tekst daty znika z dalszej analizy.
        var parts = new List<(string, string)>();
        text = DatePattern().Replace(text, match =>
        {
            foreach (Match part in Regex.Matches(match.Value, @"\d+"))
            {
                parts.Add((part.Value, Normalize(part.Value)));
            }

            return " ";
        });

        foreach (Match m in NumberPattern().Matches(text))
        {
            parts.Add((m.Value, Normalize(m.Value)));
        }

        return parts;
    }

    private static string Normalize(string number)
    {
        string text = number.Replace(" ", string.Empty).Replace(" ", string.Empty).Replace(',', '.');
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return text;
        }

        return value.ToString("0.############################", CultureInfo.InvariantCulture);
    }
}
