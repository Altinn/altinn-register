using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

builder.Services.TryAddLifecycleHook<DebugTrap>();

// TODO: Remove once https://github.com/dotnet/aspire/pull/4507 has been released (probably in Aspire 8.1)
var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var dbRoleAppPassword = builder.AddParameter("register-db-app-password", secret: true);
var dbRoleOwnerPassword = builder.AddParameter("register-db-owner-password", secret: true);
var dbRoleMigratorPassword = builder.AddParameter("register-db-migrator-password", secret: true);
var dbRoleSeederPassword = builder.AddParameter("register-db-seeder-password", secret: true);

// random port number - don't reuse if copy-pasting this code.
var postgresPort = builder.Configuration.GetValue("Postgres:Port", defaultValue: 28215);

var postgresServer = builder.AddPostgres("postgres", password: postgresPassword, port: postgresPort)
    .WithDataVolume()
    .WithPgAdmin();

var registerDb = postgresServer.AddAltinnDatabase(
    "register-db",
    databaseName: "register",
    password: dbRoleAppPassword,
    ownerPassword: dbRoleOwnerPassword,
    migratorPassword: dbRoleMigratorPassword,
    seederPassword: dbRoleSeederPassword);

var registerApi = builder.AddProject<Projects.Altinn_Register>("register")
    .WithReference(registerDb, "register")
    .WithEnvironment("Altinn__Npgsql__register__Enable", "true");

await builder.Build().RunAsync();

/// <summary>
/// Startup class.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed partial class Program
{
    private Program()
    {
    }
}
