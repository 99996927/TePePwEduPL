using Hr.Core.Auditing;
using Hr.Core.Security;

namespace Hr.Core.Domain;

/// <summary>Wynik miesięcznego wstępnego naliczenia pracownika; jeden wiersz na pracownika i miesiąc.</summary>
public class PayrollResultRecord : IEmployeeOwned, IAuditable
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public decimal BaseSalary { get; set; }

    public decimal FixedAllowance { get; set; }

    public decimal NormHours { get; set; }

    public decimal Overtime50Hours { get; set; }

    public decimal Overtime100Hours { get; set; }

    public decimal NormalOvertimePay { get; set; }

    public decimal Allowance50 { get; set; }

    public decimal Allowance100 { get; set; }

    public decimal Gross { get; set; }

    /// <summary>Identyfikatory reguł rozdzielone przecinkami.</summary>
    public string RuleIds { get; set; } = string.Empty;

    public DateTime CalculatedAtUtc { get; set; }

    public Guid? CalculatedByUserId { get; set; }
}
