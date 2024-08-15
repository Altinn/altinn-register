using System.Collections.Immutable;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// A party entity.
/// </summary>
public record Party
{
    /// <summary>
    /// Gets the UUID of the party.
    /// </summary>
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// Gets the ID of the party.
    /// </summary>
    public required int PartyId { get; init; }

    /// <summary>
    /// Gets the type of the party.
    /// </summary>
    public required PartyType PartyType { get; init; }

    /// <summary>
    /// Gets the (display) name of the party.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the person identifier of the party, or <see langword="null"/> if the party is not a person.
    /// </summary>
    public required PersonIdentifier? PersonIdentifier { get; init; }

    /// <summary>
    /// Gets the organization identifier of the party, or <see langword="null"/> if the party is not an organization.
    /// </summary>
    public required OrganizationIdentifier? OrganizationIdentifier { get; init; }

    /// <summary>
    /// Gets when the party was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets when the party was last modified.
    /// </summary>
    public required DateTimeOffset ModifiedAt { get; init; }

    /// <summary>
    /// Gets (an optional) list of source references for the party.
    /// </summary>
    public ImmutableArray<PartySourceRef> SourceRefs { get; init; }

    /// <inheritdoc/>
    public virtual bool Equals(Party? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        if (EqualityContract != other.EqualityContract
            || PartyUuid != other.PartyUuid
            || PartyId != other.PartyId
            || PartyType != other.PartyType
            || !string.Equals(Name, other.Name, StringComparison.Ordinal)
            || PersonIdentifier != other.PersonIdentifier
            || OrganizationIdentifier != other.OrganizationIdentifier
            || CreatedAt != other.CreatedAt
            || ModifiedAt != other.ModifiedAt
            || SourceRefs.IsDefault != other.SourceRefs.IsDefault)
        {
            return false;
        }

        if (SourceRefs.IsDefault)
        {
            return true;
        }

        return SourceRefs.AsSpan().SequenceEqual(other.SourceRefs.AsSpan());
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(EqualityContract);
        hash.Add(PartyUuid);
        hash.Add(PartyId);
        hash.Add(PartyType);
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(PersonIdentifier);
        hash.Add(OrganizationIdentifier);
        hash.Add(CreatedAt);
        hash.Add(ModifiedAt);

        if (!SourceRefs.IsDefault)
        {
            hash.Add(SourceRefs.Length);
            foreach (var item in SourceRefs)
            {
                hash.Add(item);
            }
        }

        return hash.ToHashCode();
    }
}
