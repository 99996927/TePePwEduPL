using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class LeaveApiTests(MsSqlFixture fixture)
{
    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> EmployeeIdOf(ApiSession session) =>
        (await JsonOf(await session.GetAsync("/api/auth/me"))).GetProperty("employeeId").GetGuid();

    private static Task<HttpResponseMessage> Submit(ApiSession s, string from, string to) =>
        s.PostAsync("/api/leave/requests", new { from, to });

    [Fact]
    public async Task UC05_Balance_for_self_returns_entitlement_by_service_years()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var jan = await ApiSession.LoginAsAsync(factory, "jan.kowalski");

        var annaBalance = await JsonOf(await anna.GetAsync("/api/leave/balance"));
        var janBalance = await JsonOf(await jan.GetAsync("/api/leave/balance"));

        Assert.Equal(20, annaBalance.GetProperty("entitlementDays").GetInt32());
        Assert.Equal(20, annaBalance.GetProperty("remainingDays").GetInt32());
        Assert.Equal(26, janBalance.GetProperty("entitlementDays").GetInt32());
    }

    [Fact]
    public async Task UC03_Balance_of_other_team_member_is_404_via_url()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var tomasz = await ApiSession.LoginAsAsync(factory, "tomasz.lis");

        var response = await tomasz.GetAsync($"/api/leave/balance?employeeId={await EmployeeIdOf(anna)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task UC06_Employee_submits_request_and_gets_201_with_location()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var response = await Submit(anna, "2026-10-12", "2026-10-16");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal(5, body.GetProperty("days").GetInt32());
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.NotNull(response.Headers.Location);
        var fetched = await anna.GetAsync(response.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(body.GetProperty("id").GetGuid(), (await JsonOf(fetched)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task UC07_Over_balance_is_422_with_rule_id()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        foreach (var (from, to) in new[] { ("2026-01-12", "2026-01-16"), ("2026-01-19", "2026-01-23"), ("2026-01-26", "2026-01-30"), ("2026-02-02", "2026-02-06") })
        {
            Assert.Equal(HttpStatusCode.Created, (await Submit(anna, from, to)).StatusCode);
        }

        var response = await Submit(anna, "2026-02-09", "2026-02-13");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("KP-154-1", (await JsonOf(response)).GetProperty("ruleId").GetString());
    }

    [Fact]
    public async Task UC06_Overlapping_request_is_409()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        await Submit(anna, "2026-10-12", "2026-10-16");

        var response = await Submit(anna, "2026-10-14", "2026-10-20");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UC08_Manager_approves_via_api_and_employee_sees_status()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");
        var id = (await JsonOf(await Submit(anna, "2026-10-12", "2026-10-16"))).GetProperty("id").GetGuid();

        var decision = await ewa.PostAsync($"/api/leave/requests/{id}/decision", new { approve = true });

        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        Assert.Equal("Approved", (await JsonOf(decision)).GetProperty("status").GetString());
        var list = await JsonOf(await anna.GetAsync("/api/leave/requests"));
        Assert.Equal("Approved", list.EnumerateArray().Single().GetProperty("status").GetString());
        Assert.Equal(5, (await JsonOf(await anna.GetAsync("/api/leave/balance"))).GetProperty("approvedDays").GetInt32());
    }

    [Fact]
    public async Task UC08_Reject_without_reason_is_422()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");
        var id = (await JsonOf(await Submit(anna, "2026-10-12", "2026-10-16"))).GetProperty("id").GetGuid();

        var response = await ewa.PostAsync($"/api/leave/requests/{id}/decision", new { approve = false });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UC03_Tomasz_cannot_see_or_decide_annas_request()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var tomasz = await ApiSession.LoginAsAsync(factory, "tomasz.lis");
        var id = (await JsonOf(await Submit(anna, "2026-10-12", "2026-10-16"))).GetProperty("id").GetGuid();

        var list = await JsonOf(await tomasz.GetAsync("/api/leave/requests"));
        var decision = await tomasz.PostAsync($"/api/leave/requests/{id}/decision", new { approve = true });
        var direct = await tomasz.GetAsync($"/api/leave/requests/{id}");

        Assert.Empty(list.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, decision.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
    }

    [Fact]
    public async Task UC08_Employee_cannot_decide_own_request_is_403()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var id = (await JsonOf(await Submit(anna, "2026-10-12", "2026-10-16"))).GetProperty("id").GetGuid();

        var response = await anna.PostAsync($"/api/leave/requests/{id}/decision", new { approve = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UC09_Author_cancels_pending_request_and_cancelled_cannot_be_approved()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");
        var id = (await JsonOf(await Submit(anna, "2026-10-12", "2026-10-16"))).GetProperty("id").GetGuid();

        var cancel = await anna.PostAsync($"/api/leave/requests/{id}/cancel");
        var approve = await ewa.PostAsync($"/api/leave/requests/{id}/decision", new { approve = true });

        Assert.Equal("Cancelled", (await JsonOf(cancel)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
    }

    [Fact]
    public async Task Responses_use_camelCase_and_iso_dates()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var text = await (await Submit(anna, "2026-10-12", "2026-10-16")).Content.ReadAsStringAsync();

        Assert.Contains("\"from\":\"2026-10-12\"", text);
        Assert.Contains("\"createdAtUtc\"", text);
        Assert.DoesNotContain("\"From\"", text);
    }

    [Fact]
    public async Task Employees_endpoint_does_not_leak_salary_or_account_data()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var response = await piotr.GetAsync("/api/employees");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(6, JsonDocument.Parse(text).RootElement.GetArrayLength());
        Assert.DoesNotContain("monthlySalary", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userName", text, StringComparison.OrdinalIgnoreCase);
        Assert.Single((await JsonOf(await anna.GetAsync("/api/employees"))).EnumerateArray());
    }
}
