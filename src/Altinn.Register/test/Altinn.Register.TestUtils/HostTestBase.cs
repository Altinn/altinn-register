using System.Diagnostics;
using Altinn.Authorization.ServiceDefaults;
using Altinn.Register.Core;
using Altinn.Register.TestUtils.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Altinn.Register.TestUtils;

/// <summary>
/// Base class for tests that needs a generic host.
/// </summary>
public abstract class HostTestBase
    : ServicesTestBase
{
    private readonly FakeHttpHandlers _httpHandlers = new();
    private readonly FakeTimeProvider _timeProvider = new();

    private IHost? _host;

    /// <summary>
    /// Gets a time provider.
    /// </summary>
    protected FakeTimeProvider TimeProvider 
        => _timeProvider;

    /// <summary>
    /// Gets the <see cref="FakeHttpMessageHandler"/> used by the host.
    /// </summary>
    protected FakeHttpHandlers HttpHandlers
        => _httpHandlers;

    /// <summary>
    /// Gets a value indicating whether to disable logging.
    /// </summary>
    protected virtual bool DisableLogging
        => !Debugger.IsAttached;

    /// <summary>
    /// Initialize the host.
    /// </summary>
    /// <returns>The host.</returns>
    protected virtual async ValueTask<IHost> InitializeHost()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection([
            new("Logging:LogLevel:Default", "Warning"),
        ]);

        if (DisableLogging)
        {
            configuration.AddInMemoryCollection([
                new(AltinnPreStartLogger.DisableConfigKey, "true"),
            ]);
        }

        await Configure(configuration);

        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = "test",
            EnvironmentName = "Development",
            Configuration = configuration,
        });

        if (DisableLogging)
        {
            builder.Logging.ClearProviders();
        }

        await ConfigureHost(builder);

        return builder.Build();
    }

    /// <inheritdoc/>
    protected override sealed async ValueTask<IServiceProvider> InitializeServiceProvider()
    {
        var host = await InitializeHost();
        
        _host = host;

        await _host.StartAsync();

        return _host.Services;
    }

    /// <summary>
    /// Configures the host.
    /// </summary>
    /// <param name="builder">Host builder.</param>
    protected virtual async ValueTask ConfigureHost(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(_timeProvider);
        builder.Services.TryAddSingleton<TimeProvider>(s => s.GetRequiredService<FakeTimeProvider>());
        builder.Services.AddFakeHttpHandlers(_httpHandlers);
        builder.Services.TryAddSingleton<RegisterTelemetry>();

        await ConfigureServices(builder.Services);
    }

    /// <summary>
    /// Configures the host configuration.
    /// </summary>
    /// <param name="configuration">The configuration manager.</param>
    protected virtual ValueTask Configure(IConfigurationManager configuration)
        => ValueTask.CompletedTask;

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsync()
    {
        if (_host is { } host)
        {
            await host.StopAsync();
            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
                
            host.Dispose();
        }

        await base.DisposeAsync();
    }

    /// <summary>
    /// Starts updating the <see cref="TimeProvider"/> every <paramref name="interval"/> by <paramref name="stepSize"/>
    /// in a best-effort manner.
    /// </summary>
    /// <param name="interval">How often to update the internal clock.</param>
    /// <param name="stepSize">By how much to update the clock.</param>
    /// <returns>A <see cref="IDisposable"/> that stops the updating.</returns>
    protected IDisposable UpdateTimerRealtime(
        TimeSpan interval,
        TimeSpan stepSize)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(
            GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
        var ct = cts.Token;
        var provider = TimeProvider;

        var systemTime = System.TimeProvider.System;
        var prev = systemTime.GetTimestamp();
        var timer = systemTime.CreateTimer(
            _ =>
            {
                var now = systemTime.GetTimestamp();
                var elapsed = systemTime.GetElapsedTime(prev, now);
                prev = now;

                while (!ct.IsCancellationRequested && elapsed >= interval)
                {
                    TimeProvider.Advance(stepSize);
                    elapsed -= interval;
                }
            },
            null,
            TimeSpan.Zero,
            interval);

        ct.Register(() => timer.Dispose());

        return new Disposable(() =>
        {
            cts.Cancel();
            cts.Dispose();
        });
    }

    private class Disposable(Action dispose)
        : IDisposable
    {
        private int _disposed = 0;

        public void Dispose() 
        { 
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                dispose();
                dispose = null!;
            }
        }
    }
}
