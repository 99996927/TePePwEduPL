using System.Net.Http.Json;
using System.Text.Json;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class EmployeesApiTests(MsSqlFixture fixture)
{
    [Fact]
    public async Task Employees_include_userId_for_hr_for_all_and_for_employee_only_self()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var tomasz = await ApiSession.LoginAsAsync(factory, "tomasz.lis");
        Dictionary<Guid, Guid?> accounts;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
            accounts = (await db.Users.Where(u => u.EmployeeId != null).ToListAsync()).ToDictionary(u => u.EmployeeId!.Value, u => (Guid?)u.Id);
        }

        var all = (await (await piotr.GetAsync("/api/employees")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var own = (await (await tomasz.GetAsync("/api/employees")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        Assert.Equal(6, all.Count);
        Assert.All(all, e => Assert.Equal(accounts[e.GetProperty("id").GetGuid()], e.GetProperty("userId").GetGuid()));
        var self = Assert.Single(own);
        Assert.Equal(accounts[self.GetProperty("id").GetGuid()], self.GetProperty("userId").GetGuid());
    }
}
