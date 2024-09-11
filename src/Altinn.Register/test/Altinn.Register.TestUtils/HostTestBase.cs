using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Altinn.Register.TestUtils;

/// <summary>
/// Base class for tests that needs a generic host.
/// </summary>
public abstract class HostTestBase
    : ServicesTestBase
{
    private IHost? _host;
    private FakeTimeProvider? _timeProvider;

    /// <summary>
    /// Gets a time provider.
    /// </summary>
    protected FakeTimeProvider TimeProvider 
        => _timeProvider!;

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

        await Configure(configuration);

        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = "test",
            EnvironmentName = "Development",
            Configuration = configuration,
        });

        await ConfigureHost(builder);

        return builder.Build();
    }

    /// <inheritdoc/>
    protected override sealed async ValueTask<IServiceProvider> InitializeServiceProvider()
    {
        var host = await InitializeHost();
        
        _host = host;

        await _host.StartAsync();

        _timeProvider = host.Services.GetRequiredService<FakeTimeProvider>();

        return _host.Services;
    }

    /// <summary>
    /// Configures the host.
    /// </summary>
    /// <param name="builder">Host builder.</param>
    protected virtual async ValueTask ConfigureHost(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(new FakeTimeProvider());
        builder.Services.TryAddSingleton<TimeProvider>(s => s.GetRequiredService<FakeTimeProvider>());
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
}
