﻿using System.Text;
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
    private StringBuilder _harnessLogger = new();
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
        services.AddTelemetryListener(new StringWriter(_harnessLogger), includeDetails: true);
        AltinnServiceDefaultsMassTransitTestingExtensions.AddAltinnMassTransitTestHarness(
            services,
            output: new StringWriter(_harnessLogger),
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

        _harness.InactivityToken.Register(() => output.WriteLine("Test harness inactivity timeout reached."));
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsync()
    {
        if (_harness is { } harness)
        {
            _harness.ForceInactive();
            _harness.Cancel();

            var allExceptions = _harness.Consumed.SelectAsync(static m => m.Exception is not null).Select(static m => m.Exception)
                .Concat(_harness.Sent.SelectAsync(static m => m.Exception is not null).Select(static m => m.Exception))
                .Concat(_harness.Published.SelectAsync(static m => m.Exception is not null).Select(static m => m.Exception));

            var allFaults = _harness.Published.SelectAsync<Fault>().SelectMany(static f => f.Context.Message.Exceptions.ToAsyncEnumerable());

            await foreach (var consumeException in allExceptions)
            {
                output.WriteLine(consumeException.ToString());
            }

            var sb = new StringBuilder();
            await foreach (var fault in allFaults)
            {
                sb.Clear();
                WriteFaultString(fault, sb);
                output.WriteLine(sb.ToString());
            }
        }

        await base.DisposeAsync();

        output.WriteLine(_harnessLogger.ToString());
    }

    private static void WriteFaultString(ExceptionInfo exceptionInfo, StringBuilder sb)
    {
        var message = exceptionInfo.Message;

        if (string.IsNullOrEmpty(message))
        {
            sb.Append(exceptionInfo.ExceptionType);
        }
        else
        {
            sb.Append($"{exceptionInfo.ExceptionType}: {message}");
        }

        if (exceptionInfo.InnerException != null)
        {
            sb.Append(" ---> ");
            WriteFaultString(exceptionInfo.InnerException, sb);
            sb.AppendLine();
        }

        var stackTrace = exceptionInfo.StackTrace;
        if (stackTrace != null)
        {
            sb.AppendLine(stackTrace);
        }
    }
}
