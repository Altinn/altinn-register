#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core.Parties.Records;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.PartyImport;

/// <summary>
/// A command for importing a party from A2.
/// </summary>
public sealed record BatchedUpsertPartyCommand
    : CommandBase
{
    /// <summary>
    /// Creates a new instance of <see cref="BatchedUpsertPartyCommand"/> from a <see cref="UpsertPartyCommand"/>.
    /// </summary>
    /// <param name="source">The <see cref="UpsertPartyCommand"/> to create a <see cref="BatchedUpsertPartyCommand"/> from.</param>
    /// <returns>The new <see cref="BatchedUpsertPartyCommand"/>.</returns>
    public static BatchedUpsertPartyCommand From(UpsertPartyCommand source)
    {
        Guard.IsNotNull(source);

        return new()
        {
            Party = source.Party,
            Tracking = source.Tracking,
        };
    }

    /// <summary>
    /// Gets the party to import.
    /// </summary>
    public required PartyRecord Party { get; init; }

    /// <summary>
    /// Gets the tracking information for the import.
    /// </summary>
    public UpsertPartyTracking Tracking { get; init; }
}
