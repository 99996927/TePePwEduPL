using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class TimesheetApiTests(MsSqlFixture fixture)
{
    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> EmployeeIdOf(ApiSession session) =>
        (await JsonOf(await session.GetAsync("/api/auth/me"))).GetProperty("employeeId").GetGuid();

    private static Task<HttpResponseMessage> Add(ApiSession s, string start, string end, int breakMinutes = 0) =>
        s.PostAsync("/api/timesheet/entries", new { start, end, breakMinutes });

    private static async Task<Guid> AddedId(ApiSession s, string start, string end) =>
        (await JsonOf(await Add(s, start, end))).GetProperty("id").GetGuid();

    [Fact]
    public async Task UC10_Anna_adds_entry_and_lists_it()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var created = await Add(anna, "2026-10-12T08:00:00", "2026-10-12T17:00:00", 60);
        var list = await JsonOf(await anna.GetAsync("/api/timesheet/entries?from=2026-10-12&to=2026-10-18"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var entry = list.EnumerateArray().Single();
        Assert.Equal("2026-10-12T08:00:00", entry.GetProperty("start").GetString());
        Assert.Equal(60, entry.GetProperty("breakMinutes").GetInt32());
        Assert.Equal(await EmployeeIdOf(anna), entry.GetProperty("employeeId").GetGuid());
    }

    [Fact]
    public async Task UC10_End_before_start_is_422_and_overlap_is_409()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        await Add(anna, "2026-10-12T08:00:00", "2026-10-12T16:00:00");

        var reversed = await Add(anna, "2026-10-13T17:00:00", "2026-10-13T08:00:00");
        var overlap = await Add(anna, "2026-10-12T12:00:00", "2026-10-12T20:00:00");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reversed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
    }

    [Fact]
    public async Task UC10_Future_entry_is_422()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var response = await Add(anna, "2026-10-17T08:00:00", "2026-10-17T16:00:00");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UC11_Author_corrects_with_reason_and_missing_reason_is_422()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var id = await AddedId(anna, "2026-10-12T08:00:00", "2026-10-12T16:00:00");

        var ok = await anna.PutAsync($"/api/timesheet/entries/{id}", new { start = "2026-10-12T08:00:00", end = "2026-10-12T17:00:00", breakMinutes = 0, reason = "Dopisana godzina" });
        var missing = await anna.PutAsync($"/api/timesheet/entries/{id}", new { start = "2026-10-12T08:00:00", end = "2026-10-12T18:00:00", breakMinutes = 0, reason = " " });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("2026-10-12T17:00:00", (await JsonOf(ok)).GetProperty("end").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
    }

    [Fact]
    public async Task UC11_Manager_correcting_team_entry_is_403_and_other_team_is_404()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");
        var tomasz = await ApiSession.LoginAsAsync(factory, "tomasz.lis");
        var id = await AddedId(anna, "2026-10-12T08:00:00", "2026-10-12T16:00:00");
        var body = new { start = "2026-10-12T08:00:00", end = "2026-10-12T17:00:00", breakMinutes = 0, reason = "Korekta" };

        Assert.Equal(HttpStatusCode.Forbidden, (await ewa.PutAsync($"/api/timesheet/entries/{id}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await tomasz.PutAsync($"/api/timesheet/entries/{id}", body)).StatusCode);
    }

    [Fact]
    public async Task UC12_Analysis_reports_daily_rest_violation()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        await Add(anna, "2026-10-12T08:00:00", "2026-10-12T22:00:00");
        await Add(anna, "2026-10-13T08:00:00", "2026-10-13T16:00:00");

        var analysis = await JsonOf(await piotr.GetAsync($"/api/timesheet/analysis?employeeId={await EmployeeIdOf(anna)}&from=2026-10-12&to=2026-10-18"));

        var violation = analysis.GetProperty("restViolations").EnumerateArray().Single(v => v.GetProperty("ruleId").GetString() == "KP-132-1");
        Assert.Equal(10m, violation.GetProperty("restHours").GetDecimal());
        Assert.Equal(11m, violation.GetProperty("requiredHours").GetDecimal());
    }

    [Fact]
    public async Task UC14_Analysis_reports_overtime_from_engine()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        await Add(anna, "2026-10-12T08:00:00", "2026-10-12T18:00:00");

        var analysis = await JsonOf(await anna.GetAsync($"/api/timesheet/analysis?employeeId={await EmployeeIdOf(anna)}&from=2026-10-12&to=2026-10-18"));

        Assert.Equal(2m, analysis.GetProperty("overtime").GetProperty("hours50").GetDecimal());
        Assert.Equal(10m, analysis.GetProperty("workedHours").GetDecimal());
        Assert.Equal("KP-131-1", analysis.GetProperty("weeklyAverage").GetProperty("ruleId").GetString());
    }

    [Fact]
    public async Task UC15_Manager_analyzes_team_member_and_other_team_is_404()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var tomasz = await ApiSession.LoginAsAsync(factory, "tomasz.lis");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");

        var team = await ewa.GetAsync($"/api/timesheet/analysis?employeeId={await EmployeeIdOf(anna)}&from=2026-10-01&to=2026-10-31");
        var other = await ewa.GetAsync($"/api/timesheet/analysis?employeeId={await EmployeeIdOf(tomasz)}&from=2026-10-01&to=2026-10-31");

        Assert.Equal(HttpStatusCode.OK, team.StatusCode);
        Assert.Equal(176m, (await JsonOf(team)).GetProperty("normHours").GetDecimal());
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task UC15_Employee_analyzes_only_self()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var jan = await ApiSession.LoginAsAsync(factory, "jan.kowalski");

        var response = await anna.GetAsync($"/api/timesheet/analysis?employeeId={await EmployeeIdOf(jan)}&from=2026-10-01&to=2026-10-31");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Analysis_reversed_range_is_422()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");

        var response = await piotr.GetAsync($"/api/timesheet/analysis?employeeId={await EmployeeIdOf(piotr)}&from=2026-10-31&to=2026-10-01");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
