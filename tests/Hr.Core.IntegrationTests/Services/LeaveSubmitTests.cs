using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class LeaveSubmitTests(MsSqlFixture fixture)
{
    private static LeaveRequest Existing(Guid employeeId, DateOnly from, DateOnly to, int days, LeaveStatus status) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employeeId, From = from, To = to, Days = days, Status = status,
        CreatedAtUtc = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task UC06_Submit_creates_pending_request_with_working_days()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).SubmitAsync(new(2026, 10, 12), new(2026, 10, 16));

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Days);
        Assert.Equal(LeaveStatus.Pending, result.Value.Status);
        Assert.Equal(c.Org.Anna, result.Value.EmployeeId);
        Assert.Equal(c.Clock.UtcNow, result.Value.CreatedAtUtc);
        var stored = await c.Db.LeaveRequests.AsNoTracking().SingleAsync(l => l.Id == result.Value.Id);
        Assert.Equal(new DateOnly(2026, 10, 12), stored.From);
    }

    [Fact]
    public async Task UC06_Submit_skips_holiday()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).SubmitAsync(new(2026, 11, 9), new(2026, 11, 13));

        Assert.Equal(4, result.Value!.Days);
    }

    [Fact]
    public async Task UC07_Submit_over_balance_is_validation_error_with_rule()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.LeaveRequests.Add(Existing(c.Org.Anna, new(2026, 3, 2), new(2026, 3, 25), 18, LeaveStatus.Approved));
        await c.Db.SaveChangesAsync();

        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).SubmitAsync(new(2026, 10, 12), new(2026, 10, 16));

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
        Assert.Equal("KP-154-1", result.Error.RuleId);
        Assert.Equal(1, await c.Db.LeaveRequests.CountAsync());
    }

    [Fact]
    public async Task UC07_Pending_days_reduce_available_balance()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.LeaveRequests.Add(Existing(c.Org.Anna, new(2026, 3, 2), new(2026, 3, 24), 17, LeaveStatus.Pending));
        await c.Db.SaveChangesAsync();

        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).SubmitAsync(new(2026, 10, 12), new(2026, 10, 16));

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task UC06_Submit_weekend_only_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).SubmitAsync(new(2026, 10, 17), new(2026, 10, 18));

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
        Assert.Equal(0, await c.Db.LeaveRequests.CountAsync());
    }

    [Fact]
    public async Task UC06_Submit_across_years_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).SubmitAsync(new(2026, 12, 30), new(2027, 1, 5));

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task UC06_Submit_reversed_dates_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).SubmitAsync(new(2026, 10, 16), new(2026, 10, 12));

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task UC06_Submit_overlapping_existing_request_is_conflict()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.LeaveRequests.Add(Existing(c.Org.Anna, new(2026, 10, 12), new(2026, 10, 16), 5, LeaveStatus.Pending));
        await c.Db.SaveChangesAsync();

        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).SubmitAsync(new(2026, 10, 14), new(2026, 10, 20));

        Assert.Equal(ServiceErrorCode.Conflict, result.Error!.Code);
    }

    [Fact]
    public async Task UC06_Cancelled_or_rejected_request_does_not_block()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.LeaveRequests.Add(Existing(c.Org.Anna, new(2026, 10, 12), new(2026, 10, 16), 5, LeaveStatus.Cancelled));
        c.Db.LeaveRequests.Add(Existing(c.Org.Anna, new(2026, 10, 12), new(2026, 10, 16), 5, LeaveStatus.Rejected));
        await c.Db.SaveChangesAsync();

        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).SubmitAsync(new(2026, 10, 14), new(2026, 10, 20));

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Days);
    }

    [Fact]
    public async Task UC06_Concurrent_double_submit_creates_single_request()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var connectionString = c.Db.Database.GetConnectionString()!;
        var anna = c.As(c.Org.Anna, Roles.Employee);

        foreach (int week in new[] { 12, 19, 26, 2 })
        {
            var from = week >= 12 ? new DateOnly(2026, 1, week) : new DateOnly(2026, 2, week);
            var to = from.AddDays(4);
            await using var first = await fixture.OpenContextAsync(connectionString, anna, c.Clock);
            await using var second = await fixture.OpenContextAsync(connectionString, anna, c.Clock);
            var results = await Task.WhenAll(
                new LeaveService(first, anna, c.Clock, c.Calendar).SubmitAsync(from, to),
                new LeaveService(second, anna, c.Clock, c.Calendar).SubmitAsync(from, to));

            Assert.Equal(1, results.Count(r => r.IsSuccess));
            Assert.Equal(ServiceErrorCode.Conflict, results.Single(r => !r.IsSuccess).Error!.Code);
        }

        Assert.Equal(4, await c.Db.LeaveRequests.CountAsync());
    }

    [Fact]
    public async Task UC06_Other_employees_request_does_not_block()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.LeaveRequests.Add(Existing(c.Org.Jan, new(2026, 10, 12), new(2026, 10, 16), 5, LeaveStatus.Approved));
        await c.Db.SaveChangesAsync();

        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).SubmitAsync(new(2026, 10, 12), new(2026, 10, 16));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UC06_Submit_without_employee_link_is_forbidden()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Leave(new TestUser(Guid.NewGuid(), null, Roles.Employee)).SubmitAsync(new(2026, 10, 12), new(2026, 10, 16));

        Assert.Equal(ServiceErrorCode.Forbidden, result.Error!.Code);
    }
}
