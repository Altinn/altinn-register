using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using Altinn.Authorization.ServiceDefaults.Npgsql.Yuniql;
using CommunityToolkit.Diagnostics;
using HealthChecks.RabbitMQ;
using MassTransit;
using MassTransit.RabbitMqTransport;
using MassTransit.RabbitMqTransport.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Quartz;
using Quartz.AspNetCore;

namespace Altinn.Authorization.ServiceDefaults.MassTransit;

/// <summary>
/// Configuration helper for MassTransit.
/// </summary>
[ExcludeFromCodeCoverage]
internal abstract class MassTransitTransportHelper(MassTransitSettings settings, string busName)
{
    /// <summary>
    /// Gets a <see cref="MassTransitTransportHelper"/> for the specified settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="busName">The name of the bus.</param>
    /// <returns>A <see cref="MassTransitTransportHelper"/>.</returns>
    public static MassTransitTransportHelper For(MassTransitSettings settings, string busName)
        => settings.Transport switch
        {
            MassTransitTransport.InMemory => new InMemoryTransportHelper(settings, busName),
            MassTransitTransport.RabbitMq => new RabbitMqTransportHelper(settings, busName),
            _ => ThrowHelper.ThrowArgumentException<MassTransitTransportHelper>(nameof(settings), "Invalid transport"),
        };

    /// <summary>
    /// Gets the settings for the bus.
    /// </summary>
    protected MassTransitSettings BusSettings => settings;

    /// <summary>
    /// Gets the name of the bus.
    /// </summary>
    protected string BusName => busName;

    /// <summary>
    /// Gets the health check registrations for the bus transport.
    /// </summary>
    public virtual IEnumerable<HealthCheckRegistration> HealthCheckRegistrations => [];

    /// <summary>
    /// Registers tracing for the bus transport.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/>.</param>
    public virtual void RegisterTracing(TracerProviderBuilder builder)
    {
    }

    /// <summary>
    /// Registers metrics for the bus transport.
    /// </summary>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/>.</param>
    public virtual void RegisterMetrics(MeterProviderBuilder builder)
    {
    }

    /// <summary>
    /// Configures the host.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    public virtual void ConfigureHost(IHostApplicationBuilder builder)
    {
    }

    /// <summary>
    /// Configures the bus.
    /// </summary>
    /// <param name="configurator">The bus configurator.</param>
    /// <param name="configureBus">The bus factory configurator.</param>
    public abstract void ConfigureBus(IBusRegistrationConfigurator configurator, Action<IBusRegistrationContext, IBusFactoryConfigurator> configureBus);

    private sealed class InMemoryTransportHelper(MassTransitSettings settings, string busName)
        : MassTransitTransportHelper(settings, busName)
    {
        public override void ConfigureBus(IBusRegistrationConfigurator configurator, Action<IBusRegistrationContext, IBusFactoryConfigurator> configureBus)
        {
            configurator.UsingInMemory((ctx, cfg) =>
            {
                configureBus(ctx, cfg);
                cfg.ConfigureEndpoints(ctx);
            });
        }
    }

