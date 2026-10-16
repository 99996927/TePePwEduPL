using Hr.Core.Abstractions;
using Hr.Core.Services;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class EmployeeDirectoryTests(MsSqlFixture fixture)
{
    [Fact]
    public async Task EmployeeDirectory_returns_visible_employees_sorted_without_salary()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        var asAnna = await new EmployeeDirectoryService(c.Db, c.As(c.Org.Anna, Roles.Employee)).ListAsync();
        var asEwa = await new EmployeeDirectoryService(c.Db, c.As(c.Org.Ewa, Roles.Manager)).ListAsync();
        var asHr = await new EmployeeDirectoryService(c.Db, c.As(c.Org.Piotr, Roles.HR)).ListAsync();

        Assert.Equal(new[] { c.Org.Anna }, asAnna.Select(e => e.Id));
        Assert.Equal("Zespół Ewy", asAnna.Single().TeamName);
        Assert.Equal(new[] { "Kowalski", "Nowak", "Wiśniewska" }, asEwa.Select(e => e.LastName));
        Assert.Equal(new[] { "Kamińska", "Kowal", "Kowalski", "Lis", "Nowak", "Wiśniewska", "Zieliński" }, asHr.Select(e => e.LastName));
        Assert.DoesNotContain(typeof(EmployeeSummary).GetProperties(), p => p.Name.Contains("Salary", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EmployeeDirectory_is_empty_for_user_without_employee_link()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        var result = await new EmployeeDirectoryService(c.Db, new TestUser(Guid.NewGuid(), null, Roles.HR)).ListAsync();

        Assert.Empty(result);
    }
}
