using Hr.TestSupport;
using Microsoft.Data.SqlClient;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class MsSqlFixtureTests(MsSqlFixture fixture)
{
    [Fact]
    public async Task Fresh_database_accepts_queries()
    {
        var first = await fixture.NewDatabaseAsync();
        var second = await fixture.NewDatabaseAsync();

        Assert.Equal(1, await Scalar<int>(first, "SELECT 1"));
        Assert.NotEqual(await Scalar<string>(first, "SELECT DB_NAME()"), await Scalar<string>(second, "SELECT DB_NAME()"));
    }

    private static async Task<T> Scalar<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
