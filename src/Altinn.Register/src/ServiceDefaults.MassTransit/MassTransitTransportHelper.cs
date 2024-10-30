using CommunityToolkit.Diagnostics;
using HealthChecks.RabbitMQ;
using MassTransit;
using MassTransit.RabbitMqTransport;
using MassTransit.RabbitMqTransport.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using System.Net.Security;

namespace Altinn.Authorization.ServiceDefaults.MassTransit;

/// <summary>
/// Configuration helper for MassTransit.
/// </summary>
internal abstract class MassTransitTransportHelper(MassTransitSettings settings, string busName)
{
    /// <summary>
    /// Gets a <see cref="MassTransitTransportHelper"/> for the specified settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
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
    public abstract void ConfigureBus(IBusRegistrationConfigurator configurator);

    private sealed class InMemoryTransportHelper(MassTransitSettings settings, string busName)
        : MassTransitTransportHelper(settings, busName)
    {
        public override void ConfigureBus(IBusRegistrationConfigurator configurator)
        {
            configurator.UsingInMemory();
        }
    }

    private sealed class RabbitMqTransportHelper(MassTransitSettings settings, string busName)
        : MassTransitTransportHelper(settings, busName)
    {
        private RabbitMQHealthCheck? _healthCheck;

        private MassTransitRabbitMqSettings Settings => BusSettings.RabbitMq;

        public override void ConfigureHost(IHostApplicationBuilder builder)
        {
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
        }

        public override void ConfigureBus(IBusRegistrationConfigurator configurator)
        {
            configurator.UsingRabbitMq((ctx, cfg) =>
            {
            });
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
                            var newCheck = CreateHealthCheck(sp);

                            healthCheck = Interlocked.CompareExchange(ref _healthCheck, newCheck, null) ?? newCheck;
                        }

                        return healthCheck;
                    },
                    failureStatus: default,
                    tags: default,
                    timeout: default)
            ];

        private static RabbitMQHealthCheck CreateHealthCheck(IServiceProvider services)
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
    }
}
