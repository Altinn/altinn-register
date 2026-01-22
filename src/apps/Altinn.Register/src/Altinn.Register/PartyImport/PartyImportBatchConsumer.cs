#nullable enable

using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.Telemetry;
using Altinn.Register.Contracts;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Contracts.Parties;
using Altinn.Register.Core;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Utils;
using CommunityToolkit.Diagnostics;
using MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Consumer for upserting parties from different sources in batches.
/// </summary>
public sealed partial class PartyImportBatchConsumer
    : IConsumer<UpsertValidatedPartyCommand>
    , IConsumer<UpsertPartyUserCommand>
    , IConsumer<UpsertExternalRoleAssignmentsCommand>
{
    private const int BATCH_SIZE = 10;

    private readonly ILogger<PartyImportBatchConsumer> _logger;
    private readonly IUnitOfWorkManager _uow;
    private readonly IImportJobTracker _tracker;
    private readonly ImportMeters _meters;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyImportBatchConsumer"/> class.
    /// </summary>
    public PartyImportBatchConsumer(
        ILogger<PartyImportBatchConsumer> logger,
        IUnitOfWorkManager uow,
        IImportJobTracker tracker,
        IMetricsProvider metricsProvider)
    {
        _logger = logger;
        _uow = uow;
        _tracker = tracker;
        _meters = metricsProvider.Get<ImportMeters>();
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<UpsertValidatedPartyCommand> context)
    {
        await UpsertParties([context], context.CancellationToken);
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<UpsertPartyUserCommand> context)
    {
        await UpsertPartyUsers([context], context.CancellationToken);
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<UpsertExternalRoleAssignmentsCommand> context)
    {
        await UpsertExternalRoleAssignments([context], context.CancellationToken);
    }

    /// <summary>
    /// Upserts a set of parties.
    /// </summary>
    /// <param name="upserts">The parties to upsert.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    private async Task UpsertParties(
        IReadOnlyList<ConsumeContext<UpsertValidatedPartyCommand>> upserts,
        CancellationToken cancellationToken)
    {
        using var tracking = TrackingHelper.Create(BATCH_SIZE);
        {
            await using var uow = await _uow.CreateAsync(cancellationToken);
            var persistence = uow.GetPartyPersistence();

            foreach (var context in upserts)
            {
                var party = context.Message.Party;

                // Note: even though the party has already been validated, we still run the validation logic here
                // since it's cheap and gives much better error messages than the database layer. This is mostly
                // to catch if anyone produces UpsertValidatedPartyCommand instances somewhere in the future without
                // actually validating the party.
                PartyImportHelper.ValidatePartyForUpsert(party);

                var result = await persistence.UpsertParty(party, cancellationToken);
                result.EnsureSuccess();

                tracking.Update(context.Message.Tracking);
                await context.Publish(
                    new PartyUpdatedEvent
                    {
                        Party = result.Value.PartyUuid.Value.ToPartyReferenceContract(),
                    },
                    cancellationToken);
            }

            await uow.CommitAsync(cancellationToken);
        }

        await FlushTracking(tracking, cancellationToken);

        _meters.PartiesUpserted.Add(upserts.Count);
        _meters.PartyBatchesSucceeded.Add(1);
        _meters.PartyBatchSize.Record(upserts.Count);
    }

    private async Task UpsertPartyUsers(
        IReadOnlyList<ConsumeContext<UpsertPartyUserCommand>> upserts,
        CancellationToken cancellationToken)
    {
        using var tracking = TrackingHelper.Create(BATCH_SIZE);
        {
            await using var uow = await _uow.CreateAsync(cancellationToken);
            var persistence = uow.GetPartyPersistence();
            var statePersistence = uow.GetImportJobStatePersistence();

            foreach (var context in upserts)
            {
                var partyUuid = context.Message.PartyUuid;
                var user = context.Message.User;

                var result = await persistence.UpsertPartyUser(partyUuid, user, cancellationToken);
                result.EnsureSuccess();

                if (context.Message.Tracking.JobName is { Length: > 0 } jobName)
                {
                    await statePersistence.ClearPartyState(jobName, partyUuid, cancellationToken);
                }

                tracking.Update(context.Message.Tracking);
                await context.Publish(
                    new PartyUpdatedEvent
                    {
                        Party = partyUuid.ToPartyReferenceContract(),
                    },
                    cancellationToken);
            }

            await uow.CommitAsync(cancellationToken);
        }

        await FlushTracking(tracking, cancellationToken);

        _meters.PartiesUpserted.Add(upserts.Count);
        _meters.PartyBatchesSucceeded.Add(1);
        _meters.PartyBatchSize.Record(upserts.Count);
    }

    private async Task UpsertExternalRoleAssignments(
        IReadOnlyList<ConsumeContext<UpsertExternalRoleAssignmentsCommand>> upserts,
        CancellationToken cancellationToken)
    {
        using var tracking = TrackingHelper.Create(BATCH_SIZE);
        {
            await using var uow = await _uow.CreateAsync(cancellationToken);
            var persistence = uow.GetPartyExternalRolePersistence();

            foreach (var context in upserts)
            {
                var fromParty = context.Message.FromPartyUuid;
                var source = context.Message.Source;
                var assignments = context.Message.Assignments.Select(static ra => new IPartyExternalRolePersistence.UpsertExternalRoleAssignment
                {
                    RoleIdentifier = ra.Identifier,
                    ToParty = ra.ToPartyUuid,
                });

                tracking.Update(context.Message.Tracking);
                var publishTasks = new List<Task>(context.Message.Assignments.Count);
                var upsertEvts = persistence.UpsertExternalRolesFromPartyBySource(context.Message.CommandId, fromParty, source, assignments, cancellationToken);
                await foreach (var upsertEvt in upsertEvts.WithCancellation(cancellationToken))
                {
                    var publishTask = upsertEvt.Type switch
                    {
                        ExternalRoleAssignmentEvent.EventType.Added => context.Publish(
                            new ExternalRoleAssignmentAddedEvent
                            {
                                VersionId = upsertEvt.VersionId,
                                Role = upsertEvt.ToPartyExternalRoleReferenceContract(),
                                From = upsertEvt.FromParty.ToPartyReferenceContract(),
                                To = upsertEvt.ToParty.ToPartyReferenceContract(),
                            },
                            cancellationToken),

                        ExternalRoleAssignmentEvent.EventType.Removed => context.Publish(
                            new ExternalRoleAssignmentRemovedEvent
                            {
                                VersionId = upsertEvt.VersionId,
                                Role = upsertEvt.ToPartyExternalRoleReferenceContract(),
                                From = upsertEvt.FromParty.ToPartyReferenceContract(),
                                To = upsertEvt.ToParty.ToPartyReferenceContract(),
                            },
                            cancellationToken),

                        _ => ThrowHelper.ThrowInvalidOperationException<Task>($"The event type '{upsertEvt.Type}' is not supported."),
                    };

                    publishTasks.Add(publishTask);
                }

                await Task.WhenAll(publishTasks);
            }

            await uow.CommitAsync(cancellationToken);
        }

        foreach (var info in tracking)
        {
            await _tracker.TrackProcessedStatus(info.JobName, new ImportJobProcessingStatus { ProcessedMax = info.Progress }, cancellationToken);
        }

        foreach (var group in upserts.GroupBy(static c => c.Message.Source))
        {
            int count = 0;
            TagList tags = default;
            tags.Add("external-role.source", ToTagString(group.Key));
            
            foreach (var context in group)
            {
                count++;
                _meters.RoleAssignmentUpsertSize.Record(context.Message.Assignments.Count, in tags);
            }

            _meters.RoleAssignmentUpsertsSucceeded.Add(count, in tags);
        }

        _meters.RoleAssignmentBatchesSucceeded.Add(1);
        _meters.RoleAssignmentBatchSize.Record(upserts.Count);
    }

    private async Task FlushTracking(TrackingHelper tracking, CancellationToken cancellationToken)
    {
        if (!tracking.Any())
        {
            return;
        }

        using var activity = RegisterTelemetry.StartActivity($"{nameof(FlushTracking)}");
        foreach (var info in tracking)
        {
            using var subActivity = RegisterTelemetry.StartActivity(
                $"track {info.JobName}",
                tags: [
                    new("job.name", info.JobName),
                    new("job.progress", info.Progress),
                ]);

            Log.TrackingProgressUpdated(_logger, info.JobName, info.Progress);
            await _tracker.TrackProcessedStatus(info.JobName, new ImportJobProcessingStatus { ProcessedMax = info.Progress }, cancellationToken);
        }
    }

    private static string ToTagString(ExternalRoleSource source)
        => source switch
        {
            ExternalRoleSource.CentralCoordinatingRegister => "ccr",
            ExternalRoleSource.NationalPopulationRegister => "npr",
            ExternalRoleSource.EmployersEmployeeRegister => "aar",
            _ => ThrowHelper.ThrowArgumentOutOfRangeException<string>(nameof(source), $"Invalid {nameof(PartySource)}: {source}"),
        };

    private sealed class TrackingHelper
        : IEnumerable<UpsertPartyTracking>
        , IDisposable
    {
        public static TrackingHelper Create(int size)
            => new(ArrayPool<UpsertPartyTracking>.Shared, size);

        private readonly ArrayPool<UpsertPartyTracking> _pool;
        private UpsertPartyTracking[]? _tracking;

        private TrackingHelper(ArrayPool<UpsertPartyTracking> pool, int size)
        {
            _pool = pool;
            _tracking = pool.Rent(size);

            Debug.Assert(_tracking[0].JobName is null);
        }

        public bool Any()
        {
            if (_tracking is null)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(TrackingHelper));
            }

            return _tracking[0].JobName is not null;
        }

        public IEnumerator<UpsertPartyTracking> GetEnumerator()
        {
            if (_tracking is null)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(TrackingHelper));
            }

            return _tracking.TakeWhile(static t => t.JobName is not null).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public void Update(UpsertPartyTracking trackingInfo)
        {
            if (_tracking is null)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(TrackingHelper));
            }

            if (trackingInfo.JobName is null)
            {
                return;
            }

            for (var i = 0; i < _tracking.Length; i++)
            {
                ref var existing = ref _tracking[i];
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

            Debug.Fail("Tracking array is full.");
        }

        public void Dispose()
        {
            if (_tracking is not null)
            {
                _pool.Return(_tracking, clearArray: true);
                _tracking = null;
            }
        }
    }

    /// <summary>
    /// Consumer definition for <see cref="PartyImportBatchConsumer"/>.
    /// </summary>
    public sealed class Definition
        : ConsumerDefinition<PartyImportBatchConsumer>
    {
        private readonly bool _isTest;

        /// <summary>
        /// Initializes a new instance of the <see cref="Definition"/> class.
        /// </summary>
        public Definition(IHostEnvironment host)
        {
            _isTest = host.ApplicationName == "test";
        }

        /// <inheritdoc/>
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<PartyImportBatchConsumer> consumerConfigurator,
            IRegistrationContext context)
        {
            base.ConfigureConsumer(endpointConfigurator, consumerConfigurator, context);

            endpointConfigurator.PrefetchCount = BATCH_SIZE * 5;
            endpointConfigurator.ConcurrentMessageLimit = 3;
        }
    }

    /// <summary>
    /// Meters for <see cref="PartyImportBatchConsumer"/>.
    /// </summary>
    private sealed class ImportMeters(Meter meter)
        : IMetrics<ImportMeters>
    {
        /// <summary>
        /// Gets a counter for the number of parties upserted.
        /// </summary>
        public Counter<int> PartiesUpserted { get; }
            = meter.CreateCounter<int>("altinn.register.party-import.party.upsert.succeeded.total", description: "The number of parties upserted.");

        /// <summary>
        /// Gets a counter for the number of party-batches upserted.
        /// </summary>
        public Counter<int> PartyBatchesSucceeded { get; }
            = meter.CreateCounter<int>("altinn.register.party-import.party.batch.succeeded.total", description: "The number of party-batches upserted.");

        /// <summary>
        /// Gets a histogram for the size of party batches upserted.
        /// </summary>
        public Histogram<int> PartyBatchSize { get; }
            = meter.CreateHistogram<int>("altinn.register.party-import.party.batch.size", description: "The size of party batches upserted.");

        public Counter<int> RoleAssignmentUpsertsSucceeded { get; }
            = meter.CreateCounter<int>("altinn.register.party-import.role-assignment.upsert.succeeded.total", description: "The number of role assignment-upsert that has succeeded.");

        public Counter<int> RoleAssignmentBatchesSucceeded { get; }
            = meter.CreateCounter<int>("altinn.register.party-import.role-assignment.batch.succeeded.total", description: "The number of role assignment-batches that has succeeded.");

        public Histogram<int> RoleAssignmentBatchSize { get; }
            = meter.CreateHistogram<int>("altinn.register.party-import.role-assignment.batch.size", description: "The size of role assignment-batches upserted.");

        public Histogram<int> RoleAssignmentUpsertSize { get; }
            = meter.CreateHistogram<int>("altinn.register.party-import.role-assignment.count", description: "The number of role assignments in a single upsert.");

        /// <inheritdoc/>
        public static ImportMeters Create(Meter meter)
            => new ImportMeters(meter);
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Trace, "Updating progress tracking for job '{JobName}' with progress {Progress}.")]
        public static partial void TrackingProgressUpdated(ILogger logger, string jobName, ulong progress);
    }
}
