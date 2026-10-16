using Hr.Core.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hr.Api.IntegrationTests;

/// <summary>Aplikacja na świeżej bazie SQL Server (migracje i konta demo zakłada start aplikacji), zegar stały: 2026-10-16 12:00 UTC.</summary>
public sealed class HrApiFactory(string connectionString, string environment = "Development", bool seedDemoData = true)
    : WebApplicationFactory<Program>
{
    public FakeClock Clock { get; } = new();

    public static async Task<HrApiFactory> CreateAsync(MsSqlFixture fixture, string environment = "Development", bool seedDemoData = true) =>
        new(await fixture.NewDatabaseAsync(), environment, seedDemoData);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        // UseSetting działa przed odczytem konfiguracji w Program (ConfigureAppConfiguration byłoby za późno).
        builder.UseSetting("ConnectionStrings:Hr", connectionString);
        builder.UseSetting("Hr:SeedDemoData", seedDemoData ? "true" : "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
            services.AddControllers().AddApplicationPart(typeof(TestController).Assembly);
        });
    }

    /// <summary>Klient z ciasteczkami i bez automatycznych przekierowań.</summary>
    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });
}
