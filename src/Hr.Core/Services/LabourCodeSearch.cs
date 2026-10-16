using System.Text.RegularExpressions;
using Hr.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Services;

public sealed record ArticleHit(string Number, string Citation, string Text);

/// <summary>Wyszukiwanie przepisów Kodeksu pracy (UC-23): po numerze artykułu albo po słowach kluczowych. Dane publiczne, bez zakresu użytkownika.</summary>
public sealed partial class LabourCodeSearch(HrDbContext db)
{
    private const string Source = "Dz.U. 2026 poz. 1245";
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase) { "art", "ust", "pkt", "oraz", "dla", "jak", "czy", "nie", "lub" };

    [GeneratedRegex(@"^\s*(?:art\.?\s*)?(?<n>\d+(?:\[[0-9a-z]+\])?)\s*\.?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ArticleReference();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();

    public async Task<IReadOnlyList<ArticleHit>> SearchAsync(string query, int max = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || max < 1)
        {
            return [];
        }

        var reference = ArticleReference().Match(query);
        if (reference.Success)
        {
            string number = reference.Groups["n"].Value;
            var article = await db.LabourCodeArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Number == number, ct);
            return article is null ? [] : [Hit(article.Number, article.Text)];
        }

        var stems = Word().Matches(query).Select(m => m.Value)
            .Where(w => (w.Length >= 3 || (w.Length == 2 && w.All(char.IsDigit))) && !StopWords.Contains(w))
            .Select(Stem).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (stems.Count == 0)
        {
            return [];
        }

        var likeOptions = stems.Select(s => $"%{EscapeLike(s)}%").ToList();
        var candidates = await db.LabourCodeArticles.AsNoTracking()
            .Where(a => likeOptions.Any(p => EF.Functions.Like(a.Text, p, "\\")))
            .ToListAsync(ct);
        if (candidates.Count == 0)
        {
            return [];
        }

        int total = await db.LabourCodeArticles.CountAsync(ct);
        double averageLength = await db.LabourCodeArticles.AverageAsync(a => (double)a.Text.Length, ct);
        var counts = candidates.ToDictionary(a => a.Number, a => stems.Select(stem => Count(a.Text, stem)).ToArray());
        var documentFrequency = stems.Select((_, i) => counts.Values.Count(c => c[i] > 0)).ToArray();

        int required = (stems.Count + 1) / 2;
        return candidates
            .Where(a => counts[a.Number].Count(c => c > 0) >= required)
            .Select(a => (Article: a, Score: Bm25(counts[a.Number], documentFrequency, total, a.Text.Length, averageLength)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Article.Number, StringComparer.Ordinal)
            .Take(max)
            .Select(x => Hit(x.Article.Number, x.Article.Text))
            .ToList();
    }

    private static ArticleHit Hit(string number, string text) => new(number, $"art. {number} Kodeksu pracy ({Source})", text);

    /// <summary>Obcina końcówkę fleksyjną, by „odpoczynek” znajdował „odpoczynku”.</summary>
    private static string Stem(string word) => word.Length >= 8 ? word[..^3] : word.Length >= 6 ? word[..^2] : word;

    private static int Count(string text, string stem)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(stem, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += stem.Length;
        }

        return count;
    }

    /// <summary>Okapi BM25: rzadkie słowa i krótkie artykuły dostają wyższą ocenę, więc „11 godzin” znajduje art. 132, a nie długie artykuły z wieloma „godzin”.</summary>
    private static double Bm25(int[] termCounts, int[] documentFrequency, int total, int length, double averageLength)
    {
        const double k1 = 1.2, b = 0.75;
        double score = 0;
        for (int i = 0; i < termCounts.Length; i++)
        {
            int tf = termCounts[i];
            if (tf == 0)
            {
                continue;
            }

            double idf = Math.Log(1 + ((total - documentFrequency[i] + 0.5) / (documentFrequency[i] + 0.5)));
            score += idf * tf * (k1 + 1) / (tf + (k1 * ((1 - b) + (b * length / averageLength))));
        }

        return score;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
