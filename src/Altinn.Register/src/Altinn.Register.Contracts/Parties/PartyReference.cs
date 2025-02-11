namespace Altinn.Register.Contracts.Parties;

/// <summary>
/// Represents a reference to a party.
/// </summary>
public sealed record PartyReference
{
    /// <summary>
    /// Gets the UUID of the party.
    /// </summary>
    public Guid PartyUuid { get; init; }
}
