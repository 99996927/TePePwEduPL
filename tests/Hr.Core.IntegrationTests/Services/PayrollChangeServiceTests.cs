using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class PayrollChangeServiceTests(MsSqlFixture fixture)
{
    private static readonly DateOnly Nov1 = new(2026, 11, 1);

    private static async Task<PayrollChange> NewDraftAsync(ServiceTestContext c, Guid employeeId, PayrollComponent component = PayrollComponent.BaseSalary, decimal value = 8500m, DateOnly? effective = null)
    {
        var result = await c.PayrollChanges(c.As(c.Org.Piotr, Roles.HR))
            .CreateAsync(employeeId, component, value, effective ?? Nov1, "Podwyżka roczna");
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private static async Task<PayrollChange> ApprovedAsync(ServiceTestContext c, Guid employeeId, PayrollComponent component, decimal value, DateOnly effective)
    {
        var draft = await NewDraftAsync(c, employeeId, component, value, effective);
        Assert.True((await c.PayrollChanges(c.As(c.Org.Piotr, Roles.HR)).SubmitAsync(draft.Id)).IsSuccess);
        var approved = await c.PayrollChanges(c.As(c.Org.Maria, Roles.HRApprover)).ApproveAsync(draft.Id);
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        return approved.Value!;
    }

    [Fact]
    public async Task UC18_Full_flow_hr_creates_submits_approver_approves()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var draft = await NewDraftAsync(c, c.Org.Anna);
        Assert.Equal(PayrollChangeStatus.Draft, draft.Status);
        Assert.Equal(c.UserIdOf(c.Org.Piotr), draft.CreatedByUserId);

        var submitted = await c.PayrollChanges(c.As(c.Org.Piotr, Roles.HR)).SubmitAsync(draft.Id);
        Assert.Equal(PayrollChangeStatus.Pending, submitted.Value!.Status);

        var approved = await c.PayrollChanges(c.As(c.Org.Maria, Roles.HRApprover)).ApproveAsync(draft.Id);
        Assert.Equal(PayrollChangeStatus.Approved, approved.Value!.Status);
        Assert.Equal(c.UserIdOf(c.Org.Maria), approved.Value.DecidedByUserId);
    }

    [Fact]
    public async Task UC18_Employee_and_manager_cannot_create()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        var asAnna = await c.PayrollChanges(c.As(c.Org.Anna, Roles.Employee)).CreateAsync(c.Org.Anna, PayrollComponent.BaseSalary, 9000m, Nov1, "Chcę podwyżkę");
        var asEwa = await c.PayrollChanges(c.As(c.Org.Ewa, Roles.Manager)).CreateAsync(c.Org.Anna, PayrollComponent.BaseSalary, 9000m, Nov1, "Podwyżka");

        Assert.Equal(ServiceErrorCode.Forbidden, asAnna.Error!.Code);
        Assert.Equal(ServiceErrorCode.Forbidden, asEwa.Error!.Code);
        Assert.Equal(0, await c.Db.PayrollChanges.CountAsync());
    }

    [Fact]
    public async Task UC18_Create_validates_value_reason_and_employee()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var piotr = c.PayrollChanges(c.As(c.Org.Piotr, Roles.HR));

        Assert.Equal(ServiceErrorCode.Validation, (await piotr.CreateAsync(c.Org.Anna, PayrollComponent.BaseSalary, -1m, Nov1, "Powód")).Error!.Code);
        Assert.Equal(ServiceErrorCode.Validation, (await piotr.CreateAsync(c.Org.Anna, PayrollComponent.BaseSalary, 9000m, Nov1, " ")).Error!.Code);
        Assert.Equal(ServiceErrorCode.NotFound, (await piotr.CreateAsync(Guid.NewGuid(), PayrollComponent.BaseSalary, 9000m, Nov1, "Powód")).Error!.Code);
    }

    [Fact]
    public async Task UC19_Author_cannot_approve_own_change()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var draft = await NewDraftAsync(c, c.Org.Anna);
        var both = new TestUser(c.UserIdOf(c.Org.Piotr), c.Org.Piotr, Roles.HR, Roles.HRApprover);
        await c.PayrollChanges(both).SubmitAsync(draft.Id);

        var result = await c.PayrollChanges(both).ApproveAsync(draft.Id);

        Assert.Equal(ServiceErrorCode.Forbidden, result.Error!.Code);
        Assert.Equal(PayrollChangeStatus.Pending, (await c.Db.PayrollChanges.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task UC19_HR_role_cannot_approve()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var draft = await NewDraftAsync(c, c.Org.Anna);
        var piotr = c.PayrollChanges(c.As(c.Org.Piotr, Roles.HR));
        await piotr.SubmitAsync(draft.Id);

        Assert.Equal(ServiceErrorCode.Forbidden, (await piotr.ApproveAsync(draft.Id)).Error!.Code);
    }

    [Fact]
    public async Task UC19_Draft_cannot_be_approved()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var draft = await NewDraftAsync(c, c.Org.Anna);

        var result = await c.PayrollChanges(c.As(c.Org.Maria, Roles.HRApprover)).ApproveAsync(draft.Id);

        Assert.Equal(ServiceErrorCode.Conflict, result.Error!.Code);
    }

    [Fact]
    public async Task UC19_Only_author_can_submit()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var draft = await NewDraftAsync(c, c.Org.Anna);
        var otherHr = new TestUser(Guid.NewGuid(), c.Org.Piotr, Roles.HR);

        var result = await c.PayrollChanges(otherHr).SubmitAsync(draft.Id);

        Assert.Equal(ServiceErrorCode.Forbidden, result.Error!.Code);
        Assert.Equal(PayrollChangeStatus.Draft, (await c.Db.PayrollChanges.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task UC20_Reject_requires_reason_and_stores_it()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var draft = await NewDraftAsync(c, c.Org.Anna);
        await c.PayrollChanges(c.As(c.Org.Piotr, Roles.HR)).SubmitAsync(draft.Id);
        var maria = c.PayrollChanges(c.As(c.Org.Maria, Roles.HRApprover));

        Assert.Equal(ServiceErrorCode.Validation, (await maria.RejectAsync(draft.Id, " ")).Error!.Code);
        var rejected = await maria.RejectAsync(draft.Id, "Brak budżetu");

        Assert.Equal(PayrollChangeStatus.Rejected, rejected.Value!.Status);
        Assert.Equal("Brak budżetu", rejected.Value.DecisionReason);
        Assert.Equal(c.UserIdOf(c.Org.Maria), rejected.Value.DecidedByUserId);
    }

    [Fact]
    public async Task UC20_Decided_change_cannot_be_decided_again()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var approved = await ApprovedAsync(c, c.Org.Anna, PayrollComponent.BaseSalary, 8500m, Nov1);
        var maria = c.PayrollChanges(c.As(c.Org.Maria, Roles.HRApprover));

        Assert.Equal(ServiceErrorCode.Conflict, (await maria.ApproveAsync(approved.Id)).Error!.Code);
        Assert.Equal(ServiceErrorCode.Conflict, (await maria.RejectAsync(approved.Id, "Powód")).Error!.Code);
    }

    [Fact]
    public async Task UC18_Resolver_ignores_unapproved_and_respects_effective_date()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var draft = await NewDraftAsync(c, c.Org.Anna);
        await c.PayrollChanges(c.As(c.Org.Piotr, Roles.HR)).SubmitAsync(draft.Id);

        Assert.Equal(8000m, await SalaryResolver.ResolveAsync(c.Db, c.Org.Anna, PayrollComponent.BaseSalary, new(2026, 11, 15)));

        await c.PayrollChanges(c.As(c.Org.Maria, Roles.HRApprover)).ApproveAsync(draft.Id);

        Assert.Equal(8000m, await SalaryResolver.ResolveAsync(c.Db, c.Org.Anna, PayrollComponent.BaseSalary, new(2026, 10, 31)));
        Assert.Equal(8500m, await SalaryResolver.ResolveAsync(c.Db, c.Org.Anna, PayrollComponent.BaseSalary, Nov1));
    }

    [Fact]
    public async Task UC18_Resolver_picks_latest_effective_change()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await ApprovedAsync(c, c.Org.Anna, PayrollComponent.BaseSalary, 8500m, new(2026, 11, 1));
        await ApprovedAsync(c, c.Org.Anna, PayrollComponent.BaseSalary, 9000m, new(2026, 12, 1));

        Assert.Equal(9000m, await SalaryResolver.ResolveAsync(c.Db, c.Org.Anna, PayrollComponent.BaseSalary, new(2026, 12, 15)));
        Assert.Equal(8500m, await SalaryResolver.ResolveAsync(c.Db, c.Org.Anna, PayrollComponent.BaseSalary, new(2026, 11, 15)));
    }

    [Fact]
    public async Task UC18_Resolver_fixed_allowance_defaults_to_zero_and_follows_changes()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        Assert.Equal(0m, await SalaryResolver.ResolveAsync(c.Db, c.Org.Anna, PayrollComponent.FixedAllowance, new(2026, 10, 31)));

        await ApprovedAsync(c, c.Org.Anna, PayrollComponent.FixedAllowance, 300m, new(2026, 10, 1));

        Assert.Equal(300m, await SalaryResolver.ResolveAsync(c.Db, c.Org.Anna, PayrollComponent.FixedAllowance, new(2026, 10, 31)));
    }

    [Fact]
    public async Task UC03_Employee_and_manager_see_only_approved_changes_in_their_scope()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var approved = await ApprovedAsync(c, c.Org.Anna, PayrollComponent.BaseSalary, 8500m, Nov1);
        await NewDraftAsync(c, c.Org.Anna, PayrollComponent.FixedAllowance, 300m);
        await NewDraftAsync(c, c.Org.Tomasz);

        var asAnna = await c.PayrollChanges(c.As(c.Org.Anna, Roles.Employee)).ListAsync();
        var asEwa = await c.PayrollChanges(c.As(c.Org.Ewa, Roles.Manager)).ListAsync();
        var asTomasz = await c.PayrollChanges(c.As(c.Org.Tomasz, Roles.Employee)).ListAsync();

        Assert.Equal(new[] { approved.Id }, asAnna.Select(x => x.Id));
        Assert.Equal(new[] { approved.Id }, asEwa.Select(x => x.Id));
        Assert.Empty(asTomasz);
    }

    [Fact]
    public async Task UC03_List_scope()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var anna = await NewDraftAsync(c, c.Org.Anna);
        var jan = await NewDraftAsync(c, c.Org.Jan);
        var tomasz = await NewDraftAsync(c, c.Org.Tomasz);

        var asAnna = await c.PayrollChanges(c.As(c.Org.Anna, Roles.Employee)).ListAsync();
        var asEwa = await c.PayrollChanges(c.As(c.Org.Ewa, Roles.Manager)).ListAsync();
        var asHr = await c.PayrollChanges(c.As(c.Org.Piotr, Roles.HR)).ListAsync();
        var asApprover = await c.PayrollChanges(c.As(c.Org.Maria, Roles.HRApprover)).ListAsync();

        Assert.Empty(asAnna);
        Assert.Empty(asEwa);
        Assert.Equal(3, asHr.Count);
        Assert.Equal(3, asApprover.Count);
        Assert.Contains(tomasz.Id, asHr.Select(x => x.Id));
        Assert.Contains(jan.Id, asApprover.Select(x => x.Id));
        Assert.Contains(anna.Id, asHr.Select(x => x.Id));
        Assert.Single(await c.PayrollChanges(c.As(c.Org.Piotr, Roles.HR)).ListAsync(c.Org.Anna));
    }
}
