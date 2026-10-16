using Hr.Agent;
using Hr.Core.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hr.Api.IntegrationTests;

/// <summary>Aplikacja na świeżej bazie SQL Server (migracje i konta demo zakłada start aplikacji), zegar stały: 2026-10-16 12:00 UTC.</summary>
public sealed class HrApiFactory(string connectionString, string environment = "Development", bool seedDemoData = true, bool scriptedModel = true)
    : WebApplicationFactory<Program>
{
    public FakeClock Clock { get; } = new();

    /// <summary>Skryptowany model podstawiony pod prawdziwą pętlę narzędzi (<c>UseFunctionInvocation</c>), więc narzędzia wykonują się naprawdę.</summary>
    public ScriptedChatClient Model { get; } = new();

    public static async Task<HrApiFactory> CreateAsync(MsSqlFixture fixture, string environment = "Development", bool seedDemoData = true, bool scriptedModel = true) =>
        new(await fixture.NewDatabaseAsync(), environment, seedDemoData, scriptedModel);

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
