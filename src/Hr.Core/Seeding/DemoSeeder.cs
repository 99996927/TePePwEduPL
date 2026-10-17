using System.Diagnostics;
using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Seeding.Demo;
using Hr.Core.Services;
using Hr.Rules.Calendar;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hr.Core.Seeding;

/// <summary>
/// Zapisuje zbiór z <see cref="DemoDataGenerator"/>: role, zespoły, pracownicy, dane i konta w jednej transakcji, gdy brak
/// konta <c>anna.nowak</c>; dla profilu <see cref="DemoProfile.Full"/> potem naliczenia przez <see cref="PayrollService"/>
/// dla każdego miesiąca z <see cref="DemoOptions.PayrollMonths"/> bez wyników. Nigdy nie nadpisuje ani nie usuwa danych.
/// </summary>
public static class DemoSeeder
{
    private const string MarkerLogin = "anna.nowak";
    private const string PayrollLogin = "piotr.zielinski";

    public static async Task SeedAsync(IServiceProvider services, DemoOptions options, CancellationToken ct = default)
    {
        var clock = services.GetService<IClock>() ?? new SystemClock();
        var calendar = services.GetService<WorkCalendar>() ?? new WorkCalendar();
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger(typeof(DemoSeeder).FullName!) ?? NullLogger.Instance;

        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Seed danych demo: start (profil {Profile}, kotwica {Anchor:yyyy-MM-dd}, ziarno {Seed}).",
            options.Profile, options.AnchorDate, options.Seed);

        var data = DemoDataGenerator.Build(options, calendar);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            foreach (var role in Roles.All)
            {
                if (!await roles.RoleExistsAsync(role))
                {
                    Check(await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() }), role);
                }
            }

            if (await users.FindByNameAsync(MarkerLogin) is null)
            {
                await SeedDataAsync(db, users, data, ct);
                logger.LogInformation(
                    "Seed danych demo: zapisano {Teams} zespołów, {People} osób i kont, {Leave} wniosków urlopowych, "
                    + "{Entries} wpisów czasu, {Changes} zmian płacowych, {Conversations} rozmów.",
                    data.Teams.Count, data.People.Count, data.LeaveRequests.Count, data.WorkEntries.Count,
                    data.PayrollChanges.Count, data.Conversations.Count);
            }
            else
            {
                logger.LogInformation("Seed danych demo: konto {Login} istnieje, pomijam zapis danych.", MarkerLogin);
            }

            await transaction.CommitAsync(ct);
        }

        if (options.Profile == DemoProfile.Full)
        {
            await SeedPayrollAsync(db, data, options, clock, calendar, logger, ct);
        }

        logger.LogInformation("Seed danych demo: koniec po {Elapsed} ms.", stopwatch.ElapsedMilliseconds);
    }

    private static async Task SeedDataAsync(
        HrDbContext db, UserManager<ApplicationUser> users, DemoDataSet data, CancellationToken ct)
    {
        // Team.ManagerEmployeeId wskazuje na Employee, a Employee.TeamId na Team: kierowników ustawiamy w drugim kroku.
        var teams = data.Teams.Select(t => new Team { Id = t.Id, Name = t.Name }).ToList();
        db.Teams.AddRange(teams);
        await db.SaveChangesAsync(ct);

        var teamIds = data.Teams.ToDictionary(t => t.Name, t => t.Id);
        db.Employees.AddRange(data.People.Select(p => new Employee
        {
            Id = p.EmployeeId, FirstName = p.First, LastName = p.Last,
            HireDate = p.HireDate, TeamId = teamIds[p.TeamName], MonthlySalary = p.MonthlySalary,
        }));
        await db.SaveChangesAsync(ct);

        foreach (var team in teams)
        {
            team.ManagerEmployeeId = data.Teams.Single(t => t.Id == team.Id).ManagerEmployeeId;
        }

        db.LeaveRequests.AddRange(data.LeaveRequests);
        db.WorkEntries.AddRange(data.WorkEntries);
        db.PayrollChanges.AddRange(data.PayrollChanges);
        db.Conversations.AddRange(data.Conversations);
        await db.SaveChangesAsync(ct);

        // Konta zapisuje UserManager na tym samym kontekście (ta sama transakcja); tysiące śledzonych wpisów tylko by go spowalniały.
        db.ChangeTracker.Clear();
        foreach (var p in data.People)
        {
            var user = new ApplicationUser
            {
                Id = p.UserId, UserName = p.Login, Email = $"{p.Login}@firma.example", EmailConfirmed = true,
                EmployeeId = p.EmployeeId,
            };
            Check(await users.CreateAsync(user, p.Password), p.Login);
            Check(await users.AddToRoleAsync(user, p.Role), p.Login);
        }
    }

    private static async Task SeedPayrollAsync(
        HrDbContext db, DemoDataSet data, DemoOptions options, IClock clock, WorkCalendar calendar, ILogger logger,
        CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var piotr = data.People.Single(p => p.Login == PayrollLogin);
        var employeeIds = data.People.Select(p => p.EmployeeId).ToList();
        var present = await db.Employees.CountAsync(e => employeeIds.Contains(e.Id), ct);
        if (present != employeeIds.Count || !await db.Users.AnyAsync(u => u.Id == piotr.UserId, ct))
        {
            // Np. dawna baza z sześcioma kontami: bez ewidencji naliczenia byłyby puste, więc ich nie tworzymy.
            logger.LogInformation(
                "Seed danych demo: w bazie brak pełnego zbioru osób profilu Full ({Present}/{Expected}), pomijam naliczenia.",
                present, employeeIds.Count);
            return;
        }

        var payroll = new PayrollService(db, new SeedCurrentUser(piotr.UserId, piotr.EmployeeId), clock, calendar);
        foreach (var (year, month) in options.PayrollMonths)
        {
            if (await db.PayrollResults.AnyAsync(r => r.Year == year && r.Month == month, ct))
            {
                continue;
            }

            var result = await payroll.RunMonthAsync(year, month, ct);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Seed naliczeń za {year}-{month:00} nie powiódł się: {result.Error!.Message}");
            }

            logger.LogInformation(
                "Seed danych demo: naliczenia za {Year}-{Month:00} dla {Count} pracowników.", year, month, result.Value!.Count);
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
