using NetArchTest.Rules;

namespace Hr.Architecture.Tests;

public class RulesIsolationTests
{
    [Fact]
    public void Rules_does_not_depend_on_io_data_or_http()
    {
        var result = Types.InAssembly(typeof(Hr.Rules.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "System.Net.Http", "System.Data", "System.IO", "Anthropic")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
