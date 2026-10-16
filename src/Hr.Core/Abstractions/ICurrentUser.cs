namespace Hr.Core.Abstractions;

/// <summary>Tożsamość wywołującego, z której wynika zakres widocznych danych.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? EmployeeId { get; }

    IReadOnlySet<string> Roles { get; }

    bool IsInRole(string role);
}
