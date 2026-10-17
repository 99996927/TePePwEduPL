using System.Reflection;
using System.Text.RegularExpressions;
using Hr.Core.Seeding.Demo;
using Mono.Cecil;
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

        var matched = Types.InAssembly(typeof(DemoOptions).Assembly)
            .That().ResideInNamespaceMatching(DemoNamespacePattern)
            .GetTypes()
            .Select(t => t.Name)
            .ToList();
        Assert.Contains(nameof(DemoPeople), matched);
    }

    [Fact]
    public void Demo_types_are_used_only_by_DemoSeeder_apart_from_options_in_Api()
    {
        var production = new[]
        {
            typeof(DemoOptions).Assembly,
            typeof(Hr.Agent.AssemblyMarker).Assembly,
            typeof(Program).Assembly,
        };

        var violations = Violations(production, ForbiddenDemoTypes(), IsAllowedUser);

        Assert.True(violations.Count == 0, "Naruszenie: " + string.Join(", ", violations));
    }

    [Fact]
    public void Usage_rule_reports_a_type_that_references_a_Demo_type_and_ignores_a_clean_one()
    {
        var violations = Violations(
            new[] { typeof(DemoDataIsolationTests).Assembly },
            ForbiddenDemoTypes(),
            name => !name.StartsWith("Hr.Architecture.Tests.Synthetic.", StringComparison.Ordinal));

        Assert.Contains(violations, v => v.Contains(nameof(Synthetic.UsesDemoPeople)));
        Assert.DoesNotContain(violations, v => v.Contains(nameof(Synthetic.Clean)));
    }

    [Theory]
    [InlineData("Hr.Core.Seeding.DemoSeeder", true)]
    [InlineData("Hr.Core.Seeding.DemoSeeder+<SeedAsync>d__3", true)]
    [InlineData("Hr.Core.Seeding.DemoSeeder/<SeedAsync>d__3", true)]
    [InlineData("Hr.Core.Seeding.DemoSeeder+<>c__DisplayClass1_0", true)]
    [InlineData("Hr.Core.Seeding.Demo.DemoPeople", true)]
    [InlineData("Hr.Core.Seeding.DemoSeederHelper", false)]
    [InlineData("Hr.Core.Services.PayrollService", false)]
    public void Allowed_user_predicate_covers_DemoSeeder_with_nested_types(string fullName, bool allowed) =>
        Assert.Equal(allowed, IsAllowedUser(fullName));

    private static bool IsAllowedUser(string fullName) =>
        Regex.IsMatch(fullName, @"^Hr\.Core\.Seeding\.DemoSeeder([+/]|$)")
        || Regex.IsMatch(fullName, @"^Hr\.Core\.Seeding\.Demo[.]");

    /// <summary>Wszystkie typy najwyższego poziomu z przestrzeni Demo poza DemoOptions i DemoProfile (te wolno użyć w Hr.Api).</summary>
    private static string[] ForbiddenDemoTypes()
    {
        var publicSurface = new[] { nameof(DemoOptions), nameof(DemoProfile) };
        var names = typeof(DemoOptions).Assembly.GetTypes()
            .Where(t => t.Namespace == DemoNamespace && t.DeclaringType is null
                && !t.Name.StartsWith('<') && !publicSurface.Contains(t.Name))
            .Select(t => t.FullName!)
            .ToArray();
        Assert.Contains($"{DemoNamespace}.{nameof(DemoPeople)}", names);
        return names;
    }

    private static IReadOnlyList<string> Violations(
        IEnumerable<Assembly> assemblies, string[] forbidden, Func<string, bool> isAllowedUser)
    {
        var result = Types.InAssemblies(assemblies)
            .That().MeetCustomRule(new NotAllowedUserRule(isAllowedUser))
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        return result.FailingTypeNames ?? [];
    }

    private sealed class NotAllowedUserRule(Func<string, bool> isAllowedUser) : ICustomRule
    {
        public bool MeetsRule(TypeDefinition type) => !isAllowedUser(type.FullName);
    }
}
