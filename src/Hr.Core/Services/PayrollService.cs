using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Security;
using Hr.Rules.Calendar;
using Hr.Rules.Overtime;
using Hr.Rules.Payroll;
using Hr.Rules.Time;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Services;

/// <summary>
/// Miesięczne wstępne naliczenia (UC-16, UC-17). Wszystkie liczby pochodzą z <c>Hr.Rules</c>; stawka godzinowa
/// wynika z nominalnego wymiaru czasu pracy miesiąca (bez odejmowania urlopów), a stawki ze zmian zatwierdzonych (ADR-15).
/// </summary>
public sealed class PayrollService(HrDbContext db, ICurrentUser user, IClock clock, WorkCalendar calendar)
{
    public async Task<ServiceResult<IReadOnlyList<PayrollResultRecord>>> RunMonthAsync(int year, int month, CancellationToken ct = default)
    {
        if (!user.IsInRole(Roles.HR) && !user.IsInRole(Roles.HRApprover))
        {
            return ServiceResult<IReadOnlyList<PayrollResultRecord>>.Fail(ServiceErrorCode.Forbidden, "Naliczenia uruchamia dział kadr.");
        }

        if (month is < 1 or > 12 || year is < 2000 or > 2100)
        {
            return ServiceResult<IReadOnlyList<PayrollResultRecord>>.Fail(ServiceErrorCode.Validation, "Nieprawidłowy rok lub miesiąc.");
        }

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        decimal normHours = WorkingTimeNorm.HoursForPeriod(start, end, calendar);
        var lower = start.ToDateTime(TimeOnly.MinValue);
        var upper = end.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var employees = await db.Employees.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToListAsync(ct);
        var entriesByEmployee = (await db.WorkEntries.Where(w => w.Start >= lower && w.Start < upper).ToListAsync(ct))
            .ToLookup(w => w.EmployeeId);

        var computed = new List<(Guid EmployeeId, PayrollResult Pay, OvertimeResult Overtime)>();
        foreach (var employee in employees)
        {
            OvertimeResult overtime;
            try
            {
                overtime = OvertimeCalculator.Calculate(
                    entriesByEmployee[employee.Id].Select(w => w.ToRulesEntry()).ToList(), calendar, NightWindow.Default);
            }
            catch (ArgumentException ex)
            {
                return ServiceResult<IReadOnlyList<PayrollResultRecord>>.Fail(
                    ServiceErrorCode.Validation, $"Nieprawidłowa ewidencja pracownika {employee.FirstName} {employee.LastName}: {ex.Message}");
            }

            decimal baseSalary = await SalaryResolver.ResolveAsync(db, employee.Id, PayrollComponent.BaseSalary, end, ct);
            decimal fixedAllowance = await SalaryResolver.ResolveAsync(db, employee.Id, PayrollComponent.FixedAllowance, end, ct);
            var pay = PayrollCalculator.Calculate(new PayrollInput(baseSalary, normHours, overtime.Hours50, overtime.Hours100, fixedAllowance));
            computed.Add((employee.Id, pay, overtime));
        }

        var existing = await db.PayrollResults.Where(r => r.Year == year && r.Month == month).ToDictionaryAsync(r => r.EmployeeId, ct);
        var rows = new List<PayrollResultRecord>();
        foreach (var (employeeId, pay, overtime) in computed)
        {
            if (!existing.TryGetValue(employeeId, out var row))
            {
                row = new PayrollResultRecord { Id = Guid.NewGuid(), EmployeeId = employeeId, Year = year, Month = month };
                db.PayrollResults.Add(row);
            }

            row.BaseSalary = pay.BaseSalary;
            row.FixedAllowance = pay.FixedAllowance;
            row.NormHours = normHours;
            row.Overtime50Hours = overtime.Hours50;
            row.Overtime100Hours = overtime.Hours100;
            row.NormalOvertimePay = pay.NormalOvertimePay;
            row.Allowance50 = pay.Allowance50;
            row.Allowance100 = pay.Allowance100;
            row.Gross = pay.Gross;
            row.RuleIds = string.Join(",", new[] { WorkingTimeNorm.RuleId }.Concat(overtime.RuleIds).Concat(pay.RuleIds).Distinct());
            row.CalculatedAtUtc = clock.UtcNow;
            row.CalculatedByUserId = user.UserId;
            rows.Add(row);
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult<IReadOnlyList<PayrollResultRecord>>.Ok(rows);
    }

    public async Task<ServiceResult<PayrollResultRecord>> GetAsync(Guid employeeId, int year, int month, CancellationToken ct = default)
    {
        var result = await db.PayrollResults.Visible(db, user)
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Year == year && r.Month == month, ct);
        return result is null
            ? ServiceResult<PayrollResultRecord>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono naliczenia.")
            : ServiceResult<PayrollResultRecord>.Ok(result);
    }
}
