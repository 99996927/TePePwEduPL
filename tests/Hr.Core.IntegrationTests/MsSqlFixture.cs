using Hr.Core.Abstractions;
using Hr.Core.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Hr.Core.IntegrationTests;

/// <summary>Jeden kontener SQL Server na kolekcję testów, osobna pusta baza na każde wywołanie.</summary>
public sealed class MsSqlFixture : IAsyncLifetime
{
    // Docker Engine 24.0.9 na hoście mówi API 1.43; Testcontainers 4.x bez tej wersji nie łączy się z silnikiem.
    static MsSqlFixture()
    {
        if (Environment.GetEnvironmentVariable("DOCKER_API_VERSION") is null)
        {
            Environment.SetEnvironmentVariable("DOCKER_API_VERSION", "1.43");
        }
    }

    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<string> NewDatabaseAsync()
    {
        string name = $"test_{Guid.NewGuid():N}";
        await using (var connection = new SqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand($"CREATE DATABASE [{name}]", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = name }.ConnectionString;
    }

    /// <summary>Nowa baza z zastosowanymi migracjami; zwraca connection string.</summary>
    public async Task<string> NewMigratedDatabaseAsync()
    {
        var connectionString = await NewDatabaseAsync();
        await using var db = await OpenContextAsync(connectionString);
        await db.Database.MigrateAsync();
        return connectionString;
    }

    /// <summary>Otwiera kontekst na istniejącej bazie, bez migrowania.</summary>
    public Task<HrDbContext> OpenContextAsync(string connectionString, ICurrentUser? actor = null)
    {
        var options = new DbContextOptionsBuilder<HrDbContext>().UseSqlServer(connectionString).Options;
        return Task.FromResult(new HrDbContext(options));
    }

    /// <summary>Nowa, zmigrowana baza i kontekst do niej.</summary>
    public async Task<HrDbContext> NewContextAsync(ICurrentUser? actor = null) =>
        await OpenContextAsync(await NewMigratedDatabaseAsync(), actor);
}

[CollectionDefinition("mssql")]
public sealed class MsSqlCollection : ICollectionFixture<MsSqlFixture>
{
}
