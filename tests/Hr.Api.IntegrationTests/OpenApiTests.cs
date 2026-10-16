using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class OpenApiTests(MsSqlFixture fixture)
{
    private static readonly string[] HttpMethods = { "get", "post", "put", "delete", "patch" };

    private static async Task<JsonObject> FetchAsync(HrApiFactory factory)
    {
        var response = await factory.CreateApiClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    }

    private static IEnumerable<(string Path, string Method, JsonObject Operation)> Operations(JsonObject document)
    {
        foreach (var (path, item) in document["paths"]!.AsObject())
        {
            foreach (var (method, operation) in item!.AsObject().Where(p => HttpMethods.Contains(p.Key)))
            {
                yield return (path, method, operation!.AsObject());
            }
        }
    }

    private static string Canonical(JsonNode? node) => node switch
    {
        JsonObject o => "{" + string.Join(",", o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{JsonSerializer.Serialize(p.Key)}:{Canonical(p.Value)}")) + "}",
        JsonArray a => "[" + string.Join(",", a.Select(Canonical)) + "]",
        null => "null",
        _ => node.ToJsonString(),
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hr.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Nie znaleziono katalogu głównego repozytorium (Hr.sln).");
    }

    [Fact]
    public async Task OpenApi_document_matches_committed_snapshot()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var document = await FetchAsync(factory);
        var path = Path.Combine(RepositoryRoot(), "docs", "openapi", "hr-api.json");
        var pretty = JsonNode.Parse(Canonical(document))!.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, pretty);
            return;
        }

        Assert.True(File.Exists(path), "Brak docs/openapi/hr-api.json: uruchom test z UPDATE_OPENAPI=1.");
        var committed = (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n");
        Assert.True(committed == pretty.ReplaceLineEndings("\n"),
            "Dokument OpenAPI różni się od zatwierdzonego: uruchom z UPDATE_OPENAPI=1 i przejrzyj różnicę w docs/openapi/hr-api.json.");
    }

    [Fact]
    public async Task Every_operation_has_unique_operation_id()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var ids = Operations(await FetchAsync(factory)).Select(o => o.Operation["operationId"]?.GetValue<string>()).ToList();

        Assert.NotEmpty(ids);
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task Every_operation_documents_problem_details_for_errors()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var operations = Operations(await FetchAsync(factory)).ToList();
        var anonymous = new[] { "/api/health", "/api/auth/login", "/api/auth/antiforgery" };

        foreach (var (path, method, operation) in operations)
        {
            var responses = operation["responses"]!.AsObject();
            if (!anonymous.Contains(path))
            {
                Assert.True(responses.ContainsKey("401"), $"{method} {path} nie opisuje odpowiedzi 401");
            }

            if (path.Contains('{'))
            {
                Assert.True(responses.ContainsKey("404"), $"{method} {path} nie opisuje odpowiedzi 404");
            }

            if (method != "get" && !anonymous.Contains(path))
            {
                Assert.True(responses.ContainsKey("400"), $"{method} {path} nie opisuje odpowiedzi 400 (antyforgery)");
            }
        }
    }

    [Fact]
    public async Task OpenApi_endpoint_is_not_exposed_outside_development()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture, environment: "Production");
        var response = await factory.CreateApiClient().GetAsync("/openapi/v1.json");

        // Domyślna polityka wymagająca logowania obejmuje też trasy bez endpointu, więc niezalogowany dostaje 401, zalogowany 404.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.NotFound });
        Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Document_contains_no_sensitive_schema_fields_and_declares_cookie_security()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var text = (await FetchAsync(factory)).ToJsonString();

        Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cookieAuth", text);
    }
}
