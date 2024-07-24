using Altinn.Authorization.ServiceDefaults.Npgsql.TestSeed;
using Altinn.Authorization.ServiceDefaults.Npgsql.Yuniql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class RegisterPersistenceExtensions
{
    /// <summary>
    /// Adds persistence for the register application.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns><paramref name="builder"/>.</returns>
    public static IHostApplicationBuilder AddRegisterPersistence(this IHostApplicationBuilder builder)
    {
        builder.AddPartyPersistence();

        return builder;
    }

    /// <summary>
    /// Adds persistence for the party component.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns><paramref name="builder"/>.</returns>
    public static IHostApplicationBuilder AddPartyPersistence(this IHostApplicationBuilder builder)
    {
        AddDatabase(builder);

        ////builder.Services.AddTransient<IPartyPersistence, PostgreSqlPartyPersistence>();

        return builder;
    }

    private static void AddDatabase(IHostApplicationBuilder builder)
    {
        if (builder.Services.Any(s => s.ServiceType == typeof(Marker)))
        {
            // already added
            return;
        }

        builder.Services.AddSingleton<Marker>();

        var descriptor = builder.GetAltinnServiceDescriptor();
        var yuniqlSchema = builder.Configuration.GetValue<string>($"Altinn:Npgsql:{descriptor.Name}:Yuniql:MigrationsTable:Schema");
        var migrationsFs = new ManifestEmbeddedFileProvider(typeof(RegisterPersistenceExtensions).Assembly, "Migration");
        var seedDataFs = new ManifestEmbeddedFileProvider(typeof(RegisterPersistenceExtensions).Assembly, "TestData");
        builder.AddAltinnPostgresDataSource()
            .SeedFromFileProvider(seedDataFs)
            .AddYuniqlMigrations(y =>
            {
                y.WorkspaceFileProvider = migrationsFs;
                y.MigrationsTable.Schema = yuniqlSchema;
            });
    }

    private sealed record Marker;
}
