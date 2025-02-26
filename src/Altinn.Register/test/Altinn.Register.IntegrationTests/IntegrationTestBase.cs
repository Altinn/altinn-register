using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.IntegrationTests.Fixtures;
using Altinn.Register.TestUtils;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.IntegrationTests;

public abstract class IntegrationTestBase
    : TestBase
{
    private TestWebApplication? _webApp;
    private AsyncServiceScope? _scope;
    private HttpClient? _client;

    protected IServiceProvider Services
        => _scope!.Value.ServiceProvider;

    protected HttpClient HttpClient
        => _client!;

    protected Uri BaseUrl
        => _client!.BaseAddress!;

    protected T GetRequiredService<T>()
        where T : notnull
        => Services.GetRequiredService<T>();

    protected async Task<T> Setup<T>(Func<IUnitOfWork, CancellationToken, Task<T>> setup)
    {
        var ct = TestContext.Current.CancellationToken;

        var uowManager = GetRequiredService<IUnitOfWorkManager>();
        await using var uow = await uowManager.CreateAsync(activityName: $"setup {TestContext.Current.Test!.TestDisplayName}", cancellationToken: ct);
        var result = await setup(uow, ct);
        await uow.CommitAsync(ct);

        return result;
    }

    protected Task Setup(Func<IUnitOfWork, CancellationToken, Task> setup)
        => Setup<object?>(async (uow, ct) =>
        {
            await setup(uow, ct);
            return null;
        });

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _webApp = await TestWebApplication.Create();
        _scope = _webApp.Services.CreateAsyncScope();
        _client = _webApp.CreateClient();
    }

    protected override async ValueTask DisposeAsync()
    {
        if (_client is { } client)
        {
            client.Dispose();
        }

        if (_scope is { } scope)
        {
            await scope.DisposeAsync();
        }

        if (_webApp is IAsyncDisposable webApp)
        {
            await webApp.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}
