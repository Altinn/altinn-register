#nullable enable

using System.Diagnostics.Metrics;
using Altinn.Register.Contracts.Parties;
using Altinn.Register.Core;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Utils;
using MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Consumer for upserting user records.
/// </summary>
public sealed partial class UpsertUserRecordConsumer
    : IConsumer<UpsertUserRecordCommand>
{
    private readonly ILogger<UpsertUserRecordConsumer> _logger;
    private readonly IUnitOfWorkManager _uow;
    private readonly IImportJobTracker _tracker;
    private readonly ImportMeters _meters;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertUserRecordConsumer"/> class.
    /// </summary>
    public UpsertUserRecordConsumer(
        ILogger<UpsertUserRecordConsumer> logger,
        IUnitOfWorkManager uow,
        IImportJobTracker tracker,
        RegisterTelemetry telemetry)
    {
        _logger = logger;
        _uow = uow;
        _tracker = tracker;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<UpsertUserRecordCommand> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        await using var uow = await _uow.CreateAsync(cancellationToken);
        var persistence = uow.GetPartyPersistence();

        var result = await persistence.UpsertUserRecord(
            partyUuid: message.PartyUuid,
            userId: message.UserId,
            username: message.Username,
            isActive: message.IsActive,
            cancellationToken);

        result.EnsureSuccess();
        if (result.Value.PartyUpdated)
        {
            await context.Publish(
                new PartyUpdatedEvent
                {
                    Party = message.PartyUuid.ToPartyReferenceContract(),
                },
                cancellationToken);
        }

        if (!string.IsNullOrEmpty(message.Tracking.JobName))
        {
            await _tracker.TrackProcessedStatus(
                message.Tracking.JobName,
                new ImportJobProcessingStatus { ProcessedMax = message.Tracking.Progress },
                cancellationToken);
            Log.TrackingProgressUpdated(_logger, message.Tracking.JobName, message.Tracking.Progress);
        }

        await uow.CommitAsync(cancellationToken);
        _meters.UserRecordsUpserted.Add(1);
    }

    /// <summary>
    /// Meters for <see cref="UpsertUserRecordConsumer"/>.
    /// </summary>
    private sealed class ImportMeters(RegisterTelemetry telemetry)
        : IServiceMeters<ImportMeters>
    {
        public Counter<int> UserRecordsUpserted { get; }
            = telemetry.CreateCounter<int>("register.party-import.user.upsert.succeeded.total", description: "The number of users upserted.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Trace, "Updating progress tracking for job '{JobName}' with progress {Progress}.")]
        public static partial void TrackingProgressUpdated(ILogger logger, string jobName, ulong progress);
    }

    /// <summary>
    /// Consumer definition for <see cref="UpsertUserRecordConsumer"/>.
    /// </summary>
    public sealed class Definition
        : ConsumerDefinition<UpsertUserRecordConsumer>
    {
        /// <inheritdoc/>
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<UpsertUserRecordConsumer> consumerConfigurator,
            IRegistrationContext context)
        {
            base.ConfigureConsumer(endpointConfigurator, consumerConfigurator, context);

            consumerConfigurator.UseConcurrentMessageLimit(10, endpointConfigurator);
        }
    }
}
