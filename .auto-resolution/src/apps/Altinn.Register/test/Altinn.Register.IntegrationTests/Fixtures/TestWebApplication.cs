using Altinn.Register.Core.Utils;
using Altinn.Register.IntegrationTests.TestServices;
using Altinn.Register.IntegrationTests.Tracing;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.Database;
using Altinn.Register.TestUtils.MassTransit;
using MassTransit;
using MassTransit.Testing;
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
        if (TestContext.Current.TestOutputHelper is { } output)
        {
            var busHarness = Services.GetRequiredService<ITestHarness>();
            var faults = await busHarness.Consumed.SelectExisting(static m => m.Exception is not null).ToListAsync(CancellationToken.None);

            foreach (var fault in faults)
            {
                output.WriteLine($"### FAULTED MESSAGE ###");
                output.WriteLine($"Message type: {fault.Context.SupportedMessageTypes.First()}");
                output.WriteLine($"Message destination: {fault.Context.DestinationAddress}");
                output.WriteLine(fault.Exception.ToString());
            }

            var receiveFaults = await busHarness.Sent
                .SelectExisting(static m => m.MessageObject is ReceiveFault)
                .Cast<ISentMessage<ReceiveFault>>()
                .ToListAsync(CancellationToken.None);

            foreach (var receiveFault in receiveFaults)
            {
                output.WriteLine($"### RECEIVE FAULT ###");
                output.WriteLine($"Message type: {receiveFault.Context.SupportedMessageTypes.First()}");
                output.WriteLine($"Message destination: {receiveFault.Context.DestinationAddress}");
                output.WriteLine(">>>>> MESSAGE OBJECT:");
                output.WriteLine(receiveFault.MessageObject.ToString() ?? "No details available.");
                output.WriteLine(">>>>> EXCEPTION DETAILS:");
                output.WriteLine(receiveFault.Exception?.ToString() ?? "No exception available.");
            }
        }

        await _factory.DisposeAsync().WaitAsync(timeout: TimeSpan.FromMinutes(5), timeProvider: TimeProvider.System);
        await ((IAsyncDisposable)_db).DisposeAsync();
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
