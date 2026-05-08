namespace Altinn.Register.Core.Parties;

/// <summary>
/// Feature flags for persistence operations.
/// </summary>
public enum PersistenceFeatureFlag
    : uint
{
    /// <summary>
    /// Indicates that creating new party IDs is allowed.
    /// </summary>
    CreatePartyId = 1,
}
