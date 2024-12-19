#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.PartyImport;

/// <summary>
/// A command for importing a party from A2.
/// </summary>
public sealed record UpsertPartyCommand
    : CommandBase
{
    /// <summary>
    /// The updated party record.
    /// </summary>
    public required PartyRecord Party { get; init; }
}
