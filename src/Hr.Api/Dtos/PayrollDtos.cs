using Hr.Core.Domain;

namespace Hr.Api.Dtos;

public sealed record PayrollResultDto(
    Guid EmployeeId, int Year, int Month, decimal BaseSalary, decimal FixedAllowance, decimal NormHours,
    decimal Overtime50Hours, decimal Overtime100Hours, decimal NormalOvertimePay, decimal Allowance50, decimal Allowance100,
    decimal Gross, IReadOnlyList<string> RuleIds, DateTime CalculatedAtUtc)
{
    public static PayrollResultDto Of(PayrollResultRecord r) => new(
        r.EmployeeId, r.Year, r.Month, r.BaseSalary, r.FixedAllowance, r.NormHours, r.Overtime50Hours, r.Overtime100Hours,
        r.NormalOvertimePay, r.Allowance50, r.Allowance100, r.Gross,
        r.RuleIds.Split(',', StringSplitOptions.RemoveEmptyEntries), r.CalculatedAtUtc);
}

public sealed record RunPayrollRequest(int Year, int Month);
