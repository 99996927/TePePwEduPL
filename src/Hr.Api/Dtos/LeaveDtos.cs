using Hr.Core.Domain;
using Hr.Core.Services;

namespace Hr.Api.Dtos;

public sealed record EmployeeDto(Guid Id, string FirstName, string LastName, Guid TeamId, string TeamName, DateOnly HireDate)
{
    public static EmployeeDto Of(EmployeeSummary e) => new(e.Id, e.FirstName, e.LastName, e.TeamId, e.TeamName, e.HireDate);
}

public sealed record LeaveBalanceDto(int EntitlementDays, int ApprovedDays, int PendingDays, int RemainingDays)
{
    public static LeaveBalanceDto Of(LeaveBalance b) => new(b.EntitlementDays, b.ApprovedDays, b.PendingDays, b.RemainingDays);
}

public sealed record LeaveRequestDto(
    Guid Id, Guid EmployeeId, DateOnly From, DateOnly To, int Days, string Status,
    Guid? DecidedByEmployeeId, string? DecisionReason, DateTime CreatedAtUtc)
{
    public static LeaveRequestDto Of(LeaveRequest l) =>
        new(l.Id, l.EmployeeId, l.From, l.To, l.Days, l.Status.ToString(), l.DecidedByEmployeeId, l.DecisionReason, l.CreatedAtUtc.AsUtc());
}

public sealed record SubmitLeaveRequest(DateOnly From, DateOnly To);

public sealed record LeaveDecisionRequest(bool Approve, string? Reason);
