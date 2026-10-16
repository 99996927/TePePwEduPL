using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class AuditApiTests(MsSqlFixture fixture)
{
    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> UserIdOf(ApiSession session) =>
        (await JsonOf(await session.GetAsync("/api/auth/me"))).GetProperty("userId").GetGuid();

    private static async Task<Guid> EmployeeIdOf(ApiSession session) =>
        (await JsonOf(await session.GetAsync("/api/auth/me"))).GetProperty("employeeId").GetGuid();

    [Fact]
    public async Task UC28_HR_sees_who_changed_what_after_leave_flow()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var id = (await JsonOf(await anna.PostAsync("/api/leave/requests", new { from = "2026-10-12", to = "2026-10-16" }))).GetProperty("id").GetGuid();
        await ewa.PostAsync($"/api/leave/requests/{id}/decision", new { approve = true });

        var page = await JsonOf(await piotr.GetAsync($"/api/audit?entityType=LeaveRequest&entityId={id}"));

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.Equal("Modified", items[0].GetProperty("eventType").GetString());
        Assert.Equal(await UserIdOf(ewa), items[0].GetProperty("actorUserId").GetGuid());
        Assert.Equal("Added", items[1].GetProperty("eventType").GetString());
        Assert.Equal(await UserIdOf(anna), items[1].GetProperty("actorUserId").GetGuid());
    }

    [Fact]
    public async Task UC28_Employee_and_manager_get_403()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");

        Assert.Equal(HttpStatusCode.Forbidden, (await anna.GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ewa.GetAsync("/api/audit")).StatusCode);
    }

    [Fact]
    public async Task UC28_Audit_includes_payroll_change_approval_and_correction_reason()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var maria = await ApiSession.LoginAsAsync(factory, "maria.kaminska");
        var entryId = (await JsonOf(await anna.PostAsync("/api/timesheet/entries",
            new { start = "2026-10-12T08:00:00", end = "2026-10-12T16:00:00", breakMinutes = 0 }))).GetProperty("id").GetGuid();
        await anna.PutAsync($"/api/timesheet/entries/{entryId}",
            new { start = "2026-10-12T08:00:00", end = "2026-10-12T17:00:00", breakMinutes = 0, reason = "Dopisana godzina" });
        var changeId = (await JsonOf(await piotr.PostAsync("/api/payroll-changes",
            new { employeeId = await EmployeeIdOf(anna), component = "BaseSalary", value = 8000m, effectiveFrom = "2026-11-01", reason = "Podwyżka" }))).GetProperty("id").GetGuid();
        await piotr.PostAsync($"/api/payroll-changes/{changeId}/submit");
        await maria.PostAsync($"/api/payroll-changes/{changeId}/approve");

        var changes = (await JsonOf(await piotr.GetAsync($"/api/audit?entityType=PayrollChange&entityId={changeId}"))).GetProperty("items").EnumerateArray().ToList();
        var entries = (await JsonOf(await piotr.GetAsync($"/api/audit?entityType=WorkEntryRecord&entityId={entryId}"))).GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(changes, c => c.GetProperty("eventType").GetString() == "Modified" && c.GetProperty("actorUserId").GetGuid() == UserIdOf(maria).Result);
        var corrected = entries.Single(e => e.GetProperty("eventType").GetString() == "WorkEntryCorrected");
        Assert.Equal("Dopisana godzina", JsonDocument.Parse(corrected.GetProperty("afterJson").GetString()!).RootElement.GetProperty("Reason").GetString());
    }
}
