using System.Reflection;
using Hr.Core.Seeding.Demo;
using NetArchTest.Rules;

namespace Hr.Architecture.Tests;

public class DemoDataIsolationTests
{
    private const string DemoNamespace = "Hr.Core.Seeding.Demo";

    // ResideInNamespace dopasowuje prefiks nazwy, więc objęłoby też Hr.Core.Seeding.DemoSeeder.
    private const string DemoNamespacePattern = @"^Hr\.Core\.Seeding\.Demo(\.|$)";

    [Fact]
    public void Demo_data_layer_does_not_depend_on_persistence_clock_or_identity()
    {
        var result = Types.InAssembly(typeof(DemoOptions).Assembly)
            .That().ResideInNamespaceMatching(DemoNamespacePattern)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Hr.Core.Persistence", "Microsoft.EntityFrameworkCore",
                "Hr.Core.Abstractions.IClock", "Microsoft.AspNetCore.Identity")
            .GetResult();

        Assert.True(result.IsSuccessful, "Naruszenie: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Demo_types_are_used_only_by_DemoSeeder_apart_from_options_in_Api()
    {
        var publicSurface = new[] { nameof(DemoOptions), nameof(DemoProfile) };
        var demoTypes = typeof(DemoOptions).Assembly.GetTypes()
            .Where(t => t.Namespace == DemoNamespace && t.DeclaringType is null
                && !t.Name.StartsWith('<') && !publicSurface.Contains(t.Name))
            .Select(t => t.FullName!)
            .ToList();
        Assert.Contains($"{DemoNamespace}.{nameof(DemoPeople)}", demoTypes);

        var production = new[]
        {
            typeof(DemoOptions).Assembly,
            typeof(Hr.Agent.AssemblyMarker).Assembly,
            typeof(Program).Assembly,
        };

        var result = Types.InAssemblies(production)
            .That().DoNotResideInNamespaceMatching(DemoNamespacePattern)
            .And().DoNotHaveName("DemoSeeder")
            .ShouldNot()
            .HaveDependencyOnAny(demoTypes.ToArray())
            .GetResult();

        Assert.True(result.IsSuccessful, "Naruszenie: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
