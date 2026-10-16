using NetArchTest.Rules;

namespace Hr.Architecture.Tests;

public class AgentIsolationTests
{
    [Fact]
    public void Agent_depends_on_Core_only_through_AgentTools_namespace()
    {
        var result = Types.InAssembly(typeof(Hr.Agent.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Hr.Core.Domain", "Hr.Core.Persistence", "Hr.Core.Services", "Hr.Core.Security",
                "Hr.Core.Auditing", "Hr.Core.Seeding", "Hr.Core.Abstractions", "Hr.Rules", "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "Naruszenie: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Core_does_not_depend_on_Agent()
    {
        var result = Types.InAssembly(typeof(Hr.Core.AgentTools.IAgentToolbox).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Hr.Agent")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Rules_does_not_depend_on_Agent()
    {
        var result = Types.InAssembly(typeof(Hr.Rules.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Hr.Agent")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
