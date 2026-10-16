using Hr.Core.Security;

using Hr.Core.Auditing;

namespace Hr.Core.Domain;

public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
}

public class LeaveRequest : IEmployeeOwned, IAuditable
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public int Days { get; set; }

    public LeaveStatus Status { get; set; }

    public Guid? DecidedByEmployeeId { get; set; }

    public string? DecisionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
