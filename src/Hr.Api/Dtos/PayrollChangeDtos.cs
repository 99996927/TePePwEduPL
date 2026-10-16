using Hr.Core.Domain;

namespace Hr.Api.Dtos;

public sealed record PayrollChangeDto(
    Guid Id, Guid EmployeeId, string Component, decimal Value, DateOnly EffectiveFrom, string Reason, string Status,
    Guid CreatedByUserId, Guid? DecidedByUserId, string? DecisionReason, DateTime CreatedAtUtc)
{
    public static PayrollChangeDto Of(PayrollChange c) => new(
        c.Id, c.EmployeeId, c.Component.ToString(), c.Value, c.EffectiveFrom, c.Reason, c.Status.ToString(),
        c.CreatedByUserId, c.DecidedByUserId, c.DecisionReason, c.CreatedAtUtc);
}

/// <param name="Component"><c>BaseSalary</c> albo <c>FixedAllowance</c>.</param>
public sealed record CreatePayrollChangeRequest(Guid EmployeeId, string Component, decimal Value, DateOnly EffectiveFrom, string Reason);

public sealed record RejectRequest(string Reason);
