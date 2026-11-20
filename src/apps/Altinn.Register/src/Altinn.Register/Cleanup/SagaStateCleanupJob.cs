using Altinn.Authorization.ServiceDefaults.Jobs;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.UnitOfWork;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Cleanup;

/// <summary>
/// Represents a scheduled job responsible for cleaning up saga state data within the system.
/// </summary>
public sealed partial class SagaStateCleanupJob
    : Job
    , IHasJobName<SagaStateCleanupJob>
{
    /// <inheritdoc/>
    public static string JobName => JobNames.SagaStateCleanup;

    private readonly IUnitOfWorkManager _manager;
    private readonly IOptionsMonitor<SagaStateCleanupSettings> _settings;
    private readonly ILogger<SagaStateCleanupJob> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SagaStateCleanupJob"/> class.
    /// </summary>
    public SagaStateCleanupJob(
        IUnitOfWorkManager manager,
        IOptionsMonitor<SagaStateCleanupSettings> settings,
        ILogger<SagaStateCleanupJob> logger,
        TimeProvider timeProvider)
    {
        _manager = manager;
        _settings = settings;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    protected override async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.CurrentValue;

        var now = _timeProvider.GetUtcNow();
        var completedCutoff = now - TimeSpan.FromDays(settings.DeleteCompletedSagaStatesAfterDays);
        var faultedCutoff = now - TimeSpan.FromDays(settings.DeleteFaultedSagaStatesAfterDays);
        var inProgressCutoff = now - TimeSpan.FromDays(settings.DeleteInProgressSagaStatesAfterDays);

        long totalDeleted = 0;
        int deleted;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var uow = await _manager.CreateAsync(cancellationToken, activityName: "cleanup saga states");
            var cleanup = uow.GetRequiredService<ISagaStateCleanup>();

            deleted = await cleanup.DeleteOldStates(
                batchSize: settings.BatchSize,
                completedBefore: completedCutoff,
                faultedBefore: faultedCutoff,
                inProgressBefore: inProgressCutoff,
                cancellationToken);

            await uow.CommitAsync(cancellationToken);
            totalDeleted += deleted;
            Log.DeletedSagaStateBatch(_logger, deleted);
        }
        while (deleted == settings.BatchSize);

        Log.DeletedSagaStates(_logger, totalDeleted, completedCutoff, faultedCutoff, inProgressCutoff);
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Deleted {Deleted} saga states. Completed before {CompletedCutoff}, faulted before {FaultedCutoff}, in-progress before {InProgressCutoff}.")]
        public static partial void DeletedSagaStates(
            ILogger logger,
            long deleted,
            DateTimeOffset completedCutoff,
            DateTimeOffset faultedCutoff,
            DateTimeOffset inProgressCutoff);

        [LoggerMessage(1, LogLevel.Debug, "Deleted {Deleted} saga states in batch.")]
        public static partial void DeletedSagaStateBatch(
            ILogger logger,
            int deleted);
    }
}
