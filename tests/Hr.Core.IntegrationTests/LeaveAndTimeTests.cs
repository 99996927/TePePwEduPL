using Hr.Core.Domain;
using Hr.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class LeaveAndTimeTests(MsSqlFixture fixture)
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

    private static LeaveRequest Leave(Guid employeeId, DateOnly from, DateOnly to, int days = 5) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employeeId, From = from, To = to, Days = days,
        Status = LeaveStatus.Pending, CreatedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task LeaveRequest_roundtrips_with_status_as_text()
    {
        await using var db = await fixture.NewContextAsync();
        var employee = await SeedEmployee(db);
        var request = Leave(employee.Id, new(2026, 10, 12), new(2026, 10, 16));
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var loaded = await db.LeaveRequests.AsNoTracking().SingleAsync(l => l.Id == request.Id);
        Assert.Equal(new DateOnly(2026, 10, 12), loaded.From);
        Assert.Equal(LeaveStatus.Pending, loaded.Status);
        var status = await db.Database.SqlQueryRaw<string>("SELECT CAST(Status AS nvarchar(50)) AS [Value] FROM LeaveRequests").SingleAsync();
        Assert.Equal("Pending", status);
    }

    [Fact]
    public async Task WorkEntryRecord_maps_to_rules_entry()
    {
        var record = new WorkEntryRecord
        {
            Id = Guid.NewGuid(), EmployeeId = Guid.NewGuid(),
            Start = new(2026, 10, 12, 8, 0, 0), End = new(2026, 10, 12, 17, 0, 0), Break = TimeSpan.FromHours(1),
        };
        Assert.Equal(8.0m, record.ToRulesEntry().WorkedHours);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task LeaveRequest_rejects_to_before_from()
    {
        await using var db = await fixture.NewContextAsync();
        var employee = await SeedEmployee(db);
        db.LeaveRequests.Add(Leave(employee.Id, new(2026, 10, 16), new(2026, 10, 12)));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("CK_LeaveRequest_Period", ex.InnerException!.Message);
    }

    [Fact]
    public async Task LeaveRequest_rejects_non_positive_days()
    {
        await using var db = await fixture.NewContextAsync();
        var employee = await SeedEmployee(db);
        db.LeaveRequests.Add(Leave(employee.Id, new(2026, 10, 12), new(2026, 10, 16), days: 0));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task WorkEntryRecord_rejects_end_not_after_start()
    {
        await using var db = await fixture.NewContextAsync();
        var employee = await SeedEmployee(db);
        db.WorkEntries.Add(new WorkEntryRecord
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id,
            Start = new(2026, 10, 12, 8, 0, 0), End = new(2026, 10, 12, 8, 0, 0), Break = TimeSpan.Zero,
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("CK_WorkEntry_Period", ex.InnerException!.Message);
    }
}
