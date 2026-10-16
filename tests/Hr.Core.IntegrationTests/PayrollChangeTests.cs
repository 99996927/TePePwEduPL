using Hr.Core.Domain;
using Hr.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class PayrollChangeTests(MsSqlFixture fixture)
{
    private static async Task<Employee> SeedEmployee(HrDbContext db)
    {
        var team = new Team { Id = Guid.NewGuid(), Name = "Zespół" };
        var employee = new Employee
        {
            Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Nowak",
            HireDate = new DateOnly(2019, 3, 1), TeamId = team.Id, MonthlySalary = 8000m,
        };
        db.Teams.Add(team);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee;
    }

    private static PayrollChange Change(Guid employeeId, Guid createdBy, Guid? decidedBy, PayrollChangeStatus status, decimal value = 8500m) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employeeId, Component = PayrollComponent.BaseSalary, Value = value,
        EffectiveFrom = new DateOnly(2026, 11, 1), Reason = "Podwyżka roczna", Status = status,
        CreatedByUserId = createdBy, DecidedByUserId = decidedBy,
        CreatedAtUtc = new DateTime(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task UC18_Approved_by_different_user_is_stored()
    {
        await using var db = await fixture.NewContextAsync();
        var employee = await SeedEmployee(db);
        var change = Change(employee.Id, Guid.NewGuid(), Guid.NewGuid(), PayrollChangeStatus.Approved);
        db.PayrollChanges.Add(change);
        await db.SaveChangesAsync();

        var loaded = await db.PayrollChanges.AsNoTracking().SingleAsync(c => c.Id == change.Id);
        Assert.Equal(PayrollChangeStatus.Approved, loaded.Status);
        Assert.Equal(PayrollComponent.BaseSalary, loaded.Component);
        Assert.Equal(8500m, loaded.Value);
    }

    [Fact]
    public async Task UC19_Database_rejects_self_approval()
    {
        await using var db = await fixture.NewContextAsync();
        var employee = await SeedEmployee(db);
        var sameUser = Guid.NewGuid();
        db.PayrollChanges.Add(Change(employee.Id, sameUser, sameUser, PayrollChangeStatus.Approved));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("CK_PayrollChange_FourEyes", ex.InnerException!.Message);
    }

    [Fact]
    public async Task UC19_Pending_without_decider_is_allowed()
    {
        await using var db = await fixture.NewContextAsync();
        var employee = await SeedEmployee(db);
        db.PayrollChanges.Add(Change(employee.Id, Guid.NewGuid(), null, PayrollChangeStatus.Pending));
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.PayrollChanges.CountAsync());
    }

    [Fact]
    public async Task PayrollChange_requires_non_negative_value()
    {
        await using var db = await fixture.NewContextAsync();
        var employee = await SeedEmployee(db);
        db.PayrollChanges.Add(Change(employee.Id, Guid.NewGuid(), null, PayrollChangeStatus.Draft, value: -1m));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
