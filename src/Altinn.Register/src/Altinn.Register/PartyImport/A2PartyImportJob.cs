#nullable enable

using System.Diagnostics;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Extensions;
using Altinn.Register.Jobs;

namespace Altinn.Register.PartyImport;

/// <summary>
/// A job that imports parties from A2.
/// </summary>
public sealed partial class A2PartyImportJob
    : IJob
{
    private const int MAX_UNPROCESSED = 10_000;
    private const int RECORD_EVERY = 10;

    private readonly ILogger<A2PartyImportJob> _logger;
    private readonly IImportJobTracker _tracker;
    private readonly ICommandSender _sender;
    private readonly IA2PartyImportService _importService;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportJob"/> class.
    /// </summary>
    public A2PartyImportJob(
        ILogger<A2PartyImportJob> logger,
        IImportJobTracker tracker,
        ICommandSender sender,
        IA2PartyImportService importService)
    {
        _logger = logger;
        _tracker = tracker;
        _sender = sender;
        _importService = importService;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var activity = RegisterTelemetry.StartActivity(ActivityKind.Internal, "import a2-parties");
        var progress = await _tracker.GetStatus(JobNames.A2PartyImportParty, cancellationToken);

        if (progress.Unprocessed >= MAX_UNPROCESSED)
        {
            Log.TooManyUnprocessedJobs(_logger, JobNames.A2PartyImportParty, progress);
            return;
        }

        var unrecorded = 0;
        uint lastChangeId = 0;

        var changes = _importService.GetChanges(checked((uint)progress.EnqueuedMax), cancellationToken);
        await foreach (var change in changes)
        {
            var cmd = new ImportA2PartyCommand
            {
                ChangedTime = change.ChangeTime,
                ChangeId = change.ChangeId,
                PartyUuid = change.PartyUuid,
            };

            await _sender.Send(cmd, cancellationToken);

            unrecorded++;
            lastChangeId = change.ChangeId;
            if (unrecorded >= RECORD_EVERY)
            {
                var sourceMax = await changes.GetLastChangeId(cancellationToken);
                await _tracker.TrackQueueStatus(JobNames.A2PartyImportParty, new() { EnqueuedMax = lastChangeId, SourceMax = sourceMax }, cancellationToken);
                unrecorded = 0;
            }
        }

        if (unrecorded > 0)
        {
            var sourceMax = await changes.GetLastChangeId(cancellationToken);
            await _tracker.TrackQueueStatus(JobNames.A2PartyImportParty, new() { EnqueuedMax = lastChangeId, SourceMax = sourceMax }, cancellationToken);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Warning, "Too many unprocessed items in job {job} - source contains {source} items, enqueued {enqueued} items for processing, and {processed} items has been processed")]
        private static partial void TooManyUnprocessedJobs(ILogger logger, string job, ulong source, ulong enqueued, ulong processed);

        public static void TooManyUnprocessedJobs(ILogger logger, string job, ImportJobStatus status)
            => TooManyUnprocessedJobs(logger, job, status.SourceMax, status.EnqueuedMax, status.ProcessedMax);
    }
}
