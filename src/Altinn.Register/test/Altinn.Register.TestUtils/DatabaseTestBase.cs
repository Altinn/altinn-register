using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Altinn.Register.TestUtils;

/// <summary>
/// Base class for tests that needs a database.
/// </summary>
public abstract class DatabaseTestBase
    : HostTestBase
    , IClassFixture<PostgreSqlManager>
{
    private PostgreSqlDatabase? _db;

    /// <summary>
    /// Gets whether or not to seed database.
    /// </summary>
    protected virtual bool SeedData => true;

    /// <summary>
    /// Gets the database.
    /// </summary>
    protected PostgreSqlDatabase Database => _db!;

    /// <inheritdoc/>
    protected override async ValueTask ConfigureHost(IHostApplicationBuilder builder)
    {
        _db = await PostgreSqlManager.CreateDatabase();

        await base.ConfigureHost(builder);

        builder.Configuration.AddInMemoryCollection([
            new($"Altinn:Npgsql:register:ConnectionString", _db.ConnectionString),
            new($"Altinn:Npgsql:register:Migrate:ConnectionString", _db.MigratorConnectionString),
            new($"Altinn:Npgsql:register:Seed:ConnectionString", _db.SeederConnectionString),
            new($"Altinn:Npgsql:register:Seed:Enabled", SeedData ? "true" : "false"),
        ]);

        builder.AddAltinnServiceDefaults("register");
        builder.AddRegisterPersistence();
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_db is { } db)
        {
            await db.DisposeAsync();
        }
    }
}
