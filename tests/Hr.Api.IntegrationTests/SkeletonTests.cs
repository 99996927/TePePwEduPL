using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hr.Core.Domain;
using Hr.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class SkeletonTests(MsSqlFixture fixture)
{
    private static readonly string[] PublicRoutes = { "api/health", "api/auth/login", "api/auth/antiforgery" };

    [Fact]
    public async Task UC29_Health_endpoint_is_anonymous_and_returns_ok()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var response = await factory.CreateApiClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UC29_Startup_migrates_and_seeds_demo_accounts()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Equal(6, await users.Users.CountAsync());
    }

    [Fact]
    public async Task UC29_Static_index_is_served_at_root()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var response = await factory.CreateApiClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Every_endpoint_except_the_public_ones_requires_authentication()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var client = factory.CreateApiClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .Where(e => !e.RoutePattern.RawText!.TrimStart('/').StartsWith("test/", StringComparison.Ordinal))
            .Where(e => !PublicRoutes.Contains(e.RoutePattern.RawText!.TrimStart('/')))
            .ToList();
        Assert.NotEmpty(endpoints);

        foreach (var endpoint in endpoints)
        {
            var path = "/" + string.Join("/", endpoint.RoutePattern.PathSegments.Select(SegmentValue));
            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
                Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{method} {path} zwróciło {(int)response.StatusCode}");
            }
        }
    }

    private static string SegmentValue(RoutePatternPathSegment segment) => string.Concat(segment.Parts.Select(part => part switch
    {
        RoutePatternLiteralPart literal => literal.Content,
        RoutePatternParameterPart => Guid.NewGuid().ToString(),
        _ => string.Empty,
    }));

    [Fact]
    public async Task Unauthenticated_request_gets_401_not_redirect()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var response = await factory.CreateApiClient().GetAsync("/test/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData("NotFound", 404)]
    [InlineData("Forbidden", 403)]
    [InlineData("Validation", 422)]
    [InlineData("Conflict", 409)]
    public async Task ServiceResult_errors_map_to_problem_details(string kind, int status)
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var response = await factory.CreateApiClient().GetAsync($"/test/result/{kind}");

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(kind, body.GetProperty("code").GetString());
        Assert.Equal("KP-154-1", body.GetProperty("ruleId").GetString());
        Assert.Equal("Komunikat testowy", body.GetProperty("detail").GetString());
        Assert.Equal(status, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Unhandled_exception_returns_500_problem_without_details()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var response = await factory.CreateApiClient().GetAsync("/test/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("InvalidOperationException", text);
        Assert.DoesNotContain("Sekret wewnętrzny", text);
        Assert.DoesNotContain(" at Hr.", text);
    }
}
