using Altinn.Register.TestUtils.Database;
using Altinn.Register.TestUtils.TestData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Altinn.Register.TestUtils;

/// <summary>
/// Base class for tests that need a PostgreSQL database.
/// </summary>
public abstract class DatabaseTestBase
    : HostTestBase
{
    private PostgresDatabase? _db;

    /// <summary>
    /// Gets whether database seed data should be enabled.
    /// </summary>
    protected virtual bool SeedData => true;

    /// <summary>
    /// Gets the per-test database.
    /// </summary>
    protected PostgresDatabase Database => _db!;

    /// <summary>
    /// Gets the register test data generator.
    /// </summary>
    protected RegisterTestDataGenerator TestDataGenerator => GetRequiredService<RegisterTestDataGenerator>();

    /// <inheritdoc/>
    protected override async ValueTask ConfigureHost(IHostApplicationBuilder builder)
    {
        _db = await PostgresDatabase.Create();

        await base.ConfigureHost(builder);

        builder.Configuration.AddInMemoryCollection([
            new("Altinn:Npgsql:register:ConnectionString", _db.ConnectionString),
            new("Altinn:Npgsql:register:Migrate:ConnectionString", _db.MigratorConnectionString),
            new("Altinn:Npgsql:register:Seed:ConnectionString", _db.SeederConnectionString),
            new("Altinn:Npgsql:register:Seed:Enabled", SeedData ? "true" : "false"),
        ]);

        builder.AddRegisterPersistence();
        builder.Services.AddSingleton<RegisterTestDataGenerator>();
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (_db is { } db)
        {
            await ((IAsyncDisposable)db).DisposeAsync();
        }
    }
}
