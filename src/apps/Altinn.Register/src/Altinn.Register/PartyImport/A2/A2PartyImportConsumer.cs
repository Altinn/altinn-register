#nullable enable

using System.Diagnostics.Metrics;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using MassTransit;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Consumer for importing parties from A2.
/// </summary>
public sealed partial class A2PartyImportConsumer
    : IConsumer<ImportA2PartyCommand>
    , IConsumer<ImportA2CCRRolesCommand>
    , IConsumer<ImportA2UserIdForPartyCommand>
{
    private readonly IA2PartyImportService _importService;
    private readonly ICommandSender _sender;
    private readonly ImportMeters _meters;
    private readonly ILogger<A2PartyImportConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportConsumer"/> class.
    /// </summary>
    public A2PartyImportConsumer(
        ILogger<A2PartyImportConsumer> logger,
        IA2PartyImportService importService,
        ICommandSender commandSender,
        RegisterTelemetry telemetry)
    {
        _logger = logger;
        _importService = importService;
        _sender = commandSender;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ImportA2PartyCommand> context)
    {
        var partyResult = await _importService.GetParty(context.Message.PartyUuid, context.CancellationToken);
        if (partyResult is { Problem.ErrorCode: var errorCode }
            && errorCode == Problems.PartyGone.ErrorCode)
        {
            // Party is gone, so we can skip it. These should be rare, so don't bother with tracking.
            Log.PartyGone(_logger, context.Message.PartyUuid);
            return;
        }

        partyResult.EnsureSuccess();
        await _sender.Send(new UpsertPartyCommand { Party = partyResult.Value, Tracking = new(JobNames.A2PartyImportParty, context.Message.ChangeId) }, context.CancellationToken);
        _meters.PartiesFetched.Add(1);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ImportA2CCRRolesCommand> context)
    {
        var partyId = context.Message.PartyId;
        var partyUuid = context.Message.PartyUuid;

        var externalRoleAssignments = await _importService.GetExternalRoleAssignmentsFrom(partyId, partyUuid, context.CancellationToken)
            .ToListAsync(context.CancellationToken);

        var cmd = new ResolveAndUpsertA2CCRRoleAssignmentsCommand
        {
            FromPartyUuid = partyUuid,
            FromPartyId = partyId,
            RoleAssignments = externalRoleAssignments,
            Tracking = new(JobNames.A2PartyImportCCRRoleAssignments, context.Message.ChangeId),
        };

        await _sender.Send(cmd, context.CancellationToken);
        _meters.RoleAssignmentsFetched.Add(externalRoleAssignments.Count);
        _meters.RoleAssignmentsPerParty.Record(externalRoleAssignments.Count);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ImportA2UserIdForPartyCommand> context)
    {
        var partyUuid = context.Message.PartyUuid;
        var partyType = context.Message.PartyType;

        Result<PartyUserRecord> userRecordResult;
        if (partyType is PartyRecordType.Person)
        {
            userRecordResult = await _importService.GetOrCreatePersonUser(partyUuid, context.CancellationToken);
        }
        else
        {
            userRecordResult = await _importService.GetPartyUser(partyUuid, context.CancellationToken);
        }

        userRecordResult.EnsureSuccess();
        var cmd = new UpsertPartyUserCommand
        {
            PartyUuid = partyUuid,
            User = userRecordResult.Value,
            Tracking = context.Message.Tracking,
        };

        await _sender.Send(cmd, context.CancellationToken);
        _meters.UserIdsFetched.Add(1, [new("party.type", partyType)]);
    }

    /// <summary>
    /// Meters for <see cref="A2PartyImportConsumer"/>.
    /// </summary>
    private sealed class ImportMeters(RegisterTelemetry telemetry)
        : IServiceMeters<ImportMeters>
    {
        /// <summary>
        /// Gets a counter for the number of parties imported from A2.
        /// </summary>
        public Counter<int> PartiesFetched { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.parties.fetched", "The number of parties fetched from A2.");

        /// <summary>
        /// Gets a counter for the number of role assignments fetched from A2.
        /// </summary>
        public Counter<int> RoleAssignmentsFetched { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.role-assignments.fetched", "The number of role assignments fetched from A2.");

        /// <summary>
        /// Gets a counter for the number of user ids fetched from A2.
        /// </summary>
        public Counter<int> UserIdsFetched { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.user-ids.fetched", "The number of user ids fetched from A2.");

        /// <summary>
        /// Gets a histogram for the number of role assignments fetched from A2 per party.
        /// </summary>
        public Histogram<int> RoleAssignmentsPerParty { get; }
            = telemetry.CreateHistogram<int>("register.party-import.a2.role-assignments-per-party", "The number of role assignments fetched from A2 per party.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Party with UUID {PartyUuid} is gone.")]
        public static partial void PartyGone(ILogger logger, Guid partyUuid);
    }

    /// <summary>
    /// Consumer definition for <see cref="PartyImportBatchConsumer"/>.
    /// </summary>
    public sealed class Definition
        : ConsumerDefinition<A2PartyImportConsumer>
    {
        /// <inheritdoc/>
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<A2PartyImportConsumer> consumerConfigurator,
            IRegistrationContext context)
        {
            base.ConfigureConsumer(endpointConfigurator, consumerConfigurator, context);

            consumerConfigurator.UseConcurrentMessageLimit(10, endpointConfigurator);
        }
    }
}
