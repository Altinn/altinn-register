#nullable enable

using Altinn.Authorization.ServiceDefaults;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Altinn.Register.Tests.TestingControllers.Utils;

public class WebApplicationFixture
    : IAsyncLifetime
{
    private readonly WebApplicationFactory _factory = new();

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    public WebApplicationFactory<Program> CreateServer(
        Action<IConfigurationBuilder>? configureConfiguration = null,
        Action<IServiceCollection>? configureServices = null)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            if (configureConfiguration is not null)
            {
                var settings = new ConfigurationBuilder();
                configureConfiguration(settings);
                builder.UseConfiguration(settings.Build());
            }

            if (configureServices is not null)
            {
                builder.ConfigureTestServices(configureServices);
            }
        });
    }

    private class WebApplicationFactory 
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var settings = new ConfigurationBuilder()
                .AddInMemoryCollection([
                    new(AltinnPreStartLogger.DisableConfigKey, "true"),
                    new("Altinn:IsTest", "true"),
                ])
                .Build();
            
            builder.UseConfiguration(settings);
            builder.ConfigureLogging(builder => builder.ClearProviders());

            base.ConfigureWebHost(builder);
        }
    }
}
