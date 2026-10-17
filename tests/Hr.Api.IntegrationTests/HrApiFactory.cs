using Hr.Agent;
using Hr.Core.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hr.Api.IntegrationTests;

/// <summary>
/// Aplikacja na świeżej bazie SQL Server (migracje i konta demo zakłada start aplikacji), zegar stały: 2026-10-16 12:00 UTC.
/// Domyślny profil danych demo to <c>Accounts</c> (sześć kont, bez danych), więc testy API działają na minimalnym zbiorze.
/// </summary>
public sealed class HrApiFactory(
    string connectionString, string environment = "Development", bool seedDemoData = true, bool scriptedModel = true, string demoProfile = "Accounts")
    : WebApplicationFactory<Program>
{
    public FakeClock Clock { get; } = new();

    /// <summary>Skryptowany model podstawiony pod prawdziwą pętlę narzędzi (<c>UseFunctionInvocation</c>), więc narzędzia wykonują się naprawdę.</summary>
    public ScriptedChatClient Model { get; } = new();

    public static async Task<HrApiFactory> CreateAsync(
        MsSqlFixture fixture, string environment = "Development", bool seedDemoData = true, bool scriptedModel = true, string demoProfile = "Accounts") =>
        new(await fixture.NewDatabaseAsync(), environment, seedDemoData, scriptedModel, demoProfile);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        // UseSetting działa przed odczytem konfiguracji w Program (ConfigureAppConfiguration byłoby za późno).
        builder.UseSetting("ConnectionStrings:Hr", connectionString);
        builder.UseSetting("Hr:SeedDemoData", seedDemoData ? "true" : "false");
        builder.UseSetting("Hr:DemoProfile", demoProfile);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
            if (scriptedModel)
            {
                services.AddKeyedSingleton<IChatClient>(AgentModel.ServiceKey, Model);
            }

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
