using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Services;
using Hr.Rules.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class PayrollServiceTests(MsSqlFixture fixture)
{
    private static WorkEntryRecord Entry(Guid employeeId, DateTime start, DateTime end) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employeeId, Start = start, End = end, Break = TimeSpan.Zero,
    };

    private static PayrollChange Approved(Guid employeeId, PayrollComponent component, decimal value, DateOnly effective) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employeeId, Component = component, Value = value, EffectiveFrom = effective,
        Reason = "Zmiana", Status = PayrollChangeStatus.Approved, CreatedByUserId = Guid.NewGuid(), DecidedByUserId = Guid.NewGuid(),
        CreatedAtUtc = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
    };

    private static async Task<IReadOnlyList<PayrollResultRecord>> RunAsync(ServiceTestContext c, int year, int month)
    {
        var result = await c.Payroll(c.As(c.Org.Piotr, Roles.HR)).RunMonthAsync(year, month);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    [Fact]
    public async Task UC16_Run_creates_one_row_per_employee()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var rows = await RunAsync(c, 2026, 10);

        Assert.Equal(c.Org.AllEmployees.Count, rows.Count);
        Assert.All(rows, r => Assert.Equal(176m, r.NormHours));
        Assert.Equal(c.Org.AllEmployees.Count, await c.Db.PayrollResults.CountAsync());
    }

    [Fact]
    public async Task UC16_Run_uses_engine_for_overtime_and_gross()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.WorkEntries.Add(Entry(c.Org.Anna, new(2026, 10, 12, 8, 0, 0), new(2026, 10, 12, 18, 0, 0)));
        await c.Db.SaveChangesAsync();

        var anna = (await RunAsync(c, 2026, 10)).Single(r => r.EmployeeId == c.Org.Anna);

        var expected = PayrollCalculator.Calculate(new PayrollInput(8000m, 176m, 2m, 0m));
        Assert.Equal(2m, anna.Overtime50Hours);
        Assert.Equal(0m, anna.Overtime100Hours);
        Assert.Equal(90.91m, anna.NormalOvertimePay);
        Assert.Equal(45.45m, anna.Allowance50);
        Assert.Equal(expected.Gross, anna.Gross);
        Assert.Contains("KP-151-1-1", anna.RuleIds);
    }

    [Fact]
    public async Task UC16_Run_applies_approved_salary_change_from_effective_date()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.PayrollChanges.Add(Approved(c.Org.Anna, PayrollComponent.BaseSalary, 8500m, new(2026, 11, 1)));
        await c.Db.SaveChangesAsync();

        var october = (await RunAsync(c, 2026, 10)).Single(r => r.EmployeeId == c.Org.Anna);
        var november = (await RunAsync(c, 2026, 11)).Single(r => r.EmployeeId == c.Org.Anna);

        Assert.Equal(8000m, october.BaseSalary);
        Assert.Equal(8500m, november.BaseSalary);
        Assert.Equal(160m, november.NormHours);
    }

    [Fact]
    public async Task UC16_Run_ignores_unapproved_salary_change()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var pending = Approved(c.Org.Anna, PayrollComponent.BaseSalary, 9500m, new(2026, 10, 1));
        pending.Status = PayrollChangeStatus.Pending;
        pending.DecidedByUserId = null;
        c.Db.PayrollChanges.Add(pending);
        await c.Db.SaveChangesAsync();

        var anna = (await RunAsync(c, 2026, 10)).Single(r => r.EmployeeId == c.Org.Anna);

        Assert.Equal(8000m, anna.BaseSalary);
    }

    [Fact]
    public async Task UC16_Run_adds_approved_fixed_allowance()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.PayrollChanges.Add(Approved(c.Org.Anna, PayrollComponent.FixedAllowance, 300m, new(2026, 10, 1)));
        await c.Db.SaveChangesAsync();

        var anna = (await RunAsync(c, 2026, 10)).Single(r => r.EmployeeId == c.Org.Anna);

        Assert.Equal(300m, anna.FixedAllowance);
        Assert.Equal(8300m, anna.Gross);
    }

    [Fact]
    public async Task UC16_Rerun_updates_instead_of_duplicating()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var first = (await RunAsync(c, 2026, 10)).Single(r => r.EmployeeId == c.Org.Anna);
        Assert.Equal(8000m, first.Gross);

        c.Db.WorkEntries.Add(Entry(c.Org.Anna, new(2026, 10, 12, 8, 0, 0), new(2026, 10, 12, 18, 0, 0)));
        await c.Db.SaveChangesAsync();
        c.Clock.UtcNow = c.Clock.UtcNow.AddHours(1);
        var second = (await RunAsync(c, 2026, 10)).Single(r => r.EmployeeId == c.Org.Anna);

        Assert.True(second.Gross > 8000m);
        Assert.Equal(c.Org.AllEmployees.Count, await c.Db.PayrollResults.CountAsync());
        Assert.Equal(c.Clock.UtcNow, second.CalculatedAtUtc);
        Assert.Equal(c.UserIdOf(c.Org.Piotr), second.CalculatedByUserId);
    }

    [Fact]
    public async Task UC16_Run_is_forbidden_for_employee_and_manager()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        var asAnna = await c.Payroll(c.As(c.Org.Anna, Roles.Employee)).RunMonthAsync(2026, 10);
        var asEwa = await c.Payroll(c.As(c.Org.Ewa, Roles.Manager)).RunMonthAsync(2026, 10);

        Assert.Equal(ServiceErrorCode.Forbidden, asAnna.Error!.Code);
        Assert.Equal(ServiceErrorCode.Forbidden, asEwa.Error!.Code);
        Assert.Equal(0, await c.Db.PayrollResults.CountAsync());
    }

    [Fact]
    public async Task UC16_Approver_can_run()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Payroll(c.As(c.Org.Maria, Roles.HRApprover)).RunMonthAsync(2026, 10);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UC16_Invalid_month_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Payroll(c.As(c.Org.Piotr, Roles.HR)).RunMonthAsync(2026, 13);

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task UC16_Overlapping_entries_fail_whole_run_without_partial_save()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.WorkEntries.AddRange(
            Entry(c.Org.Jan, new(2026, 10, 12, 8, 0, 0), new(2026, 10, 12, 16, 0, 0)),
            Entry(c.Org.Jan, new(2026, 10, 12, 12, 0, 0), new(2026, 10, 12, 20, 0, 0)));
        await c.Db.SaveChangesAsync();

        var result = await c.Payroll(c.As(c.Org.Piotr, Roles.HR)).RunMonthAsync(2026, 10);

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
        Assert.Equal(0, await c.Db.PayrollResults.CountAsync());
    }

    [Fact]
    public async Task UC17_Employee_reads_own_result_only()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await RunAsync(c, 2026, 10);

        var own = await c.Payroll(c.As(c.Org.Anna, Roles.Employee)).GetAsync(c.Org.Anna, 2026, 10);
        var other = await c.Payroll(c.As(c.Org.Anna, Roles.Employee)).GetAsync(c.Org.Jan, 2026, 10);
        var byManager = await c.Payroll(c.As(c.Org.Ewa, Roles.Manager)).GetAsync(c.Org.Anna, 2026, 10);
        var byHr = await c.Payroll(c.As(c.Org.Piotr, Roles.HR)).GetAsync(c.Org.Tomasz, 2026, 10);
        var missing = await c.Payroll(c.As(c.Org.Anna, Roles.Employee)).GetAsync(c.Org.Anna, 2026, 9);

        Assert.True(own.IsSuccess);
        Assert.Equal(ServiceErrorCode.NotFound, other.Error!.Code);
        Assert.True(byManager.IsSuccess);
        Assert.True(byHr.IsSuccess);
        Assert.Equal(ServiceErrorCode.NotFound, missing.Error!.Code);
    }

    [Fact]
    public async Task PayrollResult_unique_per_employee_month()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await RunAsync(c, 2026, 10);

        c.Db.PayrollResults.Add(new PayrollResultRecord
        {
            Id = Guid.NewGuid(), EmployeeId = c.Org.Anna, Year = 2026, Month = 10, RuleIds = string.Empty,
            CalculatedAtUtc = c.Clock.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => c.Db.SaveChangesAsync());
    }
}
