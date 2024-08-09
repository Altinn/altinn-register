using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// random port number - don't reuse if copy-pasting this code.
var postgresPort = builder.Configuration.GetValue("Postgres:Port", defaultValue: 28215);

var postgresServer = builder.AddPostgres("postgres", port: postgresPort)
    .WithDataVolume()
    .WithPgAdmin();

var registerDb = postgresServer.AddAltinnDatabase(
    "register-db",
    databaseName: "register");

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
