using System.Diagnostics;
using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Security;
using Hr.Core.Seeding;
using Hr.Core.Seeding.Demo;
using Hr.Rules.Calendar;
using Hr.Rules.Overtime;
using Hr.Rules.Payroll;
using Hr.Rules.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class DemoDataSeedTests(MsSqlFixture fixture)
{
    private static readonly DemoOptions Full = DemoOptions.Default;

    private async Task<ServiceProvider> BuildAsync()
    {
        var connectionString = await fixture.NewMigratedDatabaseAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<HrDbContext>(o => o.UseSqlServer(connectionString));
        services.AddIdentityCore<ApplicationUser>(HrIdentityOptions.Apply)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<HrDbContext>();
        return services.BuildServiceProvider();
    }

    private sealed record Counts(int Employees, int WorkEntries, int LeaveRequests, int PayrollChanges, int Conversations, int Messages, int PayrollResults, int Users);

    private static async Task<Counts> CountAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        return new Counts(
            await db.Employees.CountAsync(), await db.WorkEntries.CountAsync(), await db.LeaveRequests.CountAsync(),
            await db.PayrollChanges.CountAsync(), await db.Conversations.CountAsync(), await db.Messages.CountAsync(),
            await db.PayrollResults.CountAsync(), await db.Users.CountAsync());
    }

    [Fact]
    public async Task Full_seed_creates_31_people_accounts_five_teams_and_ends_in_under_60_seconds()
    {
        await using var provider = await BuildAsync();
        var stopwatch = Stopwatch.StartNew();
        await DemoSeeder.SeedAsync(provider, Full);
        stopwatch.Stop();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        Assert.Equal(31, await db.Employees.CountAsync());
        Assert.Equal(31, await db.Users.CountAsync(u => u.EmployeeId != null));
        Assert.Equal(5, await db.Teams.CountAsync());
        Assert.True(await db.WorkEntries.AnyAsync());
        Assert.True(await db.LeaveRequests.AnyAsync());
        Assert.True(await db.Conversations.AnyAsync());
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"Seed trwał {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task Seed_is_idempotent_and_never_duplicates_data()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider, Full);
        var first = await CountAsync(provider);
        await DemoSeeder.SeedAsync(provider, Full);

        Assert.Equal(first, await CountAsync(provider));
        Assert.Equal(31 * 6, first.PayrollResults);
    }

    [Fact]
    public async Task Payroll_exists_for_april_to_september_for_every_employee_and_not_for_october()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider, Full);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var piotr = await db.Users.SingleAsync(u => u.UserName == "piotr.zielinski");
        var employeeIds = await db.Employees.Select(e => e.Id).ToListAsync();
        var results = await db.PayrollResults.ToListAsync();

        Assert.Equal(31 * 6, results.Count);
        foreach (var month in Enumerable.Range(4, 6))
        {
            Assert.Equal(employeeIds.ToHashSet(), results.Where(r => r.Year == 2026 && r.Month == month).Select(r => r.EmployeeId).ToHashSet());
        }

        Assert.DoesNotContain(results, r => r.Year == 2026 && r.Month == 10);
        Assert.All(results, r => Assert.Equal(piotr.Id, r.CalculatedByUserId));
    }

    [Fact]
    public async Task Payroll_matches_calculator_for_annas_september_and_reflects_approved_change()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider, Full);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var calendar = new WorkCalendar();
        var anna = await db.Employees.SingleAsync(e => e.LastName == "Nowak");
        var start = new DateOnly(2026, 9, 1);
        var end = new DateOnly(2026, 9, 30);
        var entries = await db.WorkEntries
            .Where(w => w.EmployeeId == anna.Id && w.Start >= new DateTime(2026, 9, 1) && w.Start < new DateTime(2026, 10, 1))
            .ToListAsync();
        var overtime = OvertimeCalculator.Calculate(entries.Select(w => w.ToRulesEntry()).ToList(), calendar, NightWindow.Default);
        var expected = PayrollCalculator.Calculate(new PayrollInput(
            anna.MonthlySalary, WorkingTimeNorm.HoursForPeriod(start, end, calendar), overtime.Hours50, overtime.Hours100, 0m));

        var actual = await db.PayrollResults.SingleAsync(r => r.EmployeeId == anna.Id && r.Year == 2026 && r.Month == 9);
        Assert.NotEmpty(entries);
        Assert.Equal(7200m, actual.BaseSalary);
        Assert.Equal(expected.Gross, actual.Gross);
        Assert.Equal(expected.NormalOvertimePay, actual.NormalOvertimePay);
        Assert.Equal(expected.Allowance50, actual.Allowance50);
        Assert.Equal(expected.Allowance100, actual.Allowance100);
        Assert.Equal(overtime.Hours50, actual.Overtime50Hours);
        Assert.Equal(overtime.Hours100, actual.Overtime100Hours);

        var jan = await db.Employees.SingleAsync(e => e.LastName == "Kowalski");
        var janAllowance = await db.PayrollResults.Where(r => r.EmployeeId == jan.Id && r.Year == 2026)
            .ToDictionaryAsync(r => r.Month, r => r.FixedAllowance);
        Assert.Equal(0m, janAllowance[4]);
        Assert.Equal(0m, janAllowance[5]);
        foreach (var month in Enumerable.Range(6, 4))
        {
            Assert.Equal(400m, janAllowance[month]);
        }
    }

    [Fact]
    public async Task Seed_completes_missing_payroll_after_crash_between_data_and_payroll()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider, Full);
        var before = await CountAsync(provider);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
            await db.PayrollResults.ExecuteDeleteAsync();
        }

        await DemoSeeder.SeedAsync(provider, Full);

        Assert.Equal(before, await CountAsync(provider));
    }

    [Fact]
    public async Task Moved_anchor_on_existing_database_creates_no_payroll_for_months_without_entries()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider, Full);
        var before = await CountAsync(provider);

        await DemoSeeder.SeedAsync(provider, Full with { AnchorDate = new DateOnly(2027, 3, 12) });

        // Dane nie są dosiewane; październik 2026 ma wpisy (kotwica domyślna to 16.10), więc jego naliczenia mogą powstać, listopad 2026 - luty 2027 nie.
        Assert.Equal(before with { PayrollResults = before.PayrollResults + 31 }, await CountAsync(provider));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        Assert.False(await db.PayrollResults.AnyAsync(r => r.Year == 2026 && r.Month >= 11 || r.Year == 2027));
    }

    [Fact]
    public async Task Seed_does_not_overwrite_existing_old_six_account_database()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider, Full with { Profile = DemoProfile.Accounts });

        await DemoSeeder.SeedAsync(provider, Full);

        var counts = await CountAsync(provider);
        Assert.Equal(6, counts.Employees);
        Assert.Equal(6, counts.Users);
        Assert.Equal(0, counts.WorkEntries);
        Assert.Equal(0, counts.PayrollResults);
    }

    [Fact]
    public async Task Failed_account_creation_rolls_back_whole_data_seed()
    {
        await using var provider = await BuildAsync();
        using (var scope = provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var taken = new ApplicationUser { Id = Guid.NewGuid(), UserName = "jan.kowalski", Email = "inne.konto@firma.example" };
            Assert.True((await users.CreateAsync(taken, "zajete123")).Succeeded);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSeeder.SeedAsync(provider, Full));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
            Assert.Equal(0, await db.Teams.CountAsync());
            Assert.Equal(0, await db.Employees.CountAsync());
            Assert.Equal(0, await db.WorkEntries.CountAsync());
            Assert.Equal(0, await db.PayrollResults.CountAsync());
            Assert.Equal(1, await db.Users.CountAsync());
        }
    }

    [Fact]
    public async Task Every_generated_account_can_sign_in_with_its_password_pattern()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider, Full);

        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var all = await users.Users.ToListAsync();
        Assert.Equal(31, all.Count);
        foreach (var user in all)
        {
            var password = user.UserName!.Replace(".", string.Empty) + "123";
            Assert.True(await users.CheckPasswordAsync(user, password), user.UserName);
            Assert.Single(await users.GetRolesAsync(user));
        }
    }

    [Fact]
    public async Task UC03_Tomasz_does_not_see_Annas_entries_through_data_scope()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider, Full);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var anna = await db.Employees.SingleAsync(e => e.LastName == "Nowak");
        var tomaszUser = await db.Users.SingleAsync(u => u.UserName == "tomasz.lis");
        var tomasz = new TestUser(tomaszUser.Id, tomaszUser.EmployeeId, Roles.Employee);

        var visibleOwners = await db.WorkEntries.Visible(db, tomasz).Select(w => w.EmployeeId).Distinct().ToListAsync();

        Assert.True(await db.WorkEntries.AnyAsync(w => w.EmployeeId == anna.Id));
        Assert.Equal(new[] { tomaszUser.EmployeeId!.Value }, visibleOwners);
        Assert.False(await db.WorkEntries.Visible(db, tomasz).AnyAsync(w => w.EmployeeId == anna.Id));
    }
}
