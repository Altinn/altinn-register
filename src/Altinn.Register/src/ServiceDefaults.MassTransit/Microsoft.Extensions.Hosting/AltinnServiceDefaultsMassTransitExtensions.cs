using System.Diagnostics.CodeAnalysis;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Authorization.ServiceDefaults.MassTransit.Commands;
using CommunityToolkit.Diagnostics;
using MassTransit;
using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Host builder extensions for MassTransit.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AltinnServiceDefaultsMassTransitExtensions
{
    private static string DefaultConfigSectionName(string connectionName)
        => $"Altinn:MassTransit:{connectionName}";

    /// <summary>
    /// Adds MassTransit to the host.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <param name="configureSettings">Optional settings configuration delegate.</param>
    /// <param name="configureMassTransit">Optional bus registration configurator delegate.</param>
    /// <param name="configureBus">Optional bus factory configurator delegate.</param>
    /// <returns>A <see cref="IMassTransitBuilder"/> for further configuration.</returns>
    public static IMassTransitBuilder AddAltinnMassTransit(
        this IHostApplicationBuilder builder,
        Action<MassTransitSettings>? configureSettings = null,
        Action<IBusRegistrationConfigurator>? configureMassTransit = null,
        Action<IBusFactoryConfigurator>? configureBus = null)
    {
        var serviceDescriptor = builder.GetAltinnServiceDescriptor();

        return AddAltinnMassTransit(builder, serviceDescriptor.Name, configureSettings, configureMassTransit, configureBus);
    }

    /// <summary>
    /// Adds MassTransit to the host.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <param name="busName">The name of the bus.</param>
    /// <param name="configureSettings">Optional settings configuration delegate.</param>
    /// <param name="configureMassTransit">Optional bus registration configurator delegate.</param>
    /// <param name="configureBus">Optional bus factory configurator delegate.</param>
    /// <returns>A <see cref="IMassTransitBuilder"/> for further configuration.</returns>
    public static IMassTransitBuilder AddAltinnMassTransit(
        this IHostApplicationBuilder builder,
        string busName,
        Action<MassTransitSettings>? configureSettings = null,
        Action<IBusRegistrationConfigurator>? configureMassTransit = null,
        Action<IBusFactoryConfigurator>? configureBus = null)
        => AddAltinnMassTransitCore(builder, DefaultConfigSectionName(busName), configureSettings, busName, configureMassTransit, configureBus);

    private static IMassTransitBuilder AddAltinnMassTransitCore(
        IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<MassTransitSettings>? configureSettings,
        string busName,
        Action<IBusRegistrationConfigurator>? configureMassTransit,
        Action<IBusFactoryConfigurator>? configureBus = null)
    {
        Guard.IsNotNull(builder);

        var configuration = builder.Configuration.GetSection(configurationSectionName);
        if (builder.Services.Contains(Marker.ServiceDescriptor))
        {
            // already registered
            return new MassTransitBuilder(builder.Services, configuration);
        }

        builder.Services.Add(Marker.ServiceDescriptor);

        MassTransitSettings settings = new();
        configuration.Bind(settings);
        configureSettings?.Invoke(settings);

        MassTransitTransportHelper helper = MassTransitTransportHelper.For(settings, busName);

        helper.ConfigureHost(builder);
        builder.Services.AddSingleton<IEndpointNameFormatter, AltinnEndpointNameFormatter>();
        builder.Services.AddSingleton<CommandQueueResolver>();
        builder.Services.AddSingleton<ICommandQueueRegistry>(s => s.GetRequiredService<CommandQueueResolver>());
        builder.Services.AddSingleton<ICommandQueueResolver>(s => s.GetRequiredService<CommandQueueResolver>());
        builder.Services.AddSingleton<CommandQueueRegistryEndpointConfigurationObserver>();
        builder.Services.AddScoped<ICommandSender, CommandSender>();
        builder.Services.AddMassTransit(configurator =>
        {
            configurator.AddConfigureEndpointsCallback((ctx, name, cfg) =>
            {
                // redeliver if all retries fail
                cfg.UseScheduledRedelivery(r => r.Intervals(
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(15)));

                // retry messages 3 times, in short order, before failing back to the broker
                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(10),
                    TimeSpan.FromMilliseconds(100)));
            });

            configureMassTransit?.Invoke(configurator);
            helper.ConfigureBus(configurator, (ctx, cfg) =>
            {
                var observer = ctx.GetRequiredService<CommandQueueRegistryEndpointConfigurationObserver>();
                cfg.ConnectEndpointConfigurationObserver(observer);

                configureBus?.Invoke(cfg);
                cfg.UseInMemoryOutbox(ctx, x => x.ConcurrentMessageDelivery = true);
            });

            configurator.AddInMemoryInboxOutbox();
        });

        if (!settings.DisableHealthChecks)
        {
            foreach (var reg in helper.HealthCheckRegistrations)
            {
                builder.TryAddHealthCheck(reg);
            }
        }

        if (!settings.DisableTracing)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(traceProviderBuilder =>
                {
                    traceProviderBuilder.AddSource(DiagnosticHeaders.DefaultListenerName);
                    helper.RegisterTracing(traceProviderBuilder);
                });
        }

        if (!settings.DisableMetrics)
        {
            builder.Services.AddOpenTelemetry()
                .WithMetrics(meterProviderBuilder =>
                {
                    meterProviderBuilder.AddMeter(InstrumentationOptions.MeterName);
                    helper.RegisterMetrics(meterProviderBuilder);
                });
        }

        return new MassTransitBuilder(builder.Services, configuration);
    }

    private sealed class Marker
    {
        public static readonly ServiceDescriptor ServiceDescriptor = ServiceDescriptor.Singleton<Marker, Marker>();
    }

    private class CommandQueueRegistryEndpointConfigurationObserver(ICommandQueueRegistry registry)
        : IEndpointConfigurationObserver
    {
        public void EndpointConfigured<T>(T configurator)
            where T : IReceiveEndpointConfigurator
        {
            configurator.ConnectConsumerConfigurationObserver(new CommandQueueRegistryConsumerConfigurationObserver(registry, configurator.InputAddress));
        }
    }

    private class CommandQueueRegistryConsumerConfigurationObserver(ICommandQueueRegistry registry, Uri queueUri)
        : IConsumerConfigurationObserver
    {
        public void ConsumerConfigured<TConsumer>(IConsumerConfigurator<TConsumer> configurator)
            where TConsumer : class
        {
        }

        public void ConsumerMessageConfigured<TConsumer, TMessage>(IConsumerMessageConfigurator<TConsumer, TMessage> configurator)
            where TConsumer : class
            where TMessage : class
        {
            if (TryGetCommandType(typeof(TMessage), out var commandType))
            {
                registry.RegisterConsumerCommandQueue(commandType, queueUri, typeof(TConsumer));
            }
        }

        private static bool TryGetCommandType(Type messageType, [NotNullWhen(true)] out Type? commandType)
        {
            if (typeof(Command).IsAssignableFrom(messageType))
            {
                commandType = messageType;
                return true;
            }

            if (messageType.IsConstructedGenericType && messageType.GetGenericTypeDefinition() == typeof(Batch<>))
            {
                return TryGetCommandType(messageType.GenericTypeArguments[0], out commandType);
            }

            commandType = null;
            return false;
        }
    }
}
