using Hr.Core.Security;

using Hr.Core.Auditing;

namespace Hr.Core.Domain;

public enum PayrollComponent
{
    BaseSalary,
    FixedAllowance,
}

public enum PayrollChangeStatus
{
    Draft,
    Pending,
    Approved,
    Rejected,
}

/// <summary>Zmiana stawki lub składnika wynagrodzenia pracownika (ADR-15), zatwierdzana przez drugą osobę (ADR-08).</summary>
public class PayrollChange : IEmployeeOwned, IAuditable
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public PayrollComponent Component { get; set; }

    public decimal Value { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public string Reason { get; set; } = string.Empty;

    public PayrollChangeStatus Status { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? DecidedByUserId { get; set; }

    public string? DecisionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
