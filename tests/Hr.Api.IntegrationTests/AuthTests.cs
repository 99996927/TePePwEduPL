using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hr.Core.Abstractions;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class AuthTests(MsSqlFixture fixture)
{
    public static TheoryData<string, string, string> Accounts => new()
    {
        { "anna.nowak", Roles.Employee, "Anna Nowak" },
        { "jan.kowalski", Roles.Employee, "Jan Kowalski" },
        { "tomasz.lis", Roles.Employee, "Tomasz Lis" },
        { "ewa.wisniewska", Roles.Manager, "Ewa Wiśniewska" },
        { "piotr.zielinski", Roles.HR, "Piotr Zieliński" },
        { "maria.kaminska", Roles.HRApprover, "Maria Kamińska" },
    };

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    [Theory]
    [MemberData(nameof(Accounts))]
    public async Task UC01_Demo_accounts_log_in_with_documented_passwords(string login, string role, string displayName)
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.StartAsync(factory);

        var response = await session.PostAsync("/api/auth/login", new { userName = login, password = ApiSession.PasswordOf(login) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal(login, body.GetProperty("userName").GetString());
        Assert.Equal(displayName, body.GetProperty("displayName").GetString());
        Assert.NotEqual(Guid.Empty, body.GetProperty("employeeId").GetGuid());
        Assert.Equal(new[] { role }, body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray());
    }

    [Fact]
    public async Task UC01_Me_returns_current_user_after_login_and_401_before()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.StartAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);

        var login = await JsonOf(await session.PostAsync("/api/auth/login", new { userName = "anna.nowak", password = "annanowak123" }));
        var me = await session.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(login.GetProperty("userId").GetGuid(), (await JsonOf(me)).GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task UC01_Logout_ends_session()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        Assert.Equal(HttpStatusCode.NoContent, (await session.PostAsync("/api/auth/logout")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task UC02_Wrong_password_and_unknown_user_return_identical_401()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.StartAsync(factory);

        var wrongPassword = await session.PostAsync("/api/auth/login", new { userName = "anna.nowak", password = "zlehaslo1" });
        var unknownUser = await session.PostAsync("/api/auth/login", new { userName = "nie.istnieje", password = "zlehaslo1" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(Normalize(await JsonOf(wrongPassword)), Normalize(await JsonOf(unknownUser)));
        Assert.DoesNotContain(wrongPassword.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : Array.Empty<string>(),
            c => c.StartsWith("hr.session=", StringComparison.Ordinal));
    }

    private static string Normalize(JsonElement body)
    {
        var node = JsonNode.Parse(body.GetRawText())!.AsObject();
        node.Remove("traceId");
        return node.ToJsonString();
    }

    [Fact]
    public async Task UC02_Account_locks_after_five_failed_attempts_then_423_even_with_correct_password()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.StartAsync(factory);

        for (int i = 0; i < 4; i++)
        {
            var failed = await session.PostAsync("/api/auth/login", new { userName = "jan.kowalski", password = "zlehaslo1" });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        await session.PostAsync("/api/auth/login", new { userName = "jan.kowalski", password = "zlehaslo1" });
        var locked = await session.PostAsync("/api/auth/login", new { userName = "jan.kowalski", password = "jankowalski123" });

        Assert.Equal(423, (int)locked.StatusCode);
        Assert.Equal("LockedOut", (await JsonOf(locked)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_without_antiforgery_token_is_400()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "anna.nowak", password = "annanowak123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("AntiforgeryTokenInvalid", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Authenticated_post_without_token_is_400_but_get_is_allowed()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var post = await session.SendAsync(HttpMethod.Post, "/api/auth/logout", null, withToken: false);
        var get = await session.SendAsync(HttpMethod.Get, "/api/auth/me", null, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Session_cookie_is_httponly_and_samesite_strict()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.StartAsync(factory);

        var response = await session.PostAsync("/api/auth/login", new { userName = "anna.nowak", password = "annanowak123" });

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var sessionCookie = Assert.Single(cookies, c => c.StartsWith("hr.session=", StringComparison.Ordinal));
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", sessionCookie, StringComparison.OrdinalIgnoreCase);
        var xsrf = Assert.Single(cookies, c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        Assert.DoesNotContain("httponly", xsrf, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UC04_Me_exposes_roles_so_ui_can_gate_panels()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var roles = (await JsonOf(await session.GetAsync("/api/auth/me"))).GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray();

        Assert.Equal(new[] { Roles.Employee }, roles);
        Assert.DoesNotContain(Roles.HR, roles);
        Assert.DoesNotContain(Roles.HRApprover, roles);
    }

    [Fact]
    public async Task Login_response_does_not_leak_sensitive_fields()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.StartAsync(factory);

        var text = await (await session.PostAsync("/api/auth/login", new { userName = "anna.nowak", password = "annanowak123" })).Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", text, StringComparison.OrdinalIgnoreCase);
    }
}
