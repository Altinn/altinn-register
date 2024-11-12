using System.Diagnostics;
using Altinn.Register.Core;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Leases;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Hosted service for importing parties from A2.
/// </summary>
public sealed partial class A2PartyImportHostedService
    : BackgroundService
{
    private readonly TimeProvider _timeProvider;
    private readonly LeaseManager _leaseManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<A2PartyImportHostedService> _logger;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using (var lease = await _leaseManager.AcquireLease(Leases.A2PartyImport, stoppingToken))
            {
                if (lease is not null)
                {
                    using var activity = RegisterActivitySource.StartActivity(ActivityKind.Internal, "import a2-parties");
                    await using var scope = _serviceScopeFactory.CreateAsyncScope();
                    await RunImport(scope.ServiceProvider, lease.Token);
                }
            }

            await _timeProvider.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task RunImport(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var progressTracker = services.GetRequiredService<IImportJobTracker>();
        var progress = await progressTracker.GetStatus(Jobs.A2PartyImportParty, cancellationToken);

        if (progress.Unprocessed > 10)
        {
            Log.TooManyUnprocessedJobs(_logger, Jobs.A2PartyImportParty, progress);
            return;
        }

        // not yet implemented
        return;
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Warning, "Too many unprocessed items in job {job} - source contains {source} items, enqueued {enqueued} items for processing, and {processed} items has been processed", EventName = "TooManyUnprocessedJobs")]
        private static partial void TooManyUnprocessedJobs(ILogger logger, string job, ulong source, ulong enqueued, ulong processed);

        public static void TooManyUnprocessedJobs(ILogger logger, string job, ImportJobStatus status)
            => TooManyUnprocessedJobs(logger, job, status.SourceMax, status.EnqueuedMax, status.ProcessedMax);
    }
}
