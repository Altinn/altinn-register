using Altinn.Register.Contracts;

namespace Altinn.Register.Integrations.Npr;

/// <summary>
/// Represents information about a guardianship, including the guardian's identifier and their roles in relation to the ward.
/// </summary>
public record GuardianshipInfo
{
    /// <summary>
    /// Gets the <see cref="PersonIdentifier"/> of the guardian.
    /// </summary>
    public required PersonIdentifier Guardian { get; init; }

    /// <summary>
    /// Gets the set of roles the guardian has in relation to the ward (e.g., "bank-ta-opp-lan-kreditter").
    /// </summary>
    public required ImmutableValueSet<string> Roles { get; init; }
}
