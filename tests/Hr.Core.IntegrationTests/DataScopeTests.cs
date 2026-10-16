using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class DataScopeTests(MsSqlFixture fixture)
{
    private static TestUser As(Guid? employeeId, params string[] roles) => new(Guid.NewGuid(), employeeId, roles);

    private static async Task<HashSet<Guid>> VisibleLeaveOwners(Hr.Core.Persistence.HrDbContext db, ICurrentUser user) =>
        (await db.LeaveRequests.Visible(db, user).Select(l => l.EmployeeId).ToListAsync()).ToHashSet();

    [Fact]
    public async Task UC03_Employee_sees_only_own_leave()
    {
        await using var db = await fixture.NewContextAsync();
        var org = await TestOrg.SeedAsync(db);

        Assert.Equal(new HashSet<Guid> { org.Anna }, await VisibleLeaveOwners(db, As(org.Anna, Roles.Employee)));
        Assert.Equal(new HashSet<Guid> { org.Tomasz }, await VisibleLeaveOwners(db, As(org.Tomasz, Roles.Employee)));
    }

    [Fact]
    public async Task UC03_Employee_cannot_load_other_employees_leave_by_id()
    {
        await using var db = await fixture.NewContextAsync();
        var org = await TestOrg.SeedAsync(db);
        var annasLeave = org.LeaveByEmployee[org.Anna];

        var asTomasz = await db.LeaveRequests.Visible(db, As(org.Tomasz, Roles.Employee)).FirstOrDefaultAsync(l => l.Id == annasLeave);
        Assert.Null(asTomasz);
    }

    [Fact]
    public async Task UC03_Manager_sees_own_team_but_not_other_team()
    {
        await using var db = await fixture.NewContextAsync();
        var org = await TestOrg.SeedAsync(db);

        var visible = await VisibleLeaveOwners(db, As(org.Ewa, Roles.Manager));
        Assert.Equal(new HashSet<Guid> { org.Anna, org.Jan, org.Ewa }, visible);
        Assert.DoesNotContain(org.Tomasz, visible);
    }

    [Fact]
    public async Task UC03_Manager_of_other_team_does_not_see_first_team()
    {
        await using var db = await fixture.NewContextAsync();
        var org = await TestOrg.SeedAsync(db);

        var visible = await VisibleLeaveOwners(db, As(org.Bartek, Roles.Manager));
        Assert.Equal(new HashSet<Guid> { org.Tomasz, org.Bartek }, visible);
    }

    [Fact]
    public async Task UC04_HR_and_approver_see_everyone()
    {
        await using var db = await fixture.NewContextAsync();
        var org = await TestOrg.SeedAsync(db);

        Assert.Equal(org.AllEmployees.ToHashSet(), await VisibleLeaveOwners(db, As(org.Piotr, Roles.HR)));
        Assert.Equal(org.AllEmployees.ToHashSet(), await VisibleLeaveOwners(db, As(org.Maria, Roles.HRApprover)));
        Assert.Equal(org.AllEmployees.Count, await db.VisibleEmployees(As(org.Piotr, Roles.HR)).CountAsync());
    }

    [Fact]
    public async Task UC03_User_without_employee_link_sees_nothing()
    {
        await using var db = await fixture.NewContextAsync();
        await TestOrg.SeedAsync(db);

        Assert.Empty(await VisibleLeaveOwners(db, As(null, Roles.HR)));
        Assert.Empty(await db.VisibleEmployees(As(null, Roles.HR)).ToListAsync());
    }

    [Fact]
    public async Task UC03_Unknown_role_sees_nothing()
    {
        await using var db = await fixture.NewContextAsync();
        var org = await TestOrg.SeedAsync(db);

        Assert.Empty(await VisibleLeaveOwners(db, As(org.Anna, "Intern")));
        Assert.Empty(await VisibleLeaveOwners(db, As(org.Anna)));
    }

    [Fact]
    public async Task UC03_Scope_applies_to_work_entries_and_payroll_changes()
    {
        await using var db = await fixture.NewContextAsync();
        var org = await TestOrg.SeedAsync(db);
        db.PayrollChanges.Add(new PayrollChange
        {
            Id = Guid.NewGuid(), EmployeeId = org.Anna, Component = PayrollComponent.BaseSalary, Value = 8500m,
            EffectiveFrom = new DateOnly(2026, 11, 1), Reason = "Podwyżka", Status = PayrollChangeStatus.Pending,
            CreatedByUserId = Guid.NewGuid(), CreatedAtUtc = new DateTime(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();

        Assert.Single(await db.WorkEntries.Visible(db, As(org.Anna, Roles.Employee)).ToListAsync());
        Assert.Single(await db.PayrollChanges.Visible(db, As(org.Anna, Roles.Employee)).ToListAsync());
        Assert.Empty(await db.PayrollChanges.Visible(db, As(org.Tomasz, Roles.Employee)).ToListAsync());
        Assert.Single(await db.PayrollChanges.Visible(db, As(org.Ewa, Roles.Manager)).ToListAsync());
    }

    [Fact]
    public async Task UC03_Scope_is_translated_to_sql()
    {
        await using var db = await fixture.NewContextAsync();
        var org = await TestOrg.SeedAsync(db);

        var query = db.LeaveRequests.Visible(db, As(org.Anna, Roles.Employee));
        Assert.Contains("WHERE", query.ToQueryString());
        Assert.Single(await query.ToListAsync());
    }
}
