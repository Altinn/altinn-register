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
        using var activity = RegisterTelemetry.StartActivity("import a2-parties", ActivityKind.Internal);
        var progress = await _tracker.GetStatus(JobNames.A2PartyImportParty, cancellationToken);

        var changes = _importService.GetChanges(checked((uint)progress.EnqueuedMax), cancellationToken);
        await foreach (var batch in changes.Chunk(100))
        {
            var cmd = new ImportA2PartyBatchCommand
            {
                Items = batch.Select(static c => new ImportA2PartyBatchCommand.Item
                {
                    ChangedTime = c.ChangeTime,
                    ChangeId = c.ChangeId,
                    PartyUuid = c.PartyUuid,
                }).ToList(),
            };

            await _sender.Send(cmd, cancellationToken);

            var lastChangeId = batch[^1].ChangeId;
            var sourceMax = await changes.GetLastChangeId(cancellationToken);
            await _tracker.TrackQueueStatus(JobNames.A2PartyImportParty, new() { EnqueuedMax = lastChangeId, SourceMax = sourceMax }, cancellationToken);
        }
    }

    private static partial class Log
    {
    }
}
