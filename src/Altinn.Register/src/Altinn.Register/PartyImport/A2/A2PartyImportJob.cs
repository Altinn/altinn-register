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
    private readonly TimeProvider _timeProvider;
    private readonly ImportMeters _meters;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportJob"/> class.
    /// </summary>
    public A2PartyImportJob(
        ILogger<A2PartyImportJob> logger,
        IImportJobTracker tracker,
        ICommandSender sender,
        IA2PartyImportService importService,
        TimeProvider timeProvider,
        RegisterTelemetry telemetry)
    {
        _logger = logger;
        _tracker = tracker;
        _sender = sender;
        _importService = importService;
        _timeProvider = timeProvider;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var partyImportStatus = await RunPartyImport(cancellationToken);

        if (partyImportStatus.SourceMax - partyImportStatus.ProcessedMax < 100)
        {
            // Less than 1 page of changes left to process, so we can start importing roles.
            await RunCCRRoleImport(partyImportStatus.ProcessedMax, cancellationToken);
        }
    }

    private async Task<ImportJobStatus> RunPartyImport(CancellationToken cancellationToken)
    {
        var start = _timeProvider.GetTimestamp();
        Log.StartingPartyImport(_logger);

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
            Log.EnqueuedPartiesForImport(_logger, page.Count);

            var enqueuedMax = page[^1].ChangeId;
            var sourceMax = page.LastKnownChangeId;
            progress = await TrackQueueStatus(JobNames.A2PartyImportParty, progress, new() { EnqueuedMax = enqueuedMax, SourceMax = sourceMax }, cancellationToken);
            _meters.PartiesEnqueued.Add(page.Count);

            if (enqueuedMax - progress.ProcessedMax > 50_000)
            {
                Log.PausingEnqueueingParties(_logger, enqueuedMax, progress.ProcessedMax);
                break;
            }
        }

        var duration = _timeProvider.GetElapsedTime(start);
        Log.FinishedPartyImport(_logger, duration);

        return progress;
    }

    private async Task<ImportJobStatus> RunCCRRoleImport(ulong maxChangeId, CancellationToken cancellationToken)
    {
        var start = _timeProvider.GetTimestamp();
        Log.StartingCCRRoleImport(_logger);

        using var activity = RegisterTelemetry.StartActivity("import a2-ccr-roles", ActivityKind.Internal);
        var progress = await _tracker.GetStatus(JobNames.A2PartyImportCCRRoleAssignments, cancellationToken);

        var changes = _importService.GetChanges(checked((uint)progress.EnqueuedMax), cancellationToken);
        await foreach (var page in changes.WithCancellation(cancellationToken))
        {
            if (page.Count == 0)
            {
                // Skip empty pages.
                continue;
            }

            if (page[^1].ChangeId > maxChangeId)
            {
                // We've progressed passed imported parties, return and let the full job run anew.
                break;
            }

            var cmds = page
                .Select(static update => new ImportA2CCRRolesCommand
                {
                    PartyId = checked((uint)update.PartyId),
                    PartyUuid = update.PartyUuid,
                    ChangedTime = update.ChangeTime,
                    ChangeId = update.ChangeId,
                })
                .ToList();

            await _sender.Send(cmds, cancellationToken);
            Log.EnqueuedPartiesForCCRRoleImport(_logger, cmds.Count);

            var enqueuedMax = page[^1].ChangeId;
            var sourceMax = page.LastKnownChangeId;
            progress = await TrackQueueStatus(JobNames.A2PartyImportCCRRoleAssignments, progress, new() { EnqueuedMax = enqueuedMax, SourceMax = sourceMax }, cancellationToken);
            _meters.OrganizationCCRRolesEnqueued.Add(cmds.Count);

            if (enqueuedMax - progress.ProcessedMax > 50_000)
            {
                Log.PausingEnqueueingCCRRoles(_logger, enqueuedMax, progress.ProcessedMax);
                break;
            }
        }

        var duration = _timeProvider.GetElapsedTime(start);
        Log.FinishedCCRRoleImport(_logger, duration);

        return progress;
    }

    private async Task<ImportJobStatus> TrackQueueStatus(string name, ImportJobStatus current, ImportJobQueueStatus newStatus, CancellationToken cancellationToken)
    {
        var (newProgress, _) = await _tracker.TrackQueueStatus(name, newStatus, cancellationToken);

        // TODO: this is working around a bug that currently exists in the tracker where the status returned is for whatever reason lower than the current status.
        // This should be removed once the bug is fixed.
        var sourceMax = Math.Max(current.SourceMax, newProgress.SourceMax);
        var enqueuedMax = Math.Max(current.EnqueuedMax, newProgress.EnqueuedMax);
        var processedMax = Math.Max(current.ProcessedMax, newProgress.ProcessedMax);
        return new() { SourceMax = sourceMax, EnqueuedMax = enqueuedMax, ProcessedMax = processedMax };
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Enqueued {Count} parties for import.")]
        public static partial void EnqueuedPartiesForImport(ILogger logger, int count);

        [LoggerMessage(1, LogLevel.Information, "More than 50'000 parties in queue since last measurement. Pausing enqueueing. EnqueuedMax = {EnqueuedMax}, ProcessedMax = {ProcessedMax}.")]
        public static partial void PausingEnqueueingParties(ILogger logger, ulong enqueuedMax, ulong processedMax);

        [LoggerMessage(2, LogLevel.Information, "Enqueued {Count} parties for CCR role import.")]
        public static partial void EnqueuedPartiesForCCRRoleImport(ILogger logger, int count);

        [LoggerMessage(3, LogLevel.Information, "Starting party import.")]
        public static partial void StartingPartyImport(ILogger logger);

        [LoggerMessage(4, LogLevel.Information, "Finished party import in {Duration}.")]
        public static partial void FinishedPartyImport(ILogger logger, TimeSpan duration);

        [LoggerMessage(5, LogLevel.Information, "Starting CCR role import.")]
        public static partial void StartingCCRRoleImport(ILogger logger);

        [LoggerMessage(6, LogLevel.Information, "Finished CCR role import in {Duration}.")]
        public static partial void FinishedCCRRoleImport(ILogger logger, TimeSpan duration);

        [LoggerMessage(7, LogLevel.Information, "More than 50'000 parties in queue since last measurement. Pausing enqueueing. EnqueuedMax = {EnqueuedMax}, ProcessedMax = {ProcessedMax}.")]
        public static partial void PausingEnqueueingCCRRoles(ILogger logger, ulong enqueuedMax, ulong processedMax);
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

        public Counter<int> OrganizationCCRRolesEnqueued { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.ccr.role-assignments.enqueued", "The number of parties enqueued to be imported ccr role-assignments from A2.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
    }
}
