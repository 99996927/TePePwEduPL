using Hr.Core.Abstractions;

namespace Hr.Core.Seeding;

/// <summary>Tożsamość pracownika kadr (rola <c>HR</c>), w imieniu którego seed uruchamia naliczenia przez <c>PayrollService</c>.</summary>
public sealed class SeedCurrentUser(Guid userId, Guid employeeId) : ICurrentUser
{
    public Guid? UserId { get; } = userId;

    public Guid? EmployeeId { get; } = employeeId;

    public IReadOnlySet<string> Roles { get; } = new HashSet<string> { Abstractions.Roles.HR };

    public bool IsInRole(string role) => Roles.Contains(role);
}
