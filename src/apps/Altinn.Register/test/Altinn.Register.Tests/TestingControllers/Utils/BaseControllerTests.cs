#nullable enable

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Tests.TestingControllers.Utils;

public abstract class BaseControllerTests
    : IClassFixture<WebApplicationFixture>
    , IAsyncLifetime
{
    private readonly WebApplicationFixture _webApplicationFixture;

    private WebApplicationFactory<Program>? _webApp;
    private IServiceProvider? _services;
    private AsyncServiceScope _scope;

    protected BaseControllerTests(WebApplicationFixture webApplicationFixture)
    {
        _webApplicationFixture = webApplicationFixture;
    }

    protected virtual HttpClient CreateClient()
        => _webApp!.CreateClient();

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    protected virtual void ConfigureTestConfiguration(IConfigurationBuilder builder)
    {
    }

    protected virtual ValueTask Initialize(IServiceProvider services)
    {
        return ValueTask.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        if (_scope is { } scope) 
        { 
            await scope.DisposeAsync();
        }

        if (_services is IAsyncDisposable iad) 
        { 
            await iad.DisposeAsync();
        }
        else if (_services is IDisposable id) 
        { 
            id.Dispose(); 
        }

        if (_webApp is { } webApp) 
        { 
            await webApp.DisposeAsync();
        }
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        _webApp = _webApplicationFixture.CreateServer(
            configureConfiguration: config =>
            {
                ConfigureTestConfiguration(config);
            },
            configureServices: services =>
            {
                ConfigureTestServices(services);
            });

        _services = _webApp.Services;
        _scope = _services.CreateAsyncScope();

        await Initialize(_services);
    }

    protected virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
