using Hr.Core.Domain;
using Hr.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Services;

/// <summary>Wyznacza obowiązującą na dzień wartość składnika: zatwierdzona zmiana (ADR-15) albo wartość bazowa.</summary>
public static class SalaryResolver
{
    /// <summary>Bez kontroli zakresu danych — do użytku wewnętrznego serwisów.</summary>
    public static async Task<decimal> ResolveAsync(
        HrDbContext db, Guid employeeId, PayrollComponent component, DateOnly date, CancellationToken ct = default)
    {
        var approved = await db.PayrollChanges
            .Where(c => c.EmployeeId == employeeId && c.Component == component
                        && c.Status == PayrollChangeStatus.Approved && c.EffectiveFrom <= date)
            .OrderByDescending(c => c.EffectiveFrom)
            .ThenByDescending(c => c.CreatedAtUtc)
            .Select(c => (decimal?)c.Value)
            .FirstOrDefaultAsync(ct);
        if (approved is { } value)
        {
            return value;
        }

        if (component == PayrollComponent.BaseSalary)
        {
            return await db.Employees.Where(e => e.Id == employeeId).Select(e => e.MonthlySalary).FirstOrDefaultAsync(ct);
        }

        return 0m;
    }
}
