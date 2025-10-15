using Altinn.Authorization.ServiceDefaults.MassTransit.Commands;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Altinn.Authorization.ServiceDefaults.MassTransit;

/// <summary>
/// Configuration helper for MassTransit.
/// </summary>
internal abstract partial class MassTransitTransportHelper
{
    private sealed class InMemoryTransportHelper(MassTransitSettings settings, string busName, AltinnServiceDescriptor serviceDescriptor)
        : MassTransitTransportHelper(settings, busName, serviceDescriptor)
    {
        public override void ConfigureBus(IBusRegistrationConfigurator configurator, Action<IBusRegistrationContext, IBusFactoryConfigurator> configureBus)
        {
            configurator.UsingInMemory((ctx, cfg) =>
            {
                cfg.UseInMemoryScheduler();

                configureBus(ctx, cfg);
                cfg.ConfigureEndpoints(ctx);
            });
        }

        public override void ConfigureHost(IHostApplicationBuilder builder)
        {
            builder.Services.TryAddSingleton<ICommandQueueStatsProvider, NullStatsProvider>();
        }
    }
}
