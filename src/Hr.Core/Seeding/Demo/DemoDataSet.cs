using Hr.Core.Domain;

namespace Hr.Core.Seeding.Demo;

public sealed record DemoPerson(
    string First, string Last, string Login, string Role, string TeamName,
    DateOnly HireDate, decimal MonthlySalary, Guid EmployeeId, Guid UserId, bool IsNamed)
{
    public string Password => Login.Replace(".", string.Empty) + "123";
}

public sealed record DemoTeam(Guid Id, string Name, Guid? ManagerEmployeeId);

/// <summary>Wynik generatora danych demo. Kolejne zadania dodają kolejne listy (domyślnie puste).</summary>
public sealed record DemoDataSet
{
    public IReadOnlyList<DemoTeam> Teams { get; init; } = new List<DemoTeam>();
    public IReadOnlyList<DemoPerson> People { get; init; } = new List<DemoPerson>();
    public IReadOnlyList<LeaveRequest> LeaveRequests { get; init; } = new List<LeaveRequest>();
}
