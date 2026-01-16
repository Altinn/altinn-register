using Altinn.Authorization.TestUtils.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Fakes;

internal sealed class FakeHttpClientFactory
    : IHttpClientFactory
    , IHttpMessageHandlerFactory
    , IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private FakeHttpClientFactory(ServiceProvider provider)
    {
        _provider = provider;
    }

    public FakeHttpClientFactory()
        : this(CreateProvider())
    {
    }

    public System.Net.Http.HttpClient CreateClient(string name)
        => GetRequiredService<IHttpClientFactory>().CreateClient(name);

    public HttpMessageHandler CreateHandler(string name)
        => GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(name);

    public FakeHttpMessageHandler For(string name)
        => GetRequiredService<FakeHttpHandlers>().For(name);

    public ValueTask DisposeAsync()
        => _provider.DisposeAsync();

    private T GetRequiredService<T>()
        where T : notnull
        => _provider.GetRequiredService<T>();

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddFakeHttpHandlers();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
