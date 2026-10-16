using Hr.Core.Auditing;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class AuditTests(MsSqlFixture fixture)
{
    private static readonly Guid Actor = Guid.Parse("11111111-1111-1111-1111-111111111111");

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

    [Fact]
    public async Task UC28_Modifying_employee_writes_audit_with_before_and_after()
    {
        var clock = new FakeClock { UtcNow = new DateTime(2026, 10, 16, 14, 30, 0, DateTimeKind.Utc) };
        await using var db = await fixture.NewContextAsync(new TestUser(Actor, null, "HR"), clock);
        var employee = await SeedEmployee(db);

        employee.MonthlySalary = 8500m;
        await db.SaveChangesAsync();

        var entry = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EventType == "Modified" && a.EntityType == "Employee");
        Assert.Equal(Actor, entry.ActorUserId);
        Assert.Equal(clock.UtcNow, entry.AtUtc);
        Assert.Equal(employee.Id.ToString(), entry.EntityId);
        Assert.Contains("8000", entry.BeforeJson);
        Assert.Contains("8500", entry.AfterJson);
    }

    [Fact]
    public async Task UC28_Adding_leave_request_writes_added_entry()
    {
        await using var db = await fixture.NewContextAsync(new TestUser(Actor, null, "Employee"));
        var employee = await SeedEmployee(db);
        var request = new LeaveRequest
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, From = new(2026, 10, 12), To = new(2026, 10, 16), Days = 5,
            Status = LeaveStatus.Pending, CreatedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
        };
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var entry = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EventType == "Added" && a.EntityType == "LeaveRequest");
        Assert.Equal(request.Id.ToString(), entry.EntityId);
        Assert.Null(entry.BeforeJson);
        Assert.Contains("Pending", entry.AfterJson);
    }

    [Fact]
    public async Task AuditLog_update_is_rejected_by_database()
    {
        await using var db = await fixture.NewContextAsync(new TestUser(Actor, null, "HR"));
        await SeedEmployee(db);

        var ex = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE AuditLog SET EventType = 'x'"));
        Assert.Contains("append-only", ex.Message);
    }

    [Fact]
    public async Task AuditLog_delete_is_rejected_by_database()
    {
        await using var db = await fixture.NewContextAsync(new TestUser(Actor, null, "HR"));
        await SeedEmployee(db);

        var ex = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM AuditLog"));
        Assert.Contains("append-only", ex.Message);
        Assert.True(await db.AuditLogs.AnyAsync());
    }

    [Fact]
    public async Task Failed_save_leaves_no_audit_rows()
    {
        await using var db = await fixture.NewContextAsync(new TestUser(Actor, null, "HR"));
        var employee = await SeedEmployee(db);
        int before = await db.AuditLogs.CountAsync();

        employee.MonthlySalary = 9000m;
        db.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, From = new(2026, 10, 16), To = new(2026, 10, 12), Days = 5,
            Status = LeaveStatus.Pending, CreatedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        await using var fresh = await fixture.OpenContextAsync(db.Database.GetConnectionString()!);
        Assert.Equal(before, await fresh.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task AuditWriter_records_custom_event()
    {
        var clock = new FakeClock();
        var user = new TestUser(Actor, null, "HR");
        await using var db = await fixture.NewContextAsync(user, clock);
        var conversationId = Guid.NewGuid().ToString();

        new AuditWriter(db, user, clock).Record("ConversationRead", "Conversation", conversationId);
        await db.SaveChangesAsync();

        var entry = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EventType == "ConversationRead");
        Assert.Equal("Conversation", entry.EntityType);
        Assert.Equal(conversationId, entry.EntityId);
        Assert.Equal(Actor, entry.ActorUserId);
        Assert.Equal(clock.UtcNow, entry.AtUtc);
    }
}
