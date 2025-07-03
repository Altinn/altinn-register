namespace Altinn.Register.TestUtils.Database;

public sealed class PostgresDatabase
    : IAsyncDisposable
{
    public static async Task<PostgresDatabase> Create()
    {
        var testContext = TestContext.Current;
        var serverFixture = await testContext.GetFixture<PostgresServerFixture>();
        if (serverFixture is null)
        {
            throw new InvalidOperationException("PostgresServerFixture not found in test context.");
        }

        return await serverFixture.CreateDatabase(testContext.CancellationToken);
    }

    private readonly PostgresServerFixture.DatabaseHandle _handle;

    internal PostgresDatabase(PostgresServerFixture.DatabaseHandle handle)
    {
        _handle = handle;
    }

    public string ConnectionString(PostgresUserType type)
        => _handle.ConnectionString(type);

    public string OwnerConnectionString
        => ConnectionString(PostgresUserType.Owner);

    public string MigratorConnectionString
        => ConnectionString(PostgresUserType.Migrator);

    public string SeederConnectionString
        => ConnectionString(PostgresUserType.Seeder);

    public string AppConnectionString
        => ConnectionString(PostgresUserType.App);

    ValueTask IAsyncDisposable.DisposeAsync()
        => _handle.DisposeAsync();
}
