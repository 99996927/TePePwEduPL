using Hr.TestSupport;

namespace Hr.Core.IntegrationTests;

[CollectionDefinition("mssql")]
public sealed class MsSqlCollection : ICollectionFixture<MsSqlFixture>
{
}
