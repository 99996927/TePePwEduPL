using Hr.Core.Abstractions;

namespace Hr.TestSupport;

public sealed class TestUser : ICurrentUser
{
    public TestUser(Guid? userId, Guid? employeeId, params string[] roles)
    {
        UserId = userId;
        EmployeeId = employeeId;
        Roles = new HashSet<string>(roles);
    }

    public Guid? UserId { get; }

    public Guid? EmployeeId { get; }

    public IReadOnlySet<string> Roles { get; }

    public bool IsInRole(string role) => Roles.Contains(role);
}
