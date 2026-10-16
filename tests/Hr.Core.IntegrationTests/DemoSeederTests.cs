using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Seeding;
using Hr.Rules.Leave;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class DemoSeederTests(MsSqlFixture fixture)
{
    private static readonly (string Login, string Password, string Role)[] Accounts =
    {
        ("anna.nowak", "annanowak123", Roles.Employee),
        ("jan.kowalski", "jankowalski123", Roles.Employee),
        ("tomasz.lis", "tomaszlis123", Roles.Employee),
        ("ewa.wisniewska", "ewawisniewska123", Roles.Manager),
        ("piotr.zielinski", "piotrzielinski123", Roles.HR),
        ("maria.kaminska", "mariakaminska123", Roles.HRApprover),
    };

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

    [Fact]
    public async Task UC29_Seed_creates_six_accounts_with_roles()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider);

        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(6, await users.Users.CountAsync());
        foreach (var (login, _, role) in Accounts)
        {
            var user = await users.FindByNameAsync(login);
            Assert.NotNull(user);
            Assert.NotNull(user!.EmployeeId);
            Assert.Equal(new[] { role }, await users.GetRolesAsync(user));
        }
    }

    [Fact]
    public async Task UC01_All_demo_passwords_are_valid()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider);

        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (login, password, _) in Accounts)
        {
            var user = (await users.FindByNameAsync(login))!;
            Assert.True(await users.CheckPasswordAsync(user, password), login);
            Assert.False(await users.CheckPasswordAsync(user, password + "x"), login);
        }
    }

    [Fact]
    public async Task UC29_Seed_is_idempotent()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider);
        await DemoSeeder.SeedAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        Assert.Equal(6, await db.Users.CountAsync());
        Assert.Equal(6, await db.Employees.CountAsync());
        Assert.Equal(3, await db.Teams.CountAsync());
        Assert.Equal(4, await db.Roles.CountAsync());
    }

    [Fact]
    public async Task UC03_Seed_places_tomasz_in_other_team_than_anna()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var anna = await db.Employees.SingleAsync(e => e.LastName == "Nowak");
        var tomasz = await db.Employees.SingleAsync(e => e.LastName == "Lis");
        var ewa = await db.Employees.SingleAsync(e => e.LastName == "Wiśniewska");
        var teamOfAnna = await db.Teams.SingleAsync(t => t.Id == anna.TeamId);
        Assert.NotEqual(anna.TeamId, tomasz.TeamId);
        Assert.Equal(ewa.Id, teamOfAnna.ManagerEmployeeId);
        Assert.Equal(ewa.TeamId, anna.TeamId);
    }

    [Fact]
    public async Task UC05_Jan_has_26_days_and_Anna_20()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var asOf = new DateOnly(2026, 10, 16);
        var jan = await db.Employees.SingleAsync(e => e.LastName == "Kowalski");
        var anna = await db.Employees.SingleAsync(e => e.LastName == "Nowak");
        Assert.Equal(26, LeaveEntitlement.AnnualDays(LeaveEntitlement.YearsOfService(jan.HireDate, asOf)));
        Assert.Equal(20, LeaveEntitlement.AnnualDays(LeaveEntitlement.YearsOfService(anna.HireDate, asOf)));
    }

    [Fact]
    public async Task UC02_Account_locks_after_five_failed_attempts()
    {
        await using var provider = await BuildAsync();
        await DemoSeeder.SeedAsync(provider);

        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByNameAsync("anna.nowak"))!;
        Assert.False(await users.IsLockedOutAsync(user));
        for (int i = 0; i < 5; i++)
        {
            await users.AccessFailedAsync(user);
        }

        Assert.True(await users.IsLockedOutAsync(user));
    }
}