    private sealed class RabbitMqTransportHelper(MassTransitSettings settings, string busName)
        : MassTransitTransportHelper(settings, busName)
    {
        private RabbitMQHealthCheck? _healthCheck;

        private MassTransitRabbitMqSettings Settings => BusSettings.RabbitMq;

        public override void ConfigureHost(IHostApplicationBuilder builder)
        {
            //var quartzSchema = "quartz";

            // TODO: make better
            var descriptor = builder.GetAltinnServiceDescriptor();
            var quartzSchema = builder.Configuration.GetValue($"Altinn:MassTransit:{descriptor.Name}:Quartz:Schema", defaultValue: $"{descriptor.Name}_quartz")!;
            var connectionString = builder.Configuration.GetValue<string>($"Altinn:Npgsql:{descriptor.Name}:ConnectionString");
            var yuniqlSchema = builder.Configuration.GetValue($"Altinn:Npgsql:{descriptor.Name}:Yuniql:MigrationsTable:Schema", defaultValue: "yuniql");
            var yuniqlTable = builder.Configuration.GetValue($"Altinn:Npgsql:{descriptor.Name}:Yuniql:MigrationsTable:QuartzTable", defaultValue: $"{descriptor.Name}_quartz_migrations");
            var migrationsFs = new ManifestEmbeddedFileProvider(typeof(MassTransitTransportHelper).Assembly, "Migration");

            builder.AddAltinnPostgresDataSource()
                .AddYuniqlMigrations(typeof(MassTransitTransportHelper), y =>
                {
                    y.WorkspaceFileProvider = migrationsFs;
                    y.MigrationsTable.Schema = yuniqlSchema;
                    y.MigrationsTable.Name = yuniqlTable;
                    y.Tokens.Add("SCHEMA", quartzSchema);
                });

            builder.Services.AddOptions<RabbitMqTransportOptions>()
                .Configure(options =>
                {
                    options.Host = Settings.Host;
                    options.Port = Settings.Port;
                    options.ManagementPort = Settings.ManagementPort;
                    options.VHost = Settings.VHost;
                    options.User = Settings.UserName;
                    options.Pass = Settings.Password;
                    options.UseSsl = Settings.UseSsl;
                    options.ConnectionName = BusName;
                });

            builder.Services.AddQuartz(q => 
            {
                q.SchedulerId = "AUTO"; // TODO: use instance ID from pod-name or similar
                q.UsePersistentStore(s =>
                {
                    s.UseSystemTextJsonSerializer();
                    s.UseClustering();
                    s.UsePostgres(p =>
                    {
                        p.ConnectionString = connectionString!;
                        p.TablePrefix = $"{quartzSchema}.";
                    });
                });
            });

            builder.Services.AddQuartzServer(opt =>
            {
                opt.WaitForJobsToComplete = true;
            });
        }

        public override void ConfigureBus(IBusRegistrationConfigurator configurator, Action<IBusRegistrationContext, IBusFactoryConfigurator> configureBus)
        {
            var schedulerQueueName = $"{BusName}-scheduler";
            var schedulerEndpoint = new Uri($"queue:{schedulerQueueName}");

            configurator.AddMessageScheduler(schedulerEndpoint);
            configurator.AddQuartzConsumers(
                q => q.QueueName = schedulerQueueName);

            configurator.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.UseMessageScheduler(schedulerEndpoint);
                cfg.ReceiveEndpoint(schedulerQueueName, sch =>
                {
                    sch.ConfigureQuartzConsumers(ctx);
                });

                configureBus(ctx, cfg);
                cfg.ConfigureEndpoints(ctx);
            });
        }

        public override void RegisterTracing(TracerProviderBuilder builder)
        {
            builder.AddQuartzInstrumentation();
            builder.AddProcessor<FilterRootQuartzQueriesProcessor>();
        }

        public override IEnumerable<HealthCheckRegistration> HealthCheckRegistrations
            => [
                new HealthCheckRegistration(
                    "RabbitMQ",
                    sp => 
                    {
                        var healthCheck = Volatile.Read(ref _healthCheck);
                        if (healthCheck is null) 
                        {
                            var newCheck = CreateRabbitMqHealthCheck(sp);

                            healthCheck = Interlocked.CompareExchange(ref _healthCheck, newCheck, null) ?? newCheck;
                        }

                        return healthCheck;
                    },
                    failureStatus: default,
                    tags: default,
                    timeout: default)
            ];

        private static RabbitMQHealthCheck CreateRabbitMqHealthCheck(IServiceProvider services)
        {
            var busName = string.Empty;
            var options = services.GetRequiredService<IOptionsMonitor<RabbitMqTransportOptions>>().Get(busName);
            var configurator = new RabbitMqHostConfigurator(options.Host, options.VHost, options.Port, options.ConnectionName);
            configurator.Username(options.User);
            configurator.Password(options.Pass);
            if (options.UseSsl)
            {
                configurator.UseSsl(s =>
                {
                    var sslOptions = services.GetRequiredService<IOptionsMonitor<RabbitMqSslOptions>>().Get(busName);

                    if (!string.IsNullOrWhiteSpace(sslOptions.ServerName))
                    {
                        s.ServerName = sslOptions.ServerName;
                    }

                    if (!string.IsNullOrWhiteSpace(sslOptions.CertPath))
                    {
                        s.CertificatePath = sslOptions.CertPath;
                    }

                    if (!string.IsNullOrWhiteSpace(sslOptions.CertPassphrase))
                    {
                        s.CertificatePassphrase = sslOptions.CertPassphrase;
                    }

                    s.UseCertificateAsAuthenticationIdentity = sslOptions.CertIdentity;

                    s.Protocol = sslOptions.Protocol;

                    if (sslOptions.Trust)
                    {
                        s.AllowPolicyErrors(SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNotAvailable);
                    }
                });
            }

            var factory = configurator.Settings.GetConnectionFactory();

            return new RabbitMQHealthCheck(new RabbitMQHealthCheckOptions
            {
                ConnectionFactory = factory,
            });
        }

        private sealed class FilterRootQuartzQueriesProcessor
            : BaseProcessor<Activity>
        {
            public override void OnStart(Activity data)
            {
                if (data.Source.Name == "Npgsql" && data.Parent is null)
                {
                    data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    data.IsAllDataRequested = false;
                }
            }
        }
    }
}
