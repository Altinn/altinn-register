using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Core.Parties.Records;

/// <summary>
/// A database record for a party.
/// </summary>
[PolymorphicFieldValueRecord(IsRoot = true)]
[PolymorphicDerivedType(typeof(PersonRecord), Parties.PartyType.Person)]
[PolymorphicDerivedType(typeof(OrganizationRecord), Parties.PartyType.Organization)]
[PolymorphicDerivedType(typeof(SelfIdentifiedUserRecord), Parties.PartyType.SelfIdentifiedUser)]
public record PartyRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PartyRecord"/> class.
    /// </summary>
    public PartyRecord(FieldValue<PartyType> partyType)
    {
        PartyType = partyType;
    }

    /// <summary>
    /// Gets the UUID of the party.
    /// </summary>
    public required FieldValue<Guid> PartyUuid { get; init; }

    /// <summary>
    /// Gets the ID of the party.
    /// </summary>
    public required FieldValue<uint> PartyId { get; init; }

    /// <summary>
    /// Gets the type of the party.
    /// </summary>
    [PolymorphicDiscriminatorProperty]
    public FieldValue<PartyType> PartyType { get; private init; }

    /// <summary>
    /// Gets the display-name of the party.
    /// </summary>
    public required FieldValue<string> DisplayName { get; init; }

    /// <summary>
    /// Gets the person identifier of the party, or <see langword="null"/> if the party is not a person.
    /// </summary>
    public required FieldValue<PersonIdentifier> PersonIdentifier { get; init; }

    /// <summary>
    /// Gets the organization identifier of the party, or <see langword="null"/> if the party is not an organization.
    /// </summary>
    public required FieldValue<OrganizationIdentifier> OrganizationIdentifier { get; init; }

    /// <summary>
    /// Gets when the party was created in Altinn 3.
    /// </summary>
    public required FieldValue<DateTimeOffset> CreatedAt { get; init; }

    /// <summary>
    /// Gets when the party was last modified in Altinn 3.
    /// </summary>
    public required FieldValue<DateTimeOffset> ModifiedAt { get; init; }

    /// <summary>
    /// Gets user information for the party.
    /// </summary>
    public required FieldValue<PartyUserRecord> User { get; init; }

    /// <summary>
    /// Gets whether the party is deleted.
    /// </summary>
    public required FieldValue<bool> IsDeleted { get; init; }

    /// <summary>
    /// Gets the version ID of the party.
    /// </summary>
    public required FieldValue<ulong> VersionId { get; init; }
}
