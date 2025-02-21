#nullable enable

using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core;
using Altinn.Register.Core.PartyImport.A2;
using MassTransit;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Consumer for importing parties from A2.
/// </summary>
public sealed class A2PartyImportConsumer
    : IConsumer<ImportA2PartyCommand>
    , IConsumer<ImportA2CCRRolesCommand>
{
    private readonly IA2PartyImportService _importService;
    private readonly ICommandSender _sender;
    private readonly ImportMeters _meters;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportConsumer"/> class.
    /// </summary>
    public A2PartyImportConsumer(
        IA2PartyImportService importService,
        ICommandSender commandSender,
        RegisterTelemetry telemetry)
    {
        _importService = importService;
        _sender = commandSender;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ImportA2PartyCommand> context)
    {
        var party = await _importService.GetParty(context.Message.PartyUuid, context.CancellationToken);
        await _sender.Send(new UpsertPartyCommand { Party = party, Tracking = new(JobNames.A2PartyImportParty, context.Message.ChangeId) }, context.CancellationToken);
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
            RoleAssignments = externalRoleAssignments,
            Tracking = new(JobNames.A2PartyImportCCRRoleAssignments, context.Message.ChangeId),
        };

        await _sender.Send(cmd, context.CancellationToken);
        _meters.RoleAssignmentsFetched.Add(externalRoleAssignments.Count);
        _meters.RoleAssignmentsPerParty.Record(externalRoleAssignments.Count);
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
        /// Gets a histogram for the number of role assignments fetched from A2 per party.
        /// </summary>
        public Histogram<int> RoleAssignmentsPerParty { get; }
            = telemetry.CreateHistogram<int>("register.party-import.a2.role-assignments-per-party", "The number of role assignments fetched from A2 per party.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
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
