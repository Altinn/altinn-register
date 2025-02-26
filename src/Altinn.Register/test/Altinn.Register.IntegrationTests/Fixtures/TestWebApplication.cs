using System.Diagnostics;
using Altinn.Register.IntegrationTests.TestServices;
using Altinn.Register.IntegrationTests.Tracing;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.IntegrationTests.Fixtures;

public sealed class TestWebApplication
    : IAsyncDisposable
{
    public static async Task<TestWebApplication> Create()
    {
        var fixture = await TestContext.Current.GetRequiredFixture<WebApplicationFixture>();
        
        return await fixture.CreateServer();
    }

    private readonly WebApplicationFactory<Program> _factory;
    private readonly PostgresDatabase _db;

    public TestWebApplication(
        WebApplicationFactory<Program> factory,
        PostgresDatabase db)
    {
        _factory = factory;
        _db = db;
    }

    public IServiceProvider Services
        => _factory.Services;

    public HttpClient CreateClient()
    {
        var jwtService = Services.GetRequiredService<TestJwtService>();

        return _factory.CreateDefaultClient(
            new Uri("http://register.test/"),
            [
                new TracingHandler(),
                new DefaultTestAuthorizationHandler(jwtService),
                new RedirectHandler(),
                new CookieContainerHandler(),
            ]);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await _factory.DisposeAsync();
        await ((IAsyncDisposable)_db).DisposeAsync();
    }

    private class TracingHandler
        : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var activityVerb = request.Method.ToString().ToLowerInvariant();
            var relPath = request.RequestUri?.AbsolutePath;

            using var activity = IntegrationTestsActivities.Source.StartActivity(ActivityKind.Client, name: $"{activityVerb} {relPath}");
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private class DefaultTestAuthorizationHandler(TestJwtService jwt)
        : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is null)
            {
                var token = jwt.GenerateToken();
                request.Headers.Authorization = new("Bearer", token);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
