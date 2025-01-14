#nullable enable

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Altinn.Register.Contracts.Events;
using Altinn.Register.Core;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.UnitOfWork;
using MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Consumer for upserting parties from different sources in batches.
/// </summary>
public sealed class PartyImportBatchConsumer
    : IConsumer<Batch<UpsertValidatedPartyCommand>>
{
    private readonly IUnitOfWorkManager _uow;
    private readonly IImportJobTracker _tracker;
    private readonly ImportMeters _meters;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyImportBatchConsumer"/> class.
    /// </summary>
    public PartyImportBatchConsumer(IUnitOfWorkManager uow, IImportJobTracker tracker, RegisterTelemetry telemetry)
    {
        _uow = uow;
        _tracker = tracker;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <summary>
    /// Consumes a batch of upsert party commands.
    /// </summary>
    /// <param name="context">The consume context.</param>
    public async Task Consume(ConsumeContext<Batch<UpsertValidatedPartyCommand>> context)
    {
        await UpsertParties(context, context.Message, context.CancellationToken);
    }

    /// <summary>
    /// Upserts a set of parties.
    /// </summary>
    /// <param name="context">The <see cref="ConsumeContext"/>.</param>
    /// <param name="upserts">The parties to upsert.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    private async Task UpsertParties(
        ConsumeContext context,
        Batch<UpsertValidatedPartyCommand> upserts,
        CancellationToken cancellationToken)
    {
        PartyUpdatedEvent[]? evts = null;
        UpsertPartyTracking[]? tracking = null;
        try
        {
            int index = 0;
            evts = ArrayPool<PartyUpdatedEvent>.Shared.Rent(upserts.Length);
            tracking = ArrayPool<UpsertPartyTracking>.Shared.Rent(upserts.Length);

            {
                await using var uow = await _uow.CreateAsync(cancellationToken: cancellationToken);
                var persistence = uow.GetPartyPersistence();

                foreach (var upsert in upserts)
                {
                    var party = upsert.Message.Party;

                    // Note: even though the party has already been validated, we still run the validation logic here
                    // since it's cheap and gives much better error messages than the database layer. This is mostly
                    // to catch if anyone produces UpsertValidatedPartyCommand instances somewhere in the future without
                    // actually validating the party.
                    PartyImportHelper.ValidatePartyForUpset(party);

                    var result = await persistence.UpsertParty(party, cancellationToken);
                    result.EnsureSuccess();

                    UpdateTracking(ref tracking, upsert.Message.Tracking);
                    evts[index++] = new PartyUpdatedEvent
                    {
                        PartyUuid = result.Value.PartyUuid.Value,
                    };
                }

                await uow.CommitAsync(cancellationToken);
            }

            await context.PublishBatch(evts.Take(index), cancellationToken);

            foreach (var info in tracking)
            {
                if (info.JobName is null)
                {
                    break;
                }

                await _tracker.TrackProcessedStatus(info.JobName, new ImportJobProcessingStatus { ProcessedMax = info.Progress }, context.CancellationToken);
            }
        }
        finally
        {
            if (tracking is not null)
            {
                ArrayPool<UpsertPartyTracking>.Shared.Return(tracking);
            }

            if (evts is not null)
            {
                ArrayPool<PartyUpdatedEvent>.Shared.Return(evts);
            }
        }

        _meters.PartiesUpserted.Add(upserts.Length);
        _meters.BatchesSucceeded.Add(1);
        _meters.BatchSize.Record(upserts.Length);

        static void UpdateTracking(ref UpsertPartyTracking[] tracking, UpsertPartyTracking trackingInfo)
        {
            if (trackingInfo.JobName is null)
            {
                return;
            }

            for (var i = 0; i < tracking.Length; i++)
            {
                ref var existing = ref tracking[i];
                if (existing.JobName is null)
                {
                    existing = trackingInfo;
                    return;
                }

                if (existing.JobName == trackingInfo.JobName)
                {
                    if (existing.Progress < trackingInfo.Progress)
                    {
                        existing = trackingInfo;
                    }

                    return;
                }
            }

            Debug.Assert(false, "Tracking array is full.");
        }
    }

    /// <summary>
    /// Consumer definition for <see cref="PartyImportBatchConsumer"/>.
    /// </summary>
    public sealed class Definition
        : ConsumerDefinition<PartyImportBatchConsumer>
    {
        /// <inheritdoc/>
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<PartyImportBatchConsumer> consumerConfigurator,
            IRegistrationContext context)
        {
            base.ConfigureConsumer(endpointConfigurator, consumerConfigurator, context);

            endpointConfigurator.PrefetchCount = 500;

            consumerConfigurator.Options<BatchOptions>(options =>
            {
                options
                    .SetMessageLimit(100)
                    .SetTimeLimit(TimeSpan.FromSeconds(5))
                    .SetTimeLimitStart(BatchTimeLimitStart.FromFirst)
                    .SetConcurrencyLimit(3);
            });
        }
    }

    /// <summary>
    /// Meters for <see cref="PartyImportBatchConsumer"/>.
    /// </summary>
    private sealed class ImportMeters(RegisterTelemetry telemetry)
        : IServiceMeters<ImportMeters>
    {
        /// <summary>
        /// Gets a counter for the number of parties upserted.
        /// </summary>
        public Counter<int> PartiesUpserted { get; }
            = telemetry.CreateCounter<int>("register.party-import.party.upsert.succeeded.total", description: "The number of parties upserted.");

        /// <summary>
        /// Gets a counter for the number of party-batches upserted.
        /// </summary>
        public Counter<int> BatchesSucceeded { get; }
            = telemetry.CreateCounter<int>("register.party-import.party.batch.succeeded.total", description: "The number of party-batches upserted.");

        /// <summary>
        /// Gets a histogram for the size of party batches upserted.
        /// </summary>
        public Histogram<int> BatchSize { get; }
            = telemetry.CreateHistogram<int>("register.party-import.party.batch.size", description: "The size of party batches upserted.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
    }
}
