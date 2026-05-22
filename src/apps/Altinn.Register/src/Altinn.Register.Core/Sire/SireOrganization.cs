using Altinn.Register.Contracts;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Core.Sire;

/// <summary>
/// Represents an organization in SIRE. This is a subset of the properties returned by SIRE, and only contains the properties that are relevant for our use cases.
/// </summary>
public sealed record SireOrganization
{
    /// <summary>
    /// Gets the 9-digit organization identifier.
    /// </summary>
    public required OrganizationIdentifier OrganizationIdentifier { get; init; }

    /// <summary>
    /// Gets the organization name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the organization form (e.g. "AS", "ANS"). Stored as free text.
    /// </summary>
    public required string UnitType { get; init; }

    /// <summary>
    /// Gets the unit status. Set to "slettet" when the organization has been deleted.
    /// </summary>
    public required string? UnitStatus { get; init; }

    /// <summary>
    /// Gets whether the organization is deleted.
    /// </summary>
    public required bool IsDeleted { get; init; }

    /// <summary>
    /// Gets the mailing address, or null if no address is available.
    /// </summary>
    public required MailingAddressRecord? MailingAddress { get; init; }

    /// <summary>
    /// Gets the last updated timestamp from the postal address.
    /// </summary>
    public required DateTimeOffset? LastUpdated { get; init; }

    /// <summary>
    /// Gets the business relationships (virksomhetsrelasjon).
    /// </summary>
    public required IReadOnlyList<SireBusinessRelationship> BusinessRelationships { get; init; }
}
