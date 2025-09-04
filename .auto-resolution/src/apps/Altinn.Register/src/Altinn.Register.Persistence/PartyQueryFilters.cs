namespace Altinn.Register.Persistence;

/// <summary>
/// What to filter by when looking up parties.
/// </summary>
[Flags]
public enum PartyQueryFilters
    : byte
{
    /// <summary>
    /// Do not filter by anything.
    /// </summary>
    None = 0,

    /// <summary>
    /// Filter by party id.
    /// </summary>
    PartyId = 1 << 0,

    /// <summary>
    /// Filter by party UUID.
    /// </summary>
    PartyUuid = 1 << 1,

    /// <summary>
    /// Filter by person identifier.
    /// </summary>
    PersonIdentifier = 1 << 2,

    /// <summary>
    /// Filter by organization identifier.
    /// </summary>
    OrganizationIdentifier = 1 << 3,

    /// <summary>
    /// Filter by user id.
    /// </summary>
    UserId = 1 << 4,

    /// <summary>
    /// Filter by stream page.
    /// </summary>
    StreamPage = 1 << 5,

    /// <summary>
    /// Get multiple parties at once.
    /// </summary>
    Multiple = 1 << 7,
}
