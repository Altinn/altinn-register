#nullable enable

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.Jobs;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.PartyImport.A2;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// A job that imports CCR role assignments from A2.
/// </summary>
public sealed partial class A2PartyCCRRolesImportJob
    : Job
    , IHasJobName<A2PartyCCRRolesImportJob>
{
    /// <inheritdoc/>
    public static string JobName => JobNames.A2PartyImportCCRRoleAssignments;

    private readonly ILogger<A2PartyCCRRolesImportJob> _logger;
    private readonly IImportJobTracker _tracker;
    private readonly ICommandSender _sender;
    private readonly IA2PartyImportService _importService;
    private readonly JobCleanupHelper _cleanupHelper;
    private readonly TimeProvider _timeProvider;
    private readonly ImportMeters _meters;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyCCRRolesImportJob"/> class.
    /// </summary>
    public A2PartyCCRRolesImportJob(
        ILogger<A2PartyCCRRolesImportJob> logger,
        IImportJobTracker tracker,
        ICommandSender sender,
        IA2PartyImportService importService,
        JobCleanupHelper cleanupHelper,
        TimeProvider timeProvider,
        RegisterTelemetry telemetry)
    {
        _logger = logger;
        _tracker = tracker;
        _sender = sender;
        _importService = importService;
        _cleanupHelper = cleanupHelper;
        _timeProvider = timeProvider;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <inheritdoc/>
    protected override async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var start = _timeProvider.GetTimestamp();
        Log.StartingCCRRoleImport(_logger);

        using var activity = RegisterTelemetry.StartActivity("import a2-ccr-roles", ActivityKind.Internal);
        var progress = await _tracker.GetStatus(JobName, cancellationToken);
        var partyProgress = await _tracker.GetStatus(A2PartyImportJob.JobName, cancellationToken);
        var maxChangeId = partyProgress.ProcessedMax;
        Log.RoleImportInitialProgress(_logger, in progress, maxChangeId);
        var startEnqueuedMax = progress.EnqueuedMax;

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
            progress = await TrackQueueStatus(JobName, progress, new() { EnqueuedMax = enqueuedMax, SourceMax = sourceMax }, cancellationToken);
            _meters.OrganizationCCRRolesEnqueued.Add(cmds.Count);

            if (enqueuedMax - progress.ProcessedMax > 50_000)
            {
                Log.PausingEnqueueingCCRRoles(_logger, enqueuedMax, progress.ProcessedMax);
                break;
            }
        }

        var duration = _timeProvider.GetElapsedTime(start);
        Log.FinishedCCRRoleImport(_logger, duration);

        await _cleanupHelper.MaybeRunCleanup(JobName, startEnqueuedMax, in progress, cancellationToken);
    }

    private async Task<ImportJobStatus> TrackQueueStatus(string name, ImportJobStatus current, ImportJobQueueStatus newStatus, CancellationToken cancellationToken)
    {
        var (newProgress, _) = await _tracker.TrackQueueStatus(name, newStatus, cancellationToken);

        // TODO: this is working around a bug that currently exists in the tracker where the status returned is for whatever reason lower than the current status.
        // This should be removed once the bug is fixed.
        var sourceMax = NullMax(current.SourceMax, newProgress.SourceMax);
        var enqueuedMax = Math.Max(current.EnqueuedMax, newProgress.EnqueuedMax);
        var processedMax = Math.Max(current.ProcessedMax, newProgress.ProcessedMax);
        return new() { SourceMax = sourceMax, EnqueuedMax = enqueuedMax, ProcessedMax = processedMax };

        static ulong? NullMax(ulong? val1, ulong? val2)
            => (val1, val2) switch
            {
                (null, null) => null,
                (null, ulong u) => u,
                (ulong u, null) => u,
                (ulong u1, ulong u2) => Math.Max(u1, u2),
            };
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Enqueued {Count} parties for CCR role import.")]
        public static partial void EnqueuedPartiesForCCRRoleImport(ILogger logger, int count);

        [LoggerMessage(2, LogLevel.Information, "Starting CCR role import.")]
        public static partial void StartingCCRRoleImport(ILogger logger);

        [LoggerMessage(3, LogLevel.Information, "Finished CCR role import in {Duration}.")]
        public static partial void FinishedCCRRoleImport(ILogger logger, TimeSpan duration);

        [LoggerMessage(4, LogLevel.Information, "More than 50'000 parties in queue since last measurement. Pausing enqueueing. EnqueuedMax = {EnqueuedMax}, ProcessedMax = {ProcessedMax}.")]
        public static partial void PausingEnqueueingCCRRoles(ILogger logger, ulong enqueuedMax, ulong processedMax);

        [LoggerMessage(5, LogLevel.Information, "Party CCR roles import initial progress: EnqueuedMax = {EnqueuedMax}, SourceMax = {SourceMax}, ProcessedMax = {ProcessedMax}, MaxChangeId = {MaxChangeId}.")]
        private static partial void RoleImportInitialProgress(ILogger logger, ulong enqueuedMax, ulong? sourceMax, ulong processedMax, ulong maxChangeId);

        public static void RoleImportInitialProgress(ILogger logger, in ImportJobStatus status, ulong maxChangeId)
            => RoleImportInitialProgress(logger, status.EnqueuedMax, status.SourceMax, status.ProcessedMax, maxChangeId);
    }

    /// <summary>
    /// Meters for <see cref="A2PartyImportJob"/>.
    /// </summary>
    private sealed class ImportMeters(RegisterTelemetry telemetry)
        : IServiceMeters<ImportMeters>
    {
        public Counter<int> OrganizationCCRRolesEnqueued { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.ccr.role-assignments.enqueued", "The number of parties enqueued to be imported ccr role-assignments from A2.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
    }
}
