#nullable enable

using System.Diagnostics;
using Altinn.Register.Contracts;
using Altinn.Register.Core;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Saga for importing parties from A2.
/// </summary>
public sealed partial class A2PartyImportSaga
    : ISaga<A2PartyImportSaga, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaStartedBy<A2PartyImportSaga, ImportA2PartyCommand, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaStartedBy<A2PartyImportSaga, ImportA2UserProfileCommand, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaHandles<A2PartyImportSaga, EnrichA2PartyImportSagaCommand, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaHandles<A2PartyImportSaga, CompleteA2PartyImportSagaCommand, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaHandles<A2PartyImportSaga, RetryA2PartyImportSagaCommand, A2PartyImportSaga.A2PartyImportSagaData>
{
    /// <inheritdoc/>
    public static string Name => nameof(A2PartyImportSaga);

    private readonly SagaContext<A2PartyImportSagaData> _context;
    private readonly IA2PartyImportService _importService;
    private readonly TimeProvider _timeProvider;
    private readonly IPartyPersistence _parties;
    private readonly IPartyExternalRolePersistence _roles;
    private readonly IImportJobTracker _tracker;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;
    private readonly ILogger<A2PartyImportSaga> _logger;

    private A2PartyImportSagaData State => _context.State.Data!;

    private Guid SagaId => _context.SagaId;

    private void MarkComplete(bool error = false)
    {
        _context.State.Status = error ? SagaStatus.Faulted : SagaStatus.Completed;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportSaga"/> class.
    /// </summary>
    public A2PartyImportSaga(
        SagaContext<A2PartyImportSagaData> context,
        IA2PartyImportService importService,
        TimeProvider timeProvider,
        IPartyPersistence parties,
        IPartyExternalRolePersistence roles,
        IImportJobTracker tracker,
        IConfiguration configuration,
        IServiceProvider services,
        ILogger<A2PartyImportSaga> logger)
    {
        _context = context;
        _importService = importService;
        _timeProvider = timeProvider;
        _parties = parties;
        _roles = roles;
        _tracker = tracker;
        _configuration = configuration;
        _services = services;
        _logger = logger;
    }

    private async Task Enrich(CancellationToken cancellationToken)
    {
        Debug.Assert(State.Party is not null);
        Debug.Assert(State.Enrichers.Count == 0);

        var context = new A2PartyImportSagaEnrichmentCheckContext { Party = State.Party, PartyUuid = State.PartyUuid };
        State.Enrichers = new(A2PartyImportSagaEnricher.For(_configuration, context));

        await ContinueEnrichment(cancellationToken);
    }

    private async Task ContinueEnrichment(CancellationToken cancellationToken)
    {
        if (State.Enrichers.Count > 0)
        {
            await _context.Send(
                  new EnrichA2PartyImportSagaCommand
                  {
                      CorrelationId = SagaId,
                  },
                  cancellationToken);

            return;
        }

        await _context.Send(
            new CompleteA2PartyImportSagaCommand
            {
                CorrelationId = SagaId,
            },
            cancellationToken);
    }

    private async Task<FlowControl> FetchParty(CancellationToken cancellationToken)
    {
        using var activity = RegisterTelemetry.StartActivity("fetch party altinn 2", ActivityKind.Internal, tags: [new("party.uuid", State.PartyUuid)]);

        var partyResult = await _importService.GetParty(State.PartyUuid, cancellationToken);
        if (partyResult is { Problem.ErrorCode: var errorCode }
            && errorCode == Problems.PartyGone.ErrorCode)
        {
            // Party is gone, so we can skip it. These should be rare, so don't bother with tracking.
            Log.PartyGone(_logger, State.PartyUuid);
            MarkComplete();
            return FlowControl.Break;
        }

        partyResult.EnsureSuccess();
        State.Party = partyResult.Value;
        return FlowControl.Continue;
    }

    /// <summary>
    /// State data for <see cref="A2PartyImportSaga"/>.
    /// </summary>
    public sealed class A2PartyImportSagaData
        : ISagaStateData<A2PartyImportSagaData>
    {
        /// <inheritdoc/>
        public static string StateType => nameof(A2PartyImportSagaData);

        /// <summary>
        /// Gets the unique identifier for the party.
        /// </summary>
        public required Guid PartyUuid { get; init; }

        /// <summary>
        /// Gets the unique identifier of the user associated with this instance.
        /// </summary>
        public required ulong? UserId { get; init; }

        /// <summary>
        /// Gets tracking information for the import job.
        /// </summary>
        public required UpsertPartyTracking Tracking { get; init; }

        /// <summary>
        /// Gets or sets the party being upserted.
        /// </summary>
        public PartyRecord? Party { get; set; }

        /// <summary>
        /// Gets or sets the collection of role assignments grouped by external role source.
        /// </summary>
        public Dictionary<ExternalRoleSource, IReadOnlyList<UpsertExternalRoleAssignmentsCommand.Assignment>> RoleAssignments { get; set; } = new();

        /// <summary>
        /// Gets or sets the remaining enrichers.
        /// </summary>
        public Queue<string> Enrichers { get; set; } = new();

        /// <summary>
        /// Clears non-initial state data.
        /// </summary>
        internal void Clear()
        {
            Party = null;
            RoleAssignments.Clear();
            Enrichers.Clear();
        }
    }

    private enum FlowControl
    {
        Continue,
        Break,
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Party with UUID {PartyUuid} is gone.")]
        public static partial void PartyGone(ILogger logger, Guid partyUuid);

        [LoggerMessage(1, LogLevel.Information, "User with ID {UserId} is gone.")]
        public static partial void ProfileGone(ILogger logger, ulong userId);
    }
}
