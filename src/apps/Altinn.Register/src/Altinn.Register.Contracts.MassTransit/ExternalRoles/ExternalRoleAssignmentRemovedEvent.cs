﻿using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Contracts.Parties;
using MassTransit;

namespace Altinn.Register.Contracts.ExternalRoles;

/// <summary>
/// An event that is published when an external role assignment is removed.
/// </summary>
[MessageUrn("event:altinn-register:external-role-assignment-removed")]
public sealed record ExternalRoleAssignmentRemovedEvent
    : EventBase
{
    /// <summary>
    /// Gets the role that was assigned.
    /// </summary>
    public required ExternalRoleReference Role { get; init; }

    /// <summary>
    /// Gets the party that the role was assigned from.
    /// </summary>
    public required PartyReference From { get; init; }

    /// <summary>
    /// Gets the party that the role was assigned to.
    /// </summary>
    public required PartyReference To { get; init; }
}
