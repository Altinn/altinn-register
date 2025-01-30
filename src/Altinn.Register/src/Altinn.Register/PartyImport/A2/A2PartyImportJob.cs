#nullable enable

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Jobs;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// A job that imports parties from A2.
/// </summary>
public sealed partial class A2PartyImportJob
    : IJob
{
    private readonly ILogger<A2PartyImportJob> _logger;
    private readonly IImportJobTracker _tracker;
    private readonly ICommandSender _sender;
    private readonly IA2PartyImportService _importService;
    private readonly ImportMeters _meters;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportJob"/> class.
    /// </summary>
    public A2PartyImportJob(
        ILogger<A2PartyImportJob> logger,
        IImportJobTracker tracker,
        ICommandSender sender,
        IA2PartyImportService importService,
        RegisterTelemetry telemetry)
    {
        _logger = logger;
        _tracker = tracker;
        _sender = sender;
        _importService = importService;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var activity = RegisterTelemetry.StartActivity("import a2-parties", ActivityKind.Internal);
        var progress = await _tracker.GetStatus(JobNames.A2PartyImportParty, cancellationToken);

        var changes = _importService.GetChanges(checked((uint)progress.EnqueuedMax), cancellationToken);
        await foreach (var page in changes.WithCancellation(cancellationToken))
        {
            if (page.Count == 0)
            {
                // Skip empty pages.
                continue;
            }

            var cmds = page.Select(static update => new ImportA2PartyCommand
            {
                PartyUuid = update.PartyUuid,
                ChangedTime = update.ChangeTime,
                ChangeId = update.ChangeId,
            });

            await _sender.Send(cmds, cancellationToken);
            Log.EnqueuedParties(_logger, page.Count);

            var enqueuedMax = page[^1].ChangeId;
            var sourceMax = page.LastKnownChangeId;
            (progress, _) = await _tracker.TrackQueueStatus(JobNames.A2PartyImportParty, new() { EnqueuedMax = enqueuedMax, SourceMax = sourceMax }, cancellationToken);
            _meters.PartiesEnqueued.Add(page.Count);

            if (enqueuedMax - progress.ProcessedMax > 50_000)
            {
                Log.PausingEnqueueing(_logger);
                break;
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Enqueued {Count} parties for import.")]
        public static partial void EnqueuedParties(ILogger logger, int count);

        [LoggerMessage(1, LogLevel.Information, "More than 10'000 parties in queue since last measurement. Pausing enqueueing.")]
        public static partial void PausingEnqueueing(ILogger logger);
    }

    /// <summary>
    /// Meters for <see cref="A2PartyImportJob"/>.
    /// </summary>
    private sealed class ImportMeters(RegisterTelemetry telemetry)
        : IServiceMeters<ImportMeters>
    {
        /// <summary>
        /// Gets a counter for the number of parties imported from A2.
        /// </summary>
        public Counter<int> PartiesEnqueued { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.parties.enqueued", "The number of parties enqueued to be imported from A2.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
    }
}
