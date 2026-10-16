using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hr.Rules.Payroll;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class PayrollApiTests(MsSqlFixture fixture)
{
    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> EmployeeIdOf(ApiSession session) =>
        (await JsonOf(await session.GetAsync("/api/auth/me"))).GetProperty("employeeId").GetGuid();

    private static Task<HttpResponseMessage> Run(ApiSession s, int year = 2026, int month = 10) =>
        s.PostAsync("/api/payroll/runs", new { year, month });

    private static Task<HttpResponseMessage> Result(ApiSession s, Guid employeeId, int year = 2026, int month = 10) =>
        s.GetAsync($"/api/payroll/results?employeeId={employeeId}&year={year}&month={month}");

    [Fact]
    public async Task UC16_HR_runs_month_and_gets_one_row_per_employee()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");

        var response = await Run(piotr);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await JsonOf(response)).EnumerateArray().ToList();
        Assert.Equal(6, rows.Count);
        Assert.All(rows, r => Assert.Equal(176m, r.GetProperty("normHours").GetDecimal()));
        Assert.All(rows, r => Assert.Contains("KP-130-1", r.GetProperty("ruleIds").EnumerateArray().Select(x => x.GetString())));
    }

    [Fact]
    public async Task UC16_Run_is_403_for_employee_and_manager()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");

        Assert.Equal(HttpStatusCode.Forbidden, (await Run(anna)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Run(ewa)).StatusCode);
    }

    [Fact]
    public async Task UC16_Invalid_month_is_422()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");

        var response = await Run(piotr, month: 13);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UC16_Rerun_is_idempotent()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        await Run(piotr);
        var second = await Run(piotr);

        Assert.Equal(6, (await JsonOf(second)).GetArrayLength());
        var own = await Result(anna, await EmployeeIdOf(anna));
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(7200m, (await JsonOf(own)).GetProperty("baseSalary").GetDecimal());
    }

    [Fact]
    public async Task UC16_Overtime_flows_from_timesheet_to_gross()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        await anna.PostAsync("/api/timesheet/entries", new { start = "2026-10-12T08:00:00", end = "2026-10-12T18:00:00", breakMinutes = 0 });

        await Run(piotr);
        var result = await JsonOf(await Result(anna, await EmployeeIdOf(anna)));

        var expected = PayrollCalculator.Calculate(new PayrollInput(7200m, 176m, 2m, 0m));
        Assert.Equal(2m, result.GetProperty("overtime50Hours").GetDecimal());
        Assert.Equal(expected.Gross, result.GetProperty("gross").GetDecimal());
        Assert.Equal(expected.Allowance50, result.GetProperty("allowance50").GetDecimal());
    }

    [Fact]
    public async Task UC17_Employee_reads_own_result_only()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var jan = await ApiSession.LoginAsAsync(factory, "jan.kowalski");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var tomasz = await ApiSession.LoginAsAsync(factory, "tomasz.lis");
        var annaId = await EmployeeIdOf(anna);

        Assert.Equal(HttpStatusCode.NotFound, (await Result(anna, annaId)).StatusCode);
        await Run(piotr);

        Assert.Equal(HttpStatusCode.OK, (await Result(anna, annaId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Result(anna, await EmployeeIdOf(jan))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Result(ewa, annaId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Result(piotr, await EmployeeIdOf(tomasz))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Result(anna, annaId, month: 9)).StatusCode);
    }

    [Fact]
    public async Task UC16_Approved_salary_change_applies_from_effective_month()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var maria = await ApiSession.LoginAsAsync(factory, "maria.kaminska");
        var annaId = await EmployeeIdOf(anna);
        var created = await JsonOf(await piotr.PostAsync("/api/payroll-changes",
            new { employeeId = annaId, component = "BaseSalary", value = 8000m, effectiveFrom = "2026-11-01", reason = "Podwyżka" }));
        var id = created.GetProperty("id").GetGuid();
        await piotr.PostAsync($"/api/payroll-changes/{id}/submit");
        await maria.PostAsync($"/api/payroll-changes/{id}/approve");

        await Run(piotr, month: 10);
        await Run(piotr, month: 11);

        Assert.Equal(7200m, (await JsonOf(await Result(anna, annaId, month: 10))).GetProperty("baseSalary").GetDecimal());
        Assert.Equal(8000m, (await JsonOf(await Result(anna, annaId, month: 11))).GetProperty("baseSalary").GetDecimal());
    }
}
