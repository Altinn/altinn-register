﻿using Altinn.Authorization.ServiceDefaults.MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// A command for importing a party from A2.
/// </summary>
public sealed record ImportA2PartyCommand
    : Command
{
    /// <summary>
    /// Gets the party UUID.
    /// </summary>
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// Gets the change ID.
    /// </summary>
    public required int ChangeId { get; init; }

    /// <summary>
    /// Gets when the change was registered.
    /// </summary>
    public required DateTimeOffset ChangedTime { get; init; }
}