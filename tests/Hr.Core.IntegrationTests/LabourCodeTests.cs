using Hr.Core.Domain;
using Hr.Core.Seeding;
using Hr.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class LabourCodeTests(MsSqlFixture fixture)
{
    private async Task<(Hr.Core.Persistence.HrDbContext Db, LabourCodeSearch Search)> SeededAsync()
    {
        var db = await fixture.NewContextAsync();
        await LabourCodeSeeder.SeedAsync(db);
        return (db, new LabourCodeSearch(db));
    }

    [Fact]
    public async Task UC23_Seeder_loads_all_articles_and_is_idempotent()
    {
        var (db, _) = await SeededAsync();
        await using var _db = db;
        int first = await db.LabourCodeArticles.CountAsync();

        await LabourCodeSeeder.SeedAsync(db);

        Assert.True(first > 400, $"Załadowano tylko {first} artykułów.");
        Assert.Equal(first, await db.LabourCodeArticles.CountAsync());
    }

    [Fact]
    public async Task Seeder_updates_changed_text_and_restores_missing_articles()
    {
        var (db, _) = await SeededAsync();
        await using var _db = db;
        var article = await db.LabourCodeArticles.SingleAsync(a => a.Number == "131");
        string original = article.Text;
        article.Text = "zmieniony";
        db.LabourCodeArticles.Remove(await db.LabourCodeArticles.SingleAsync(a => a.Number == "132"));
        await db.SaveChangesAsync();

        await LabourCodeSeeder.SeedAsync(db);

        Assert.Equal(original, (await db.LabourCodeArticles.AsNoTracking().SingleAsync(a => a.Number == "131")).Text);
        Assert.True(await db.LabourCodeArticles.AnyAsync(a => a.Number == "132"));
    }

    [Theory]
    [InlineData("art. 131")]
    [InlineData("131")]
    [InlineData("Art 131")]
    public async Task UC23_Search_by_article_number_returns_that_article_first(string query)
    {
        var (db, search) = await SeededAsync();
        await using var _db = db;

        var hits = await search.SearchAsync(query);

        Assert.Equal("131", hits[0].Number);
        Assert.Contains("48 godzin", hits[0].Text);
        Assert.Equal("art. 131 Kodeksu pracy (Dz.U. 2026 poz. 1245)", hits[0].Citation);
    }

    [Fact]
    public async Task UC23_Search_by_article_number_with_superscript()
    {
        var (db, search) = await SeededAsync();
        await using var _db = db;

        var hits = await search.SearchAsync("art. 151[1]");

        Assert.Equal("151[1]", hits[0].Number);
        Assert.Single(hits);
    }

    [Fact]
    public async Task UC23_Search_by_keywords_finds_rest_rules()
    {
        var (db, search) = await SeededAsync();
        await using var _db = db;

        var hits = await search.SearchAsync("odpoczynek dobowy 11 godzin");

        Assert.Contains(hits, h => h.Number == "132");
        Assert.True(hits.Count <= 5);
    }

    [Theory]
    [InlineData("zzzxqwy")]
    [InlineData("art. 99999")]
    public async Task UC23_Search_without_match_returns_empty_not_error(string query)
    {
        var (db, search) = await SeededAsync();
        await using var _db = db;

        Assert.Empty(await search.SearchAsync(query));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UC23_Blank_query_returns_empty(string query)
    {
        var (db, search) = await SeededAsync();
        await using var _db = db;

        Assert.Empty(await search.SearchAsync(query));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("[a-z]")]
    [InlineData("100%")]
    public async Task UC23_Search_treats_wildcards_literally(string query)
    {
        var (db, search) = await SeededAsync();
        await using var _db = db;

        var hits = await search.SearchAsync(query);

        Assert.True(hits.Count <= 5);
        if (query is "%" or "_")
        {
            Assert.Empty(hits);
        }
    }
}
