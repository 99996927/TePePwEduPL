using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hr.Core.Seeding;

/// <summary>Role, zespoły, pracownicy i 6 kont demo z docs/arc42/07-widok-wdrozenia.md. Idempotentny (po nazwach i loginach).</summary>
public static class DemoSeeder
{
    private const string TeamEwa = "Zespół Ewy";
    private const string TeamB = "Zespół B";
    private const string TeamHr = "Kadry";

    private sealed record Person(string First, string Last, string Login, string Role, string Team, DateOnly Hired, decimal Salary)
    {
        public string Password => Login.Replace(".", string.Empty) + "123";
    }

    private static readonly Person[] People =
    {
        new("Anna", "Nowak", "anna.nowak", Roles.Employee, TeamEwa, new DateOnly(2019, 3, 1), 7200m),
        new("Jan", "Kowalski", "jan.kowalski", Roles.Employee, TeamEwa, new DateOnly(2014, 5, 1), 8000m),
        new("Tomasz", "Lis", "tomasz.lis", Roles.Employee, TeamB, new DateOnly(2022, 9, 1), 6800m),
        new("Ewa", "Wiśniewska", "ewa.wisniewska", Roles.Manager, TeamEwa, new DateOnly(2012, 1, 2), 11000m),
        new("Piotr", "Zieliński", "piotr.zielinski", Roles.HR, TeamHr, new DateOnly(2018, 6, 1), 9000m),
        new("Maria", "Kamińska", "maria.kaminska", Roles.HRApprover, TeamHr, new DateOnly(2015, 2, 1), 12000m),
    };

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                Check(await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() }), role);
            }
        }

        var teams = new Dictionary<string, Team>();
        foreach (var name in new[] { TeamEwa, TeamB, TeamHr })
        {
            teams[name] = await db.Teams.FirstOrDefaultAsync(t => t.Name == name, ct) ?? db.Teams.Add(new Team { Id = Guid.NewGuid(), Name = name }).Entity;
        }

        await db.SaveChangesAsync(ct);

        var employees = new Dictionary<string, Employee>();
        foreach (var p in People)
        {
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.FirstName == p.First && e.LastName == p.Last, ct)
                ?? db.Employees.Add(new Employee
                {
                    Id = Guid.NewGuid(), FirstName = p.First, LastName = p.Last,
                    HireDate = p.Hired, TeamId = teams[p.Team].Id, MonthlySalary = p.Salary,
                }).Entity;
            employees[p.Login] = employee;
        }

        await db.SaveChangesAsync(ct);

        teams[TeamEwa].ManagerEmployeeId = employees["ewa.wisniewska"].Id;
        await db.SaveChangesAsync(ct);

        foreach (var p in People)
        {
            if (await users.FindByNameAsync(p.Login) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = p.Login, Email = $"{p.Login}@firma.example", EmailConfirmed = true,
                EmployeeId = employees[p.Login].Id,
            };
            Check(await users.CreateAsync(user, p.Password), p.Login);
            Check(await users.AddToRoleAsync(user, p.Role), p.Login);
        }
    }

    private static void Check(IdentityResult result, string subject)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Seed nie powiódł się dla '{subject}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
