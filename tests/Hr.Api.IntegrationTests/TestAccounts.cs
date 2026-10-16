using Hr.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hr.Api.IntegrationTests;

public static class TestAccounts
{
    /// <summary>Dodaje konto powiązane z pracownikiem o podanym nazwisku i z podanymi rolami; hasło: login bez kropki + „123”.</summary>
    public static async Task<string> CreateAsync(HrApiFactory factory, string login, string employeeLastName, params string[] roles)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<Hr.Core.Persistence.HrDbContext>();
        var employeeId = await db.Employees.Where(e => e.LastName == employeeLastName).Select(e => e.Id).SingleAsync();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = login, Email = $"{login}@firma.example", EmployeeId = employeeId };
        Assert.True((await users.CreateAsync(user, ApiSession.PasswordOf(login))).Succeeded);
        Assert.True((await users.AddToRolesAsync(user, roles)).Succeeded);
        return login;
    }
}
