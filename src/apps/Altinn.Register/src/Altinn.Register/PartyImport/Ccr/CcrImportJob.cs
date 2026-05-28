using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.IO.Pipelines;
using Altinn.Authorization.ServiceDefaults.Jobs;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Authorization.ServiceDefaults.Telemetry;
using Altinn.Register.Core;
using Altinn.Register.Core.Ccr;
using Altinn.Register.Core.ImportJobs;

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
    private readonly ICcrDataTransfer _sftpClient;
    private readonly JobCleanupHelper _cleanupHelper;
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
        ICcrDataTransfer client,
        JobCleanupHelper cleanupHelper,
        TimeProvider timeProvider,
        IMetricsProvider metricsProvider,
        ICcrFlatFileProcessor fileproc)
    {
        _logger = logger;
        _tracker = tracker;
        _sender = sender;
        _sftpClient = client;
        _cleanupHelper = cleanupHelper;
        _timeProvider = timeProvider;
        _meters = metricsProvider.Get<ImportMeters>();
        _fileproc = fileproc;
    }

    /// <inheritdoc/>
    protected override async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var track = await _tracker.GetStatus(JobName, cancellationToken);
        int lastrunId = (int)track.ProcessedMax;

        var pipe = new Pipe();
        PipeWriter writer = pipe.Writer;
        PipeReader reader = pipe.Reader;

        Log.StartingPartyImport(_logger);

        using var activity = RegisterTelemetry.StartActivity("import ccr parties from file", ActivityKind.Internal);

        bool fileReadSuccessfully = await _sftpClient.GetNextFileAsync(writer, lastrunId, cancellationToken);

        if (!fileReadSuccessfully)
        {
            Log.FinishedPartyImport(_logger, TimeSpan.Zero);
            return;
        }

        var parties = await _fileproc
            .ProcessCcrFlatFile(reader, cancellationToken)
            .ToListAsync(cancellationToken);

        // TODO: put on the bus
        foreach (var party in parties)
        {
        }

        Log.FinishedPartyImport(_logger, TimeSpan.Zero);
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
}
