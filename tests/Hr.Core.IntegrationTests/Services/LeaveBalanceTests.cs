using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class LeaveBalanceTests(MsSqlFixture fixture)
{
    private static LeaveRequest Request(Guid employeeId, DateOnly from, DateOnly to, int days, LeaveStatus status) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employeeId, From = from, To = to, Days = days, Status = status,
        CreatedAtUtc = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task UC05_Balance_of_employee_hired_2019_is_20_days_all_remaining()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).GetBalanceAsync(c.Org.Anna);

        Assert.True(result.IsSuccess);
        Assert.Equal(new LeaveBalance(20, 0, 0, 20), result.Value);
    }

    [Fact]
    public async Task UC05_Balance_after_hire_date_change_gives_26_days()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        (await c.Db.Employees.SingleAsync(e => e.Id == c.Org.Anna)).HireDate = new DateOnly(2014, 5, 1);
        await c.Db.SaveChangesAsync();

        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).GetBalanceAsync(c.Org.Anna);

        Assert.Equal(26, result.Value!.EntitlementDays);
    }

    [Fact]
    public async Task UC05_Balance_counts_approved_and_pending_of_current_year()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.LeaveRequests.AddRange(
            Request(c.Org.Anna, new(2026, 3, 2), new(2026, 3, 6), 5, LeaveStatus.Approved),
            Request(c.Org.Anna, new(2026, 6, 1), new(2026, 6, 3), 3, LeaveStatus.Pending),
            Request(c.Org.Anna, new(2025, 8, 4), new(2025, 8, 7), 4, LeaveStatus.Approved),
            Request(c.Org.Anna, new(2026, 7, 6), new(2026, 7, 7), 2, LeaveStatus.Cancelled));
        await c.Db.SaveChangesAsync();

        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).GetBalanceAsync(c.Org.Anna);

        Assert.Equal(new LeaveBalance(20, 5, 3, 12), result.Value);
    }

    [Fact]
    public async Task UC05_Balance_of_other_team_is_not_found()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Leave(c.As(c.Org.Tomasz, Roles.Employee)).GetBalanceAsync(c.Org.Anna);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorCode.NotFound, result.Error!.Code);
    }

    [Fact]
    public async Task UC05_Manager_can_read_team_member_balance()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Leave(c.As(c.Org.Ewa, Roles.Manager)).GetBalanceAsync(c.Org.Anna);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UC05_User_without_employee_gets_not_found()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var nobody = new TestUser(Guid.NewGuid(), null, Roles.HR);

        var result = await c.Leave(nobody).GetBalanceAsync(c.Org.Anna);

        Assert.Equal(ServiceErrorCode.NotFound, result.Error!.Code);
    }
}
