using System.Diagnostics.CodeAnalysis;
using Altinn.Register.AppHost;
using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// random port number - don't reuse if copy-pasting this code.
var postgresPort = builder.Configuration.GetValue("Postgres:Port", defaultValue: 28215);
var rabbitMqPort = builder.Configuration.GetValue("RabbitMq:Port", defaultValue: 28216);
var rabbitMqMgmtPort = builder.Configuration.GetValue("RabbitMq:ManagementPort", defaultValue: 28217);

// databases
var postgresServer = builder.AddPostgres("postgres", port: postgresPort)
    .WithDataVolume()
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPublicEndpoints();

var registerDb = postgresServer.AddAltinnDatabase(
    "register-db",
    databaseName: "register");

// rabbitmq
var rabbitMqUsername = builder.CreateResourceBuilder(new ParameterResource("rabbitmq-username", _ => "rabbit-admin"));
var rabbitMq = builder.AddRabbitMQ("rabbitmq", userName: rabbitMqUsername, port: rabbitMqPort)
    .WithDataVolume()
    .WithManagementPlugin(port: rabbitMqMgmtPort)
    .WithContainerFiles("/etc/rabbitmq", [new ContainerFile { Name = "enabled_plugins", Contents = "[rabbitmq_management,rabbitmq_prometheus,rabbitmq_shovel_management,rabbitmq_shovel].\n" }])
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPublicEndpoints();

var registerApi = builder.AddProject<Projects.Altinn_Register>("register")
    .WithReference(registerDb, "register")
    .WaitFor(rabbitMq)
    .WithEnvironment(ctx =>
    {
        var env = ctx.EnvironmentVariables;
        var prefix = "Altinn__MassTransit__register__";
        var rabbitEndpoint = rabbitMq.GetEndpoint("tcp");
        var rabbitMgmtEndpoint = rabbitMq.GetEndpoint("management");

        env[$"{prefix}Enable"] = "true";
        env[$"{prefix}Transport"] = "rabbitmq";

        env[$"{prefix}RabbitMq__Host"] = rabbitEndpoint.Property(EndpointProperty.Host);
        env[$"{prefix}RabbitMq__Port"] = rabbitEndpoint.Property(EndpointProperty.Port);
        env[$"{prefix}RabbitMq__ManagementPort"] = rabbitMgmtEndpoint.Property(EndpointProperty.Port);
        env[$"{prefix}RabbitMq__Username"] = ReferenceExpression.Create($"{rabbitMq.Resource.UserNameParameter!}");
        env[$"{prefix}RabbitMq__Password"] = ReferenceExpression.Create($"{rabbitMq.Resource.PasswordParameter}");
        env[$"{prefix}RabbitMq__VirtualHost"] = "/";
    })
    .WithEnvironment("Altinn__Npgsql__register__Enable", "true")
    .WithEnvironment("Altinn__register__PartyImport__A2__Enable", "false")
    .WithEnvironment("Altinn__register__PartyImport__A2__PartyUserId__Enable", "false")
    .WithHttpHealthCheck("/health");

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
