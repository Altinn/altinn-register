using MassTransit;

namespace Altinn.Authorization.ServiceDefaults.MassTransit;

/// <summary>
/// Configuration helper for MassTransit.
/// </summary>
internal abstract partial class MassTransitTransportHelper
{
    private sealed class AzureServiceBusTransportHelper(MassTransitSettings settings, string busName)
        : MassTransitTransportHelper(settings, busName)
    {
        private MassTransitAzureServiceBusSettings Settings => BusSettings.AzureServiceBus;

        public override void ConfigureBus(IBusRegistrationConfigurator configurator, Action<IBusRegistrationContext, IBusFactoryConfigurator> configureBus)
        {
            configurator.AddServiceBusMessageScheduler();

            configurator.UsingAzureServiceBus((ctx, cfg) =>
            {
                cfg.Host(Settings.ConnectionString);
                cfg.UseServiceBusMessageScheduler();

                configureBus(ctx, cfg);
                cfg.ConfigureEndpoints(ctx);
            });
        }
    }
}
