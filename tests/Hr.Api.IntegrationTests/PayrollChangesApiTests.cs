using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hr.Core.Abstractions;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class PayrollChangesApiTests(MsSqlFixture fixture)
{
    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> EmployeeIdOf(ApiSession session) =>
        (await JsonOf(await session.GetAsync("/api/auth/me"))).GetProperty("employeeId").GetGuid();

    private static async Task<Guid> UserIdOf(ApiSession session) =>
        (await JsonOf(await session.GetAsync("/api/auth/me"))).GetProperty("userId").GetGuid();

    private static Task<HttpResponseMessage> Create(ApiSession s, Guid employeeId, string component = "BaseSalary", decimal value = 8500m) =>
        s.PostAsync("/api/payroll-changes", new { employeeId, component, value, effectiveFrom = "2026-11-01", reason = "Podwyżka roczna" });

    private static async Task<Guid> CreatedId(ApiSession s, Guid employeeId, string component = "BaseSalary", decimal value = 8500m) =>
        (await JsonOf(await Create(s, employeeId, component, value))).GetProperty("id").GetGuid();

    [Fact]
    public async Task UC18_Full_flow_over_http()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var maria = await ApiSession.LoginAsAsync(factory, "maria.kaminska");
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var created = await Create(piotr, await EmployeeIdOf(anna));
        var createdBody = await JsonOf(created);
        var id = createdBody.GetProperty("id").GetGuid();
        var submitted = await piotr.PostAsync($"/api/payroll-changes/{id}/submit");
        var approved = await maria.PostAsync($"/api/payroll-changes/{id}/approve");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Draft", createdBody.GetProperty("status").GetString());
        Assert.Equal("Pending", (await JsonOf(submitted)).GetProperty("status").GetString());
        var body = await JsonOf(approved);
        Assert.Equal("Approved", body.GetProperty("status").GetString());
        Assert.Equal(await UserIdOf(maria), body.GetProperty("decidedByUserId").GetGuid());
        Assert.Equal(await UserIdOf(piotr), body.GetProperty("createdByUserId").GetGuid());
        Assert.Equal("BaseSalary", body.GetProperty("component").GetString());
    }

    [Fact]
    public async Task UC18_Employee_and_manager_cannot_create_is_403()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");
        var target = await EmployeeIdOf(anna);

        Assert.Equal(HttpStatusCode.Forbidden, (await Create(anna, target)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Create(ewa, target)).StatusCode);
    }

    [Fact]
    public async Task UC18_Create_with_unknown_component_is_422()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var response = await Create(piotr, await EmployeeIdOf(anna), component: "Premia");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Validation", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task UC19_Author_with_both_roles_cannot_approve_is_403()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        await TestAccounts.CreateAsync(factory, "dual.role", "Zieliński", Roles.HR, Roles.HRApprover);
        var dual = await ApiSession.LoginAsAsync(factory, "dual.role");
        var id = await CreatedId(dual, await EmployeeIdOf(anna));
        await dual.PostAsync($"/api/payroll-changes/{id}/submit");

        var response = await dual.PostAsync($"/api/payroll-changes/{id}/approve");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UC19_HR_cannot_approve_is_403_and_draft_approval_is_409()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var maria = await ApiSession.LoginAsAsync(factory, "maria.kaminska");
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var id = await CreatedId(piotr, await EmployeeIdOf(anna));

        var draftByApprover = await maria.PostAsync($"/api/payroll-changes/{id}/approve");
        await piotr.PostAsync($"/api/payroll-changes/{id}/submit");
        var byHr = await piotr.PostAsync($"/api/payroll-changes/{id}/approve");

        Assert.Equal(HttpStatusCode.Conflict, draftByApprover.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byHr.StatusCode);
    }

    [Fact]
    public async Task UC20_Reject_requires_reason_and_stores_it()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var maria = await ApiSession.LoginAsAsync(factory, "maria.kaminska");
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var id = await CreatedId(piotr, await EmployeeIdOf(anna));
        await piotr.PostAsync($"/api/payroll-changes/{id}/submit");

        var missing = await maria.PostAsync($"/api/payroll-changes/{id}/reject", new { reason = " " });
        var rejected = await maria.PostAsync($"/api/payroll-changes/{id}/reject", new { reason = "Brak budżetu" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        var body = await JsonOf(rejected);
        Assert.Equal("Rejected", body.GetProperty("status").GetString());
        Assert.Equal("Brak budżetu", body.GetProperty("decisionReason").GetString());
    }

    [Fact]
    public async Task UC03_Employee_sees_only_approved_changes_via_api()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var maria = await ApiSession.LoginAsAsync(factory, "maria.kaminska");
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var tomasz = await ApiSession.LoginAsAsync(factory, "tomasz.lis");
        var annaId = await EmployeeIdOf(anna);
        var approvedId = await CreatedId(piotr, annaId);
        await piotr.PostAsync($"/api/payroll-changes/{approvedId}/submit");
        await maria.PostAsync($"/api/payroll-changes/{approvedId}/approve");
        await CreatedId(piotr, annaId, "FixedAllowance", 300m);

        var asAnna = await JsonOf(await anna.GetAsync("/api/payroll-changes"));
        var asTomasz = await JsonOf(await tomasz.GetAsync("/api/payroll-changes"));
        var asHr = await JsonOf(await piotr.GetAsync("/api/payroll-changes"));

        Assert.Equal(approvedId, asAnna.EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Empty(asTomasz.EnumerateArray());
        Assert.Equal(2, asHr.GetArrayLength());
    }

    [Fact]
    public async Task UC03_Employee_cannot_submit_or_approve_is_403_or_404()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var id = await CreatedId(piotr, await EmployeeIdOf(anna));

        var submit = await anna.PostAsync($"/api/payroll-changes/{id}/submit");
        var approve = await anna.PostAsync($"/api/payroll-changes/{id}/approve");

        Assert.Contains((int)submit.StatusCode, new[] { 403, 404 });
        Assert.Contains((int)approve.StatusCode, new[] { 403, 404 });
    }
}
