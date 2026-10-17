using Hr.Core.Seeding.Demo;

namespace Hr.Architecture.Tests.Synthetic;

// Typy pomocnicze wyłącznie dla testu negatywnego reguły użycia przestrzeni Demo.
internal static class UsesDemoPeople
{
    public static int Count() => DemoPeople.Build(DemoOptions.Default).Item2.Count;
}

internal static class Clean
{
    public static int One() => 1;
}
