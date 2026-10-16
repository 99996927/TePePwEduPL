using System.Text.Json;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hr.Core.Seeding;

/// <summary>Ładuje artykuły Kodeksu pracy z zasobu osadzonego (<c>labour-code.json</c>) do bazy: dodaje brakujące i poprawia zmieniony tekst.</summary>
public static class LabourCodeSeeder
{
    private sealed record Article(string Number, string Text);

    private sealed record Document(IReadOnlyList<Article> Articles);

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        await SeedAsync(scope.ServiceProvider.GetRequiredService<HrDbContext>(), ct);
    }

    public static async Task SeedAsync(HrDbContext db, CancellationToken ct = default)
    {
        var existing = await db.LabourCodeArticles.ToDictionaryAsync(a => a.Number, ct);
        bool changed = false;
        foreach (var article in Load())
        {
            if (!existing.TryGetValue(article.Number, out var row))
            {
                db.LabourCodeArticles.Add(new LabourCodeArticle { Number = article.Number, Text = article.Text });
                changed = true;
            }
            else if (row.Text != article.Text)
            {
                row.Text = article.Text;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private static IReadOnlyList<Article> Load()
    {
        using var stream = typeof(LabourCodeSeeder).Assembly.GetManifestResourceStream("labour-code.json")
            ?? throw new InvalidOperationException("Brak zasobu labour-code.json w Hr.Core.");
        var document = JsonSerializer.Deserialize<Document>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Pusty zasób labour-code.json.");
        return document.Articles;
    }
}
