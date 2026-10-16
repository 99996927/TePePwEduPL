using Hr.Core.Domain;
using Hr.Core.Persistence;

namespace Hr.Core.IntegrationTests;

/// <summary>Zespół A (kierownik Ewa: Anna, Jan), zespół B (kierownik Bartek: Tomasz), Kadry (Piotr, Maria); po jednym wniosku i wpisie czasu na osobę.</summary>
public sealed class TestOrg
{
    public Guid TeamA { get; } = Guid.NewGuid();
    public Guid TeamB { get; } = Guid.NewGuid();
    public Guid TeamHr { get; } = Guid.NewGuid();

    public Guid Anna { get; } = Guid.NewGuid();
    public Guid Jan { get; } = Guid.NewGuid();
    public Guid Tomasz { get; } = Guid.NewGuid();
    public Guid Ewa { get; } = Guid.NewGuid();
    public Guid Bartek { get; } = Guid.NewGuid();
    public Guid Piotr { get; } = Guid.NewGuid();
    public Guid Maria { get; } = Guid.NewGuid();

    public IReadOnlyDictionary<Guid, Guid> LeaveByEmployee { get; private set; } = new Dictionary<Guid, Guid>();

    public IReadOnlyList<Guid> AllEmployees => new[] { Anna, Jan, Tomasz, Ewa, Bartek, Piotr, Maria };

    public static async Task<TestOrg> SeedAsync(HrDbContext db)
    {
        var org = new TestOrg();
        db.Teams.AddRange(
            new Team { Id = org.TeamA, Name = "Zespół Ewy" },
            new Team { Id = org.TeamB, Name = "Zespół B" },
            new Team { Id = org.TeamHr, Name = "Kadry" });
        db.Employees.AddRange(
            Emp(org.Anna, "Anna", "Nowak", org.TeamA), Emp(org.Jan, "Jan", "Kowalski", org.TeamA),
            Emp(org.Ewa, "Ewa", "Wiśniewska", org.TeamA), Emp(org.Tomasz, "Tomasz", "Lis", org.TeamB),
            Emp(org.Bartek, "Bartek", "Kowal", org.TeamB), Emp(org.Piotr, "Piotr", "Zieliński", org.TeamHr),
            Emp(org.Maria, "Maria", "Kamińska", org.TeamHr));
        await db.SaveChangesAsync();

        db.Teams.Find(org.TeamA)!.ManagerEmployeeId = org.Ewa;
        db.Teams.Find(org.TeamB)!.ManagerEmployeeId = org.Bartek;
        var leave = new Dictionary<Guid, Guid>();
        foreach (var employeeId in org.AllEmployees)
        {
            var request = new LeaveRequest
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, From = new(2026, 10, 12), To = new(2026, 10, 16), Days = 5,
                Status = LeaveStatus.Pending, CreatedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
            };
            leave[employeeId] = request.Id;
            db.LeaveRequests.Add(request);
            db.WorkEntries.Add(new WorkEntryRecord
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId,
                Start = new(2026, 10, 5, 8, 0, 0), End = new(2026, 10, 5, 16, 0, 0), Break = TimeSpan.Zero,
            });
        }

        await db.SaveChangesAsync();
        org.LeaveByEmployee = leave;
        return org;
    }

    private static Employee Emp(Guid id, string first, string last, Guid teamId) => new()
    {
        Id = id, FirstName = first, LastName = last, HireDate = new DateOnly(2019, 3, 1), TeamId = teamId, MonthlySalary = 8000m,
    };
}
