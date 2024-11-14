using Altinn.Register.Jobs;
using Altinn.Register.Tests.Utils;
using Altinn.Register.TestUtils;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Tests.Jobs;

public class RecurringJobHostedServiceTests
    : DatabaseTestBase
{
    private static readonly ObjectFactory<RecurringJobHostedService> _factory
        = ActivatorUtilities.CreateFactory<RecurringJobHostedService>([typeof(IEnumerable<JobRegistration>)]);

    protected override bool SeedData => false;

    protected override async ValueTask ConfigureServices(IServiceCollection services)
    {
        await base.ConfigureServices(services);

        services.AddLeaseManager();
    }

    [Fact]
    public async Task CanRun_WithNo_JobRegistrations()
    {
        using var sut = CreateService([]);

        await Start(sut);
        await Stop(sut);
    }

    private RecurringJobHostedService CreateService(IEnumerable<JobRegistration> registrations)
        => _factory(Services, [registrations]);

    private static async Task Start(RecurringJobHostedService service, CancellationToken cancellationToken = default)
    {
        await service.StartingAsync(cancellationToken);
        await service.StartAsync(cancellationToken);
        await service.StartedAsync(cancellationToken);
    }

    private static async Task Stop(RecurringJobHostedService service, CancellationToken cancellationToken = default)
    {
        await service.StoppingAsync(cancellationToken);
        await service.StopAsync(cancellationToken);
        await service.StoppedAsync(cancellationToken);
    }

    private sealed class CounterJob(AtomicCounter counter)
        : IJob
    {
        public Task RunAsync(CancellationToken cancellationToken)
        {
            counter.Increment();

            return Task.CompletedTask;
        }
    }
}
