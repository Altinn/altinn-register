#nullable enable

using System.Collections.Frozen;
using Altinn.Authorization.ServiceDefaults.Jobs;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// A job that imports user-ids from A2 for parties already imported into A3.
/// </summary>
internal sealed partial class A2PartyUserIdImportJob
    : Job
{
    /// <summary>
    /// The job name.
    /// </summary>
    internal const string JOB_NAME = JobNames.A2PartyUserIdImport;

    private readonly static FrozenSet<PartyRecordType> _partyTypes = [
        PartyRecordType.Person, 
        PartyRecordType.SelfIdentifiedUser,
    ];

    private readonly ILogger<A2PartyUserIdImportJob> _logger;
    private readonly IImportJobTracker _tracker;
    private readonly ICommandSender _sender;
    private readonly IUnitOfWorkManager _uowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyUserIdImportJob"/> class.
    /// </summary>
    public A2PartyUserIdImportJob(
        ILogger<A2PartyUserIdImportJob> logger,
        IImportJobTracker tracker,
        ICommandSender sender,
        IUnitOfWorkManager uowManager)
    {
        _logger = logger;
        _tracker = tracker;
        _sender = sender;
        _uowManager = uowManager;
    }

    /// <inheritdoc/>
    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        const int CHUNK_SIZE = 100;

        await using var uow = await _uowManager.CreateAsync(cancellationToken, activityName: nameof(A2PartyUserIdImportJob));
        var service = uow.GetRequiredService<IUserIdImportJobService>();
        var progress = await _tracker.GetStatus(JobNames.A2PartyUserIdImport, cancellationToken);

        var enqueuedMax = progress.EnqueuedMax;
        var startEnqueuedMax = enqueuedMax;
        List<ImportA2UserIdForPartyCommand> messages = new(100);
        await foreach (var party in service.GetPartiesWithoutUserIdAndJobState(JOB_NAME, _partyTypes, cancellationToken))
        {
            if (messages.Count == 0)
            {
                // we update the tracker early, cause it's just for checking the progress for this job, and has no functional use.
                // but there is a database constraint that processed_max <= enqueued_max, so we just make sure enqueued_max is bigger.
                (progress, _) = await _tracker.TrackQueueStatus(JOB_NAME, new ImportJobQueueStatus { EnqueuedMax = enqueuedMax + CHUNK_SIZE, SourceMax = null }, cancellationToken);
            }

            enqueuedMax += 1;
            messages.Add(new ImportA2UserIdForPartyCommand { PartyUuid = party.PartyUuid, PartyType = party.PartyType, Tracking = new(JOB_NAME, enqueuedMax) });

            if (messages.Count >= CHUNK_SIZE)
            {
                (progress, _) = await _tracker.TrackQueueStatus(JOB_NAME, new ImportJobQueueStatus { EnqueuedMax = enqueuedMax, SourceMax = null }, cancellationToken);
                await SendAndUpdateState(JOB_NAME, messages, cancellationToken);
                messages.Clear();

                if (enqueuedMax - progress.ProcessedMax > 50_000)
                {
                    Log.PausingEnqueueing(_logger, enqueuedMax, progress.ProcessedMax);
                    break;
                }
            }
        }

        await _tracker.TrackQueueStatus(JOB_NAME, new ImportJobQueueStatus { EnqueuedMax = enqueuedMax, SourceMax = null }, cancellationToken);
        if (messages.Count > 0)
        {
            await SendAndUpdateState(JOB_NAME, messages, cancellationToken);
        }
        else if (startEnqueuedMax == enqueuedMax && progress.ProcessedMax == enqueuedMax)
        {
            // we've processed all parties, so we can clear up temporary state.
            await service.ClearJobStateForPartiesWithUserId(JOB_NAME, cancellationToken);
        }
    }

    private async Task SendAndUpdateState(
        string jobName,
        IEnumerable<ImportA2UserIdForPartyCommand> messages,
        CancellationToken cancellationToken)
    {
        await using var uow = await _uowManager.CreateAsync(cancellationToken);
        var persistence = uow.GetRequiredService<IImportJobStatePersistence>();

        foreach (var msg in messages)
        {
            await persistence.SetPartyState(jobName, msg.PartyUuid, new ImportPartyUserIdJobState { }, cancellationToken);
        }

        await _sender.Send(messages, cancellationToken);
        await uow.CommitAsync(cancellationToken);
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Pausing enqueueing of A2PartyUserIdImport job. Enqueued: {Enqueued}, Processed: {Processed}")]
        public static partial void PausingEnqueueing(ILogger logger, ulong enqueued, ulong processed);
    }
}
