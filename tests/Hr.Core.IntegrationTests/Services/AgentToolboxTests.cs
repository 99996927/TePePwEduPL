using System.Text.Json;
using Hr.Core.Abstractions;
using Hr.Core.AgentTools;
using Hr.Core.Domain;
using Hr.Core.Seeding;
using Hr.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class AgentToolboxTests(MsSqlFixture fixture)
{
    private static JsonElement Parse(ToolOutcome outcome) => JsonDocument.Parse(outcome.Json).RootElement;

    private static string ErrorCode(ToolOutcome outcome) => Parse(outcome).GetProperty("error").GetProperty("code").GetString()!;

    private static async Task AddEntry(ServiceTestContext c, Guid employeeId, DateTime start, DateTime end)
    {
        var result = await c.Timesheet(c.As(employeeId, Roles.Employee)).AddEntryAsync(start, end, TimeSpan.Zero);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    private static Task<ToolOutcome> Run(AgentToolbox toolbox, string tool, object? input = null, Guid? conversationId = null) =>
        toolbox.ExecuteAsync(tool, JsonSerializer.Serialize(input ?? new { }), conversationId, CancellationToken.None);

    [Fact]
    public async Task UC21_Leave_balance_tool_returns_numbers_from_service_with_rule_id()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var anna = c.As(c.Org.Anna, Roles.Employee);
        var expected = (await c.Leave(anna).GetBalanceAsync(c.Org.Anna)).Value!;

        var outcome = await Run(c.Toolbox(anna), "get_leave_balance");

        Assert.False(outcome.IsError);
        var json = Parse(outcome);
        Assert.Equal(expected.RemainingDays, json.GetProperty("remainingDays").GetInt32());
        Assert.Equal(expected.EntitlementDays, json.GetProperty("entitlementDays").GetInt32());
        Assert.Equal("KP-154-1", json.GetProperty("ruleId").GetString());
    }

    [Fact]
    public async Task UC22_Overtime_tool_returns_rule_ids_and_values_from_engine()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await AddEntry(c, c.Org.Anna, new(2026, 10, 6, 8, 0, 0), new(2026, 10, 6, 20, 0, 0));
        var anna = c.As(c.Org.Anna, Roles.Employee);
        var analysis = (await c.Timesheet(anna).AnalyzeAsync(c.Org.Anna, new(2026, 10, 1), new(2026, 10, 31))).Value!;

        var outcome = await Run(c.Toolbox(anna), "calculate_overtime", new { from = "2026-10-01", to = "2026-10-31" });

        Assert.False(outcome.IsError, outcome.Json);
        var json = Parse(outcome);
        Assert.True(analysis.Overtime.Total > 0m);
        Assert.Equal(analysis.Overtime.Hours50, json.GetProperty("overtimeHours50").GetDecimal());
        Assert.Equal(analysis.Overtime.Hours100, json.GetProperty("overtimeHours100").GetDecimal());
        Assert.Equal(analysis.Overtime.Total, json.GetProperty("overtimeTotal").GetDecimal());
        Assert.Equal(analysis.NormHours, json.GetProperty("normHours").GetDecimal());
        Assert.Equal(analysis.WorkedHours, json.GetProperty("workedHours").GetDecimal());
        Assert.Equal(analysis.Overtime.RuleIds.OrderBy(x => x), json.GetProperty("ruleIds").EnumerateArray().Select(x => x.GetString()!).Where(analysis.Overtime.RuleIds.Contains).OrderBy(x => x));
        Assert.Equal(150m, json.GetProperty("annualOvertimeLimitHours").GetDecimal());
    }

    [Fact]
    public async Task UC12_Violations_tool_reports_rest_violation_with_rule_id()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await AddEntry(c, c.Org.Anna, new(2026, 10, 7, 8, 0, 0), new(2026, 10, 7, 22, 0, 0));
        await AddEntry(c, c.Org.Anna, new(2026, 10, 8, 6, 0, 0), new(2026, 10, 8, 14, 0, 0));

        var outcome = await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "check_work_time_violations", new { from = "2026-10-01", to = "2026-10-31" });

        Assert.False(outcome.IsError, outcome.Json);
        var violation = Parse(outcome).GetProperty("restViolations").EnumerateArray().Single();
        Assert.Equal("KP-132-1", violation.GetProperty("ruleId").GetString());
        Assert.Equal(8m, violation.GetProperty("restHours").GetDecimal());
        Assert.Equal(11m, violation.GetProperty("requiredHours").GetDecimal());
    }

    [Fact]
    public async Task UC23_Labour_code_tool_returns_citation_with_article_number()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await LabourCodeSeeder.SeedAsync(c.Db);

        var outcome = await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "search_labour_code", new { query = "art. 132" });

        var hit = Parse(outcome).GetProperty("results").EnumerateArray().First();
        Assert.Equal("132", hit.GetProperty("number").GetString());
        Assert.Equal("art. 132 Kodeksu pracy (Dz.U. 2026 poz. 1245)", hit.GetProperty("citation").GetString());
        Assert.Contains("11 godzin", hit.GetProperty("text").GetString());
    }

    [Fact]
    public async Task UC16_Payroll_tool_returns_stored_result_values_and_rule_ids()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await AddEntry(c, c.Org.Anna, new(2026, 10, 6, 8, 0, 0), new(2026, 10, 6, 20, 0, 0));
        Assert.True((await c.Payroll(c.As(c.Org.Piotr, Roles.HR)).RunMonthAsync(2026, 10)).IsSuccess);
        var stored = await c.Db.PayrollResults.AsNoTracking().SingleAsync(r => r.EmployeeId == c.Org.Anna);
        int rowsBefore = await c.Db.PayrollResults.CountAsync();

        var outcome = await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "get_payroll_result", new { year = 2026, month = 10 });

        Assert.False(outcome.IsError, outcome.Json);
        var json = Parse(outcome);
        Assert.Equal(stored.Gross, json.GetProperty("gross").GetDecimal());
        Assert.Equal(stored.NormalOvertimePay, json.GetProperty("normalOvertimePay").GetDecimal());
        Assert.Equal(stored.Allowance50, json.GetProperty("allowance50").GetDecimal());
        Assert.Equal(stored.BaseSalary, json.GetProperty("baseSalary").GetDecimal());
        Assert.Equal(stored.RuleIds.Split(','), json.GetProperty("ruleIds").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(rowsBefore, await c.Db.PayrollResults.CountAsync());
    }

    [Fact]
    public async Task UC16_Payroll_tool_without_calculation_returns_NotFound_error_and_no_amounts()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        var outcome = await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "get_payroll_result", new { year = 2026, month = 10 });

        Assert.True(outcome.IsError);
        Assert.Equal("NotFound", ErrorCode(outcome));
        Assert.Contains("nie zostało jeszcze wykonane", Parse(outcome).GetProperty("error").GetProperty("message").GetString());
        Assert.DoesNotContain("gross", outcome.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("8000", outcome.Json);
    }

    [Fact]
    public async Task UC24_Employee_cannot_read_other_employees_payroll()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        Assert.True((await c.Payroll(c.As(c.Org.Piotr, Roles.HR)).RunMonthAsync(2026, 10)).IsSuccess);
        var jan = await c.Db.Employees.SingleAsync(e => e.Id == c.Org.Jan);
        jan.MonthlySalary = 9999m;
        await c.Db.SaveChangesAsync();
        Assert.True((await c.Payroll(c.As(c.Org.Piotr, Roles.HR)).RunMonthAsync(2026, 10)).IsSuccess);

        var asAnna = Parse(await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "get_payroll_result", new { employee_id = c.Org.Jan, year = 2026, month = 10 }));
        var asEwa = Parse(await Run(c.Toolbox(c.As(c.Org.Ewa, Roles.Manager)), "get_payroll_result", new { employee_id = c.Org.Tomasz, year = 2026, month = 10 }));
        var asHr = Parse(await Run(c.Toolbox(c.As(c.Org.Piotr, Roles.HR)), "get_payroll_result", new { employee_id = c.Org.Tomasz, year = 2026, month = 10 }));

        Assert.Equal(c.Org.Anna, asAnna.GetProperty("employeeId").GetGuid());
        Assert.Equal(c.Org.Ewa, asEwa.GetProperty("employeeId").GetGuid());
        Assert.Equal(c.Org.Tomasz, asHr.GetProperty("employeeId").GetGuid());
    }

    [Fact]
    public async Task UC24_Employee_cannot_read_other_employee_even_if_model_passes_employee_id()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        var outcome = await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "get_leave_balance", new { employee_id = c.Org.Jan });
        var forged = await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "get_leave_balance", new { employee_id = Guid.NewGuid() });

        Assert.False(outcome.IsError);
        Assert.Equal(c.Org.Anna, Parse(outcome).GetProperty("employeeId").GetGuid());
        Assert.False(forged.IsError);
        Assert.Equal(outcome.Json, forged.Json);
    }

    [Fact]
    public async Task UC24_HR_may_target_an_employee_and_gets_NotFound_for_unknown_one()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var hr = c.Toolbox(c.As(c.Org.Piotr, Roles.HR));

        var known = await Run(hr, "get_leave_balance", new { employee_id = c.Org.Jan });
        var unknown = await Run(hr, "get_leave_balance", new { employee_id = Guid.NewGuid() });
        var malformed = await Run(hr, "get_leave_balance", new { employee_id = "nie-guid" });

        Assert.Equal(c.Org.Jan, Parse(known).GetProperty("employeeId").GetGuid());
        Assert.Equal("NotFound", ErrorCode(unknown));
        Assert.Equal("InvalidInput", ErrorCode(malformed));
    }

    [Fact]
    public async Task UC25_Service_failure_becomes_error_outcome()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        var outcome = await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "calculate_overtime", new { from = "2026-10-31", to = "2026-10-01" });

        Assert.True(outcome.IsError);
        Assert.Equal("Validation", ErrorCode(outcome));
        Assert.DoesNotContain("overtimeTotal", outcome.Json);
    }

    [Fact]
    public async Task Period_longer_than_400_days_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        var outcome = await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "check_work_time_violations", new { from = "2024-01-01", to = "2026-10-01" });

        Assert.Equal("Validation", ErrorCode(outcome));
    }

    [Fact]
    public async Task Unknown_tool_and_malformed_json_give_error_outcomes_not_exceptions()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var toolbox = c.Toolbox(c.As(c.Org.Anna, Roles.Employee));

        Assert.Equal("UnknownTool", ErrorCode(await Run(toolbox, "delete_everything")));
        Assert.Equal("InvalidInput", ErrorCode(await toolbox.ExecuteAsync("calculate_overtime", "{ nie json", null, CancellationToken.None)));
        Assert.Equal("InvalidInput", ErrorCode(await toolbox.ExecuteAsync("calculate_overtime", "[1,2]", null, CancellationToken.None)));
        Assert.Equal("InvalidInput", ErrorCode(await Run(toolbox, "calculate_overtime", new { to = "2026-10-31" })));
        Assert.Equal("InvalidInput", ErrorCode(await Run(toolbox, "calculate_overtime", new { from = "jutro", to = "2026-10-31" })));
        Assert.Equal("InvalidInput", ErrorCode(await Run(toolbox, "get_payroll_result", new { year = "dwa tysiące", month = 10 })));
        Assert.Equal("Validation", ErrorCode(await Run(toolbox, "get_payroll_result", new { year = 2026, month = 13 })));
    }

    [Fact]
    public async Task UC26_Only_HR_sees_and_can_run_propose_correction()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var entry = await c.Db.WorkEntries.AsNoTracking().FirstAsync(w => w.EmployeeId == c.Org.Anna);
        var input = new { work_entry_id = entry.Id, start = "2026-10-05T09:00:00", end = "2026-10-05T17:00:00", break_minutes = 30, reason = "Brak odbicia" };
        var conversationId = Guid.NewGuid();

        var employee = c.Toolbox(c.As(c.Org.Anna, Roles.Employee));
        var hr = c.Toolbox(c.As(c.Org.Piotr, Roles.HR));

        Assert.DoesNotContain(employee.DefinitionsForCurrentUser(), d => d.Name == "propose_correction");
        Assert.Contains(hr.DefinitionsForCurrentUser(), d => d.Name == "propose_correction");
        Assert.Equal("Forbidden", ErrorCode(await Run(employee, "propose_correction", input)));
        Assert.Equal(0, await c.Db.CorrectionDrafts.CountAsync());

        var created = await Run(hr, "propose_correction", input, conversationId);

        Assert.False(created.IsError, created.Json);
        Assert.Equal("Pending", Parse(created).GetProperty("status").GetString());
        var draft = await c.Db.CorrectionDrafts.SingleAsync();
        Assert.Equal(Parse(created).GetProperty("draftId").GetGuid(), draft.Id);
        Assert.Equal(conversationId, draft.ConversationId);
        Assert.Equal(CorrectionDraftStatus.Pending, draft.Status);
    }

    [Fact]
    public async Task Propose_correction_rejects_start_with_time_zone()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var entry = await c.Db.WorkEntries.AsNoTracking().FirstAsync(w => w.EmployeeId == c.Org.Anna);

        var outcome = await Run(c.Toolbox(c.As(c.Org.Piotr, Roles.HR)), "propose_correction",
            new { work_entry_id = entry.Id, start = "2026-10-05T09:00:00Z", end = "2026-10-05T17:00:00Z", break_minutes = 0, reason = "x" });

        Assert.Equal("InvalidInput", ErrorCode(outcome));
    }

    [Fact]
    public async Task Definitions_expose_employee_id_only_to_hr_staff()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        var employeeTools = c.Toolbox(c.As(c.Org.Anna, Roles.Employee)).DefinitionsForCurrentUser();
        var hrTools = c.Toolbox(c.As(c.Org.Piotr, Roles.HR)).DefinitionsForCurrentUser();

        Assert.Equal(
            new[] { "calculate_overtime", "check_work_time_violations", "get_leave_balance", "get_payroll_result", "list_work_entries", "search_labour_code" },
            employeeTools.Select(d => d.Name).OrderBy(n => n));
        Assert.Equal(8, hrTools.Count);
        Assert.Contains(hrTools, d => d.Name == "find_employee");
        Assert.All(employeeTools.Where(d => d.Name != "search_labour_code"), d => Assert.DoesNotContain("employee_id", d.InputSchemaJson));
        Assert.All(hrTools.Where(d => d.Name is "get_leave_balance" or "get_payroll_result" or "calculate_overtime" or "check_work_time_violations" or "list_work_entries"),
            d => Assert.Contains("employee_id", d.InputSchemaJson));
        Assert.All(hrTools.Concat(employeeTools), d => Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(d.InputSchemaJson).RootElement.ValueKind));
    }

    [Fact]
    public async Task Every_tool_call_is_audited_without_secrets()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var conversationId = Guid.NewGuid();

        await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "get_leave_balance", null, conversationId);
        await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "nieznane", null, conversationId);

        var rows = await c.Db.AuditLogs.AsNoTracking().Where(a => a.EventType == "AgentToolCall").ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal("Conversation", r.EntityType);
            Assert.Equal(conversationId.ToString(), r.EntityId);
            Assert.Equal(c.UserIdOf(c.Org.Anna), r.ActorUserId);
        });
        Assert.Contains(rows, r => r.AfterJson!.Contains("get_leave_balance") && r.AfterJson.Contains("\"ok\":true"));
        Assert.Contains(rows, r => r.AfterJson!.Contains("nieznane") && r.AfterJson.Contains("\"ok\":false"));
    }

    [Fact]
    public async Task UC26_List_work_entries_returns_ids_that_propose_correction_accepts()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var hr = c.Toolbox(c.As(c.Org.Piotr, Roles.HR));

        var listed = Parse(await Run(hr, "list_work_entries", new { from = "2026-10-01", to = "2026-10-31", employee_id = c.Org.Anna }));
        var entry = listed.GetProperty("entries").EnumerateArray().Single();
        var created = await Run(hr, "propose_correction",
            new { work_entry_id = entry.GetProperty("id").GetGuid(), start = "2026-10-05T09:00:00", end = "2026-10-05T17:00:00", break_minutes = 30, reason = "Korekta" });

        Assert.Equal(c.Org.Anna, listed.GetProperty("employeeId").GetGuid());
        Assert.Equal("2026-10-05T08:00:00", entry.GetProperty("start").GetString());
        Assert.Equal(0, entry.GetProperty("breakMinutes").GetInt32());
        Assert.False(created.IsError, created.Json);
    }

    [Fact]
    public async Task UC24_List_work_entries_for_employee_ignores_employee_id_and_manager_sees_only_own()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture, withRequests: true);
        var anna = c.Toolbox(c.As(c.Org.Anna, Roles.Employee));
        var ewa = c.Toolbox(c.As(c.Org.Ewa, Roles.Manager));

        var asAnna = Parse(await Run(anna, "list_work_entries", new { from = "2026-10-01", to = "2026-10-31", employee_id = c.Org.Jan }));
        var asEwa = Parse(await Run(ewa, "list_work_entries", new { from = "2026-10-01", to = "2026-10-31", employee_id = c.Org.Anna }));

        Assert.Equal(c.Org.Anna, asAnna.GetProperty("employeeId").GetGuid());
        Assert.Single(asAnna.GetProperty("entries").EnumerateArray());
        Assert.Equal(c.Org.Ewa, asEwa.GetProperty("employeeId").GetGuid());
        Assert.Single(asEwa.GetProperty("entries").EnumerateArray());
    }

    [Fact]
    public async Task List_work_entries_validates_period()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var toolbox = c.Toolbox(c.As(c.Org.Anna, Roles.Employee));

        Assert.Equal("Validation", ErrorCode(await Run(toolbox, "list_work_entries", new { from = "2026-10-31", to = "2026-10-01" })));
        Assert.Equal("Validation", ErrorCode(await Run(toolbox, "list_work_entries", new { from = "2024-01-01", to = "2026-10-01" })));
        Assert.Equal("InvalidInput", ErrorCode(await Run(toolbox, "list_work_entries", new { from = "2026-10-01" })));
    }

    [Fact]
    public async Task UC24_Find_employee_is_HR_only_and_returns_ids_without_salary()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        var found = await Run(c.Toolbox(c.As(c.Org.Piotr, Roles.HR)), "find_employee", new { query = "nowak" });
        var none = await Run(c.Toolbox(c.As(c.Org.Piotr, Roles.HR)), "find_employee", new { query = "zzzz" });
        var asEmployee = await Run(c.Toolbox(c.As(c.Org.Anna, Roles.Employee)), "find_employee", new { query = "nowak" });
        var asManager = await Run(c.Toolbox(c.As(c.Org.Ewa, Roles.Manager)), "find_employee", new { query = "nowak" });

        var hit = Parse(found).GetProperty("employees").EnumerateArray().Single();
        Assert.Equal(c.Org.Anna, hit.GetProperty("id").GetGuid());
        Assert.Equal("Anna", hit.GetProperty("firstName").GetString());
        Assert.DoesNotContain("alary", found.Json, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Parse(none).GetProperty("employees").EnumerateArray());
        Assert.Equal("Forbidden", ErrorCode(asEmployee));
        Assert.Equal("Forbidden", ErrorCode(asManager));
    }

    [Fact]
    public async Task UC24_Find_employee_matches_full_name_and_limits_results()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var hr = c.Toolbox(c.As(c.Org.Piotr, Roles.HR));

        var byFullName = Parse(await Run(hr, "find_employee", new { query = "Jan Kowalski" }));
        var everyone = Parse(await Run(hr, "find_employee", new { query = "a" }));

        Assert.Equal(c.Org.Jan, byFullName.GetProperty("employees").EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.True(everyone.GetProperty("employees").GetArrayLength() <= 10);
    }
}
