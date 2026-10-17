using Hr.Core.Domain;

namespace Hr.Core.Seeding.Demo;

public sealed record DemoPerson(
    string First, string Last, string Login, string Role, string TeamName,
    DateOnly HireDate, decimal MonthlySalary, Guid EmployeeId, Guid UserId, bool IsNamed)
{
    public string Password => Login.Replace(".", string.Empty) + "123";
}

public sealed record DemoTeam(Guid Id, string Name, Guid? ManagerEmployeeId);

/// <summary>Wynik generatora danych demo. Zawiera osoby i zespoły oraz wnioski urlopowe, ewidencję czasu pracy, zmiany płacowe i rozmowy; listy są domyślnie puste (profil Accounts).</summary>
public sealed record DemoDataSet
{
    public IReadOnlyList<DemoTeam> Teams { get; init; } = new List<DemoTeam>();
    public IReadOnlyList<DemoPerson> People { get; init; } = new List<DemoPerson>();
    public IReadOnlyList<LeaveRequest> LeaveRequests { get; init; } = new List<LeaveRequest>();
    public IReadOnlyList<WorkEntryRecord> WorkEntries { get; init; } = new List<WorkEntryRecord>();
    public IReadOnlyList<PayrollChange> PayrollChanges { get; init; } = new List<PayrollChange>();
    public IReadOnlyList<Conversation> Conversations { get; init; } = new List<Conversation>();
}
