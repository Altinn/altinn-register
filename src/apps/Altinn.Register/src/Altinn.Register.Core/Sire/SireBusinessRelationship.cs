using Altinn.Register.Contracts;

namespace Altinn.Register.Core.Sire;

/// <summary>
/// Represents a business relationship (virksomhetsrelasjon) as returned by the SIRE API.
/// </summary>
public sealed record SireBusinessRelationship
{
    /// <summary>
    /// Gets the relationship type (e.g. "styretsLeder").
    /// </summary>
    public required string RelationshipType { get; init; }

    /// <summary>
    /// Gets the related person identifier, if the related party is a person.
    /// </summary>
    public required PersonIdentifier? RelatedPersonIdentifier { get; init; }

    /// <summary>
    /// Gets the related organization identifier, if the related party is an organization.
    /// </summary>
    public required OrganizationIdentifier? RelatedOrganizationIdentifier { get; init; }

    /// <summary>
    /// Gets the validity start timestamp.
    /// </summary>
    public required DateTimeOffset? ValidFrom { get; init; }

    /// <summary>
    /// Gets the validity end timestamp.
    /// </summary>
    public required DateTimeOffset? ValidTo { get; init; }
}
