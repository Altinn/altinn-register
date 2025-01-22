using System.Diagnostics.CodeAnalysis;
using System.Text;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Authorization.ServiceDefaults.MassTransit.Commands;
using Altinn.Authorization.ServiceDefaults.MassTransit.Testing;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.PartyImport.A2;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Altinn.Register.TestUtils;

/// <summary>
/// Base class for tests that needs a bus.
/// </summary>
public abstract class BusTestBase(ITestOutputHelper output)
    : DatabaseTestBase
{
    private ITestHarness? _harness;
    private IBusControl? _bus;
    private ICommandSender? _commandSender;
    private ICommandQueueResolver? _commandQueueResolver;

    /// <summary>
    /// Gets the mass transit test harness.
    /// </summary>
    protected ITestHarness Harness => _harness!;

    /// <summary>
    /// Gets the bus.
    /// </summary>
    protected IBusControl Bus => _bus!;

    /// <summary>
    /// Gets the command sender.
    /// </summary>
    protected ICommandSender CommandSender => _commandSender!;

    /// <summary>
    /// Gets the command queue resolver.
    /// </summary>
    protected ICommandQueueResolver CommandQueueResolver => _commandQueueResolver!;

    /// <inheritdoc/>
    protected override ValueTask ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient<IA2PartyImportService, A2PartyImportService>();
        AltinnServiceDefaultsMassTransitTestingExtensions.AddAltinnMassTransitTestHarness(
            services,
            configureMassTransit: (cfg) =>
            {
                cfg.AddConsumers(typeof(RegisterHost).Assembly);
            });

        return base.ConfigureServices(services);
    }

    /// <inheritdoc/>
    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _harness = Services.GetTestHarness();
        _bus = Services.GetRequiredService<IBusControl>();
        _commandSender = Services.GetRequiredService<ICommandSender>();
        _commandQueueResolver = Services.GetRequiredService<ICommandQueueResolver>();

        _harness.TestInactivityTimeout = TimeSpan.FromMinutes(2);
        _harness.TestTimeout = TimeSpan.FromMinutes(5);
        await _harness.RestartHostedServices();
        _harness.InactivityToken.Register(() => output.WriteLine("Test harness inactivity timeout reached."));
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsync()
    {
        if (_harness is { } harness)
        {
            _harness.ForceInactive();
            _harness.Cancel();
            var builder = new StringBuilder();
            await _harness.OutputTimeline(new StringWriter(builder));
            output.WriteLine(builder.ToString());

            await foreach (var consumeException in _harness.Consumed.SelectAsync(static m => m.Exception is not null).Select(static m => m.Exception))
            {
                output.WriteLine(consumeException.ToString());
            }
        }

        await base.DisposeAsync();
    }
}
