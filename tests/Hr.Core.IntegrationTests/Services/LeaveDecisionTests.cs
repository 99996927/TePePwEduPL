using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class LeaveDecisionTests(MsSqlFixture fixture)
{
    private static LeaveRequest Pending(Guid employeeId, DateOnly from, DateOnly to, int days, LeaveStatus status = LeaveStatus.Pending) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employeeId, From = from, To = to, Days = days, Status = status,
        CreatedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
    };

    private static async Task<LeaveRequest> AddAsync(ServiceTestContext c, Guid employeeId, LeaveStatus status = LeaveStatus.Pending)
    {
        var request = Pending(employeeId, new(2026, 10, 12), new(2026, 10, 16), 5, status);
        c.Db.LeaveRequests.Add(request);
        await c.Db.SaveChangesAsync();
        return request;
    }

    [Fact]
    public async Task UC09_Author_cancels_pending_request()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var request = await AddAsync(c, c.Org.Anna);

        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).CancelAsync(request.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(LeaveStatus.Cancelled, (await c.Db.LeaveRequests.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task UC09_Cannot_cancel_approved_request()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var request = await AddAsync(c, c.Org.Anna, LeaveStatus.Approved);

        var result = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).CancelAsync(request.Id);

        Assert.Equal(ServiceErrorCode.Conflict, result.Error!.Code);
    }

    [Fact]
    public async Task UC09_Other_employee_cannot_cancel()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var request = await AddAsync(c, c.Org.Anna);

        var result = await c.Leave(c.As(c.Org.Tomasz, Roles.Employee)).CancelAsync(request.Id);

        Assert.Equal(ServiceErrorCode.NotFound, result.Error!.Code);
        Assert.Equal(LeaveStatus.Pending, (await c.Db.LeaveRequests.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task UC08_Manager_approves_team_member_request_and_balance_drops()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var request = await AddAsync(c, c.Org.Anna);
        var ewa = c.As(c.Org.Ewa, Roles.Manager);

        var result = await c.Leave(ewa).DecideAsync(request.Id, approve: true, reason: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(LeaveStatus.Approved, result.Value!.Status);
        Assert.Equal(c.Org.Ewa, result.Value.DecidedByEmployeeId);
        var balance = (await c.Leave(ewa).GetBalanceAsync(c.Org.Anna)).Value!;
        Assert.Equal(5, balance.ApprovedDays);
        Assert.Equal(0, balance.PendingDays);
        Assert.Equal(15, balance.RemainingDays);
    }

    [Fact]
    public async Task UC08_Manager_rejects_with_reason_and_needs_one()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var request = await AddAsync(c, c.Org.Anna);
        var ewa = c.As(c.Org.Ewa, Roles.Manager);

        var withoutReason = await c.Leave(ewa).DecideAsync(request.Id, approve: false, reason: " ");
        Assert.Equal(ServiceErrorCode.Validation, withoutReason.Error!.Code);
        Assert.Equal(LeaveStatus.Pending, (await c.Db.LeaveRequests.AsNoTracking().SingleAsync()).Status);

        var result = await c.Leave(ewa).DecideAsync(request.Id, approve: false, reason: "Brak obsady w tym tygodniu");
        Assert.True(result.IsSuccess);
        Assert.Equal(LeaveStatus.Rejected, result.Value!.Status);
        Assert.Equal("Brak obsady w tym tygodniu", result.Value.DecisionReason);
    }

    [Fact]
    public async Task UC08_Manager_cannot_decide_other_team_request()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var request = await AddAsync(c, c.Org.Tomasz);

        var result = await c.Leave(c.As(c.Org.Ewa, Roles.Manager)).DecideAsync(request.Id, true, null);

        Assert.Equal(ServiceErrorCode.NotFound, result.Error!.Code);
    }

    [Fact]
    public async Task UC08_Manager_cannot_decide_own_request()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var request = await AddAsync(c, c.Org.Ewa);

        var result = await c.Leave(c.As(c.Org.Ewa, Roles.Manager)).DecideAsync(request.Id, true, null);

        Assert.Equal(ServiceErrorCode.Forbidden, result.Error!.Code);
        Assert.Equal(LeaveStatus.Pending, (await c.Db.LeaveRequests.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task UC08_Employee_cannot_decide()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var request = await AddAsync(c, c.Org.Anna);

        var asJan = await c.Leave(c.As(c.Org.Jan, Roles.Employee)).DecideAsync(request.Id, true, null);
        var asAnna = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).DecideAsync(request.Id, true, null);

        Assert.Equal(ServiceErrorCode.NotFound, asJan.Error!.Code);
        Assert.Equal(ServiceErrorCode.Forbidden, asAnna.Error!.Code);
    }

    [Fact]
    public async Task UC08_HR_can_decide_any_request_but_not_own()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var tomasz = await AddAsync(c, c.Org.Tomasz);
        var piotrs = Pending(c.Org.Piotr, new(2026, 11, 2), new(2026, 11, 6), 5);
        c.Db.LeaveRequests.Add(piotrs);
        await c.Db.SaveChangesAsync();
        var piotr = c.As(c.Org.Piotr, Roles.HR);

        Assert.True((await c.Leave(piotr).DecideAsync(tomasz.Id, true, null)).IsSuccess);
        Assert.Equal(ServiceErrorCode.Forbidden, (await c.Leave(piotr).DecideAsync(piotrs.Id, true, null)).Error!.Code);
    }

    [Fact]
    public async Task UC08_Decision_on_already_decided_request_is_conflict_and_does_not_change_balance()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var request = await AddAsync(c, c.Org.Anna);
        var ewa = c.As(c.Org.Ewa, Roles.Manager);

        Assert.True((await c.Leave(ewa).DecideAsync(request.Id, true, null)).IsSuccess);
        var second = await c.Leave(ewa).DecideAsync(request.Id, true, null);

        Assert.Equal(ServiceErrorCode.Conflict, second.Error!.Code);
        Assert.Equal(5, (await c.Leave(ewa).GetBalanceAsync(c.Org.Anna)).Value!.ApprovedDays);
    }

    [Fact]
    public async Task UC08_Approve_rechecks_balance()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        c.Db.LeaveRequests.Add(Pending(c.Org.Anna, new(2026, 3, 2), new(2026, 3, 24), 15, LeaveStatus.Approved));
        var request = await AddAsync(c, c.Org.Anna);
        c.Db.LeaveRequests.Add(Pending(c.Org.Anna, new(2026, 5, 4), new(2026, 5, 6), 3, LeaveStatus.Approved));
        await c.Db.SaveChangesAsync();

        var result = await c.Leave(c.As(c.Org.Ewa, Roles.Manager)).DecideAsync(request.Id, true, null);

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
        Assert.Equal("KP-154-1", result.Error.RuleId);
        Assert.Equal(LeaveStatus.Pending, (await c.Db.LeaveRequests.AsNoTracking().SingleAsync(l => l.Id == request.Id)).Status);
    }

    [Fact]
    public async Task UC03_List_returns_only_visible_requests_newest_first()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var annaOld = Pending(c.Org.Anna, new(2026, 3, 2), new(2026, 3, 6), 5);
        var annaNew = Pending(c.Org.Anna, new(2026, 10, 12), new(2026, 10, 16), 5);
        var jan = Pending(c.Org.Jan, new(2026, 6, 1), new(2026, 6, 5), 5);
        var tomasz = Pending(c.Org.Tomasz, new(2026, 8, 3), new(2026, 8, 7), 5);
        c.Db.LeaveRequests.AddRange(annaOld, annaNew, jan, tomasz);
        await c.Db.SaveChangesAsync();

        var asAnna = await c.Leave(c.As(c.Org.Anna, Roles.Employee)).ListAsync();
        var asEwa = await c.Leave(c.As(c.Org.Ewa, Roles.Manager)).ListAsync();
        var asHr = await c.Leave(c.As(c.Org.Piotr, Roles.HR)).ListAsync();

        Assert.Equal(new[] { annaNew.Id, annaOld.Id }, asAnna.Select(l => l.Id));
        Assert.Equal(new[] { annaNew.Id, jan.Id, annaOld.Id }, asEwa.Select(l => l.Id));
        Assert.Equal(new[] { annaNew.Id, tomasz.Id, jan.Id, annaOld.Id }, asHr.Select(l => l.Id));
    }
}
