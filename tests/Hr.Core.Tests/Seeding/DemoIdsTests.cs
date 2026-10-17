using Hr.Core.Seeding.Demo;

namespace Hr.Core.Tests.Seeding;

public class DemoIdsTests
{
    [Fact]
    public void SeedFor_is_stable_and_differs_per_login_and_seed()
    {
        Assert.Equal(DemoIds.SeedFor(1, "anna.nowak"), DemoIds.SeedFor(1, "anna.nowak"));
        Assert.NotEqual(DemoIds.SeedFor(1, "anna.nowak"), DemoIds.SeedFor(1, "jan.kowalski"));
        Assert.NotEqual(DemoIds.SeedFor(1, "anna.nowak"), DemoIds.SeedFor(2, "anna.nowak"));
    }
}
