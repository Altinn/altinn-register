#nullable enable

using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Contracts;
using Altinn.Register.Core;
using Altinn.Register.Core.ExternalRoles;
using MassTransit;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Consumer for resolving A2 external role assignments.
/// </summary>
public sealed partial class A2ExternalRoleResolverConsumer
    : IConsumer<ResolveAndUpsertA2CCRRoleAssignmentsCommand>
{
    private readonly ILogger<A2ExternalRoleResolverConsumer> _logger;
    private readonly IExternalRoleDefinitionPersistence _persistence;
    private readonly ICommandSender _sender;
    private readonly ImportMeters _meters;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2ExternalRoleResolverConsumer"/> class.
    /// </summary>
    public A2ExternalRoleResolverConsumer(
        ILogger<A2ExternalRoleResolverConsumer> logger,
        IExternalRoleDefinitionPersistence externalRoleDefinitionPersistence,
        ICommandSender sender,
        RegisterTelemetry telemetry)
    {
        _logger = logger;
        _persistence = externalRoleDefinitionPersistence;
        _sender = sender;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<ResolveAndUpsertA2CCRRoleAssignmentsCommand> context)
    {
        if (context.Message.RoleAssignments.Count == 0)
        {
            await _sender.Send(new UpsertExternalRoleAssignmentsCommand
            {
                FromPartyUuid = context.Message.FromPartyUuid,
                FromPartyId = context.Message.FromPartyId,
                Source = ExternalRoleSource.CentralCoordinatingRegister,
                Assignments = [],
                Tracking = context.Message.Tracking,
            });

            return;
        }

        var assignments = new List<UpsertExternalRoleAssignmentsCommand.Assignment>(context.Message.RoleAssignments.Count);
        foreach (var assignment in context.Message.RoleAssignments)
        {
            var roleDefinition = await _persistence.TryGetRoleDefinitionByRoleCode(assignment.RoleCode, context.CancellationToken);
            if (roleDefinition is null)
            {
                Log.RoleWithRoleCodeNotFound(_logger, assignment.RoleCode, context.Message.FromPartyUuid, assignment.ToPartyUuid);
                _meters.RoleDefinitionsNotFound.Add(1);
                continue;
            }

            if (roleDefinition.Source != ExternalRoleSource.CentralCoordinatingRegister)
            {
                Log.RoleWithWrongSource(_logger, assignment.RoleCode, context.Message.FromPartyUuid, assignment.ToPartyUuid, roleDefinition.Source, roleDefinition.Identifier);
                _meters.RoleDefinitionsWrongSource.Add(1);
                continue;
            }

            _meters.RoleDefinitionsResolved.Add(1);
            assignments.Add(new()
            {
                Identifier = roleDefinition.Identifier,
                ToPartyUuid = assignment.ToPartyUuid,
            });
        }

        await _sender.Send(
            new UpsertExternalRoleAssignmentsCommand
            {
                FromPartyUuid = context.Message.FromPartyUuid,
                FromPartyId = context.Message.FromPartyId,
                Source = ExternalRoleSource.CentralCoordinatingRegister,
                Assignments = assignments,
                Tracking = context.Message.Tracking,
            },
            context.CancellationToken);
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Warning, "External role-definition with role code '{RoleCode}' not found. Attempted to add from party '{FromPartyUuid}' to party '{ToPartyUuid}'.", EventName = "RoleWithRoleCodeNotFound")]
        public static partial void RoleWithRoleCodeNotFound(ILogger logger, string roleCode, Guid fromPartyUuid, Guid toPartyUuid);

        [LoggerMessage(1, LogLevel.Warning, "External role-definition with role code '{RoleCode}' found, but with the wrong source '{Source}' and identifier '{Identifier}'. Attempted to add from party '{FromPartyUuid}' to party '{ToPartyUuid}'.", EventName = "RoleWithWrongSource")]
        public static partial void RoleWithWrongSource(ILogger logger, string roleCode, Guid fromPartyUuid, Guid toPartyUuid, ExternalRoleSource source, string identifier);
    }

    /// <summary>
    /// Consumer definition for <see cref="A2ExternalRoleResolverConsumer"/>.
    /// </summary>
    public sealed class Definition
        : ConsumerDefinition<A2ExternalRoleResolverConsumer>
    {
        /// <inheritdoc/>
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<A2ExternalRoleResolverConsumer> consumerConfigurator,
            IRegistrationContext context)
        {
            base.ConfigureConsumer(endpointConfigurator, consumerConfigurator, context);
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
        public Counter<int> RoleDefinitionsNotFound { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.resolve-role.errors", description: "The number of times a role-code was not found.");

        public Counter<int> RoleDefinitionsResolved { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.resolve-role.found", description: "The number of times a role-code was resolved.");

        public Counter<int> RoleDefinitionsWrongSource { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.resolve-role.wrong-source", description: "The number of times a role-code was found, but with the wrong source.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
    }
}
