using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Altinn.Authorization.ServiceDefaults;
using Altinn.Register.Core;
using Altinn.Register.TestUtils.Http;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit.Abstractions;

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
    /// Gets the test output helper.
    /// </summary>
    protected virtual ITestOutputHelper? TestOutputHelper
        => null;

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

        if (TestOutputHelper is { } output)
        {
            builder.Services.AddSingleton<ILoggerProvider>(s =>
            {
                var scopeProvider = s.GetService<IExternalScopeProvider>();
                var formatter = s.GetServices<ConsoleFormatter>().FirstOrDefault(f => f.Name == ConsoleFormatterNames.Simple);
                if (formatter is null)
                {
                    ThrowHelper.ThrowInvalidOperationException($"The '{ConsoleFormatterNames.Simple}' console formatter is not registered.");
                }

                return new XunitLoggerProvider(output, scopeProvider, formatter);
            });
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
        builder.Services.RemoveAll<TimeProvider>();
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

    private sealed class Disposable(Action dispose)
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

    private sealed class XunitLoggerProvider
        : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;
        private readonly IExternalScopeProvider? _scopeProvider;
        private readonly ConsoleFormatter _formatter;
        private readonly Lock _lock = new();

        public XunitLoggerProvider(ITestOutputHelper output, IExternalScopeProvider? scopeProvider, ConsoleFormatter formatter)
        {
            _output = output;
            _scopeProvider = scopeProvider;
            _formatter = formatter;
        }

        public ILogger CreateLogger(string categoryName)
            => new Logger(categoryName, this);

        public void Dispose()
        {
        }

        private sealed class Logger
            : ILogger
        {
            private readonly XunitLoggerProvider _provider;
            private readonly string _categoryName;

            public Logger(string categoryName, XunitLoggerProvider provider)
            {
                _provider = provider;
                _categoryName = categoryName;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => _provider._scopeProvider?.Push(state) ?? NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel)
                => logLevel != LogLevel.None;

            [ThreadStatic]
            private static StringWriter? _tStaticStringWriter;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                Guard.IsNotNull(formatter);

                _tStaticStringWriter ??= new StringWriter();
                LogEntry<TState> logEntry = new(logLevel, _categoryName, eventId, state, exception, formatter);
                _provider._formatter.Write(in logEntry, _provider._scopeProvider, _tStaticStringWriter);

                var sb = _tStaticStringWriter.GetStringBuilder();
                if (sb.Length == 0)
                {
                    return;
                }

                string logString = sb.ToString();
                sb.Clear();
                if (sb.Capacity > 1024)
                {
                    sb.Capacity = 1024;
                }

                lock (_provider._lock)
                {
                    _provider._output.WriteLine(logString);
                }
            }
        }

        private sealed class NullScope
            : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();

            private NullScope()
            {
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }
    }
}
