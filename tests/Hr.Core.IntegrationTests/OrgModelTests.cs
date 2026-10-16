using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class OrgModelTests(MsSqlFixture fixture)
{
    private static Team NewTeam() => new() { Id = Guid.NewGuid(), Name = "Zespół Ewy" };

    private static Employee NewEmployee(Guid teamId, decimal salary = 8000.00m) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Anna",
        LastName = "Nowak",
        HireDate = new DateOnly(2019, 3, 1),
        TeamId = teamId,
        MonthlySalary = salary,
    };

    [Fact]
    public async Task Org_model_roundtrips_in_mssql()
    {
        var connectionString = await fixture.NewMigratedDatabaseAsync();
        var team = NewTeam();
        var employee = NewEmployee(team.Id);
        await using (var db = await fixture.OpenContextAsync(connectionString))
        {
            db.Teams.Add(team);
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
        }

        await using var fresh = await fixture.OpenContextAsync(connectionString);
        var loaded = await fresh.Employees.SingleAsync(e => e.Id == employee.Id);
        Assert.Equal("Anna", loaded.FirstName);
        Assert.Equal(new DateOnly(2019, 3, 1), loaded.HireDate);
        Assert.Equal(8000.00m, loaded.MonthlySalary);
        Assert.Equal(team.Id, loaded.TeamId);
    }

    [Fact]
    public async Task Employee_requires_existing_team()
    {
        await using var db = await fixture.NewContextAsync();
        db.Employees.Add(NewEmployee(Guid.NewGuid()));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task MonthlySalary_keeps_two_decimals()
    {
        var connectionString = await fixture.NewMigratedDatabaseAsync();
        var team = NewTeam();
        var employee = NewEmployee(team.Id, 1234.56m);
        await using (var db = await fixture.OpenContextAsync(connectionString))
        {
            db.Teams.Add(team);
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
        }

        await using var fresh = await fixture.OpenContextAsync(connectionString);
        Assert.Equal(1234.56m, (await fresh.Employees.SingleAsync()).MonthlySalary);
    }
}
