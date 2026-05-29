using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.Jobs;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Authorization.ServiceDefaults.Telemetry;
using Altinn.Register.Core.Ccr;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.ImportJobs.FileProcessing;

namespace Altinn.Register.PartyImport.Ccr;

/// <summary>
/// A job that imports CCR data.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed partial class CcrImportJob
    : Job
    , IHasJobName<CcrImportJob>
{
    /// <inheritdoc/>
    public static string JobName => JobNames.CcrImport;

    private readonly ILogger<CcrImportJob> _logger;
    private readonly IImportJobTracker _tracker;
    private readonly ICommandSender _sender;
    private readonly ICcrFlatFileService _ccrFlatFileService;
    private readonly TimeProvider _timeProvider;
    private readonly ImportMeters _meters;
    private readonly ICcrFlatFileProcessor _fileproc;

    /// <summary>
    /// Initializes a new instance of the <see cref="CcrImportJob"/> class.
    /// </summary>
    public CcrImportJob(
        ILogger<CcrImportJob> logger,
        IImportJobTracker tracker,
        ICommandSender sender,
        ICcrFlatFileService ccrFlatFileService,
        JobCleanupHelper cleanupHelper,
        TimeProvider timeProvider,
        IMetricsProvider metricsProvider,
        ICcrFlatFileProcessor fileproc)
    {
        _logger = logger;
        _tracker = tracker;
        _sender = sender;
        _ccrFlatFileService = ccrFlatFileService;
        _timeProvider = timeProvider;
        _meters = metricsProvider.Get<ImportMeters>();
        _fileproc = fileproc;
    }

    /// <inheritdoc/>
    protected override async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var start = _timeProvider.GetTimestamp();

        var track = await _tracker.GetStatus(JobName, cancellationToken);
        uint lastRunId = checked((uint)track.EnqueuedMax);

        var processor = new FileProcessor(_sender, _fileproc, _tracker, _meters);

        await _ccrFlatFileService.ProcessNextFile(processor, lastRunId, cancellationToken);

        Log.FinishedPartyImport(_logger, _timeProvider.GetElapsedTime(start));
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Enqueued {Count} parties for import.")]
        public static partial void EnqueuedPartiesForImport(ILogger logger, int count);

        [LoggerMessage(3, LogLevel.Information, "Starting party import.")]
        public static partial void StartingPartyImport(ILogger logger);

        [LoggerMessage(4, LogLevel.Information, "Finished party import in {Duration}.")]
        public static partial void FinishedPartyImport(ILogger logger, TimeSpan duration);
    }

    /// <summary>
    /// Meters for <see cref="CcrImportJob"/>.
    /// </summary>
    private sealed class ImportMeters(Meter meter)
        : IMetrics<ImportMeters>
    {
        /// <summary>
        /// Gets a counter for the number of parties imported from NPR.
        /// </summary>
        public Counter<int> PartiesEnqueued { get; }
            = meter.CreateCounter<int>("altinn.register.party-import.ccr.parties.enqueued", "The number of parties enqueued to be imported from CCR/ER.");

        /// <inheritdoc/>
        public static ImportMeters Create(Meter meter)
            => new ImportMeters(meter);
    }

    private sealed class FileProcessor(ICommandSender sender, ICcrFlatFileProcessor processor, IImportJobTracker tracker, ImportMeters meter)
        : IFileProcessor<CcrOpenedFileInfo>
    {
        public async Task ProcessFileAsync(CcrOpenedFileInfo fileInfo, CancellationToken cancellationToken)
        {
            await foreach (var item in processor.ProcessCcrFlatFile(fileInfo.Reader, cancellationToken))
            {
                var cmd = new ImportCcrXmlCommand
                {
                    BatchId = fileInfo.SequenceNumber,
                    OrganizationIdentifier = item.OrganizationIdentifier,
                    Document = item.Document.ToArray(),
                };

                await sender.Send(cmd, cancellationToken);
                meter.PartiesEnqueued.Add(1);
            }

            await tracker.TrackQueueStatus(JobName, new() { SourceMax = null, EnqueuedMax = fileInfo.SequenceNumber }, cancellationToken);
        }
    }
}
