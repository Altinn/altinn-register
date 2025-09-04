using System.Text.Json.Serialization;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Fields to include when fetching a <see cref="PartyRecord"/>.
/// </summary>
[Flags]
[StringEnumConverter]
public enum PartyFieldIncludes
    : uint
{
    /// <summary>
    /// No extra information (default).
    /// </summary>
    [JsonStringEnumMemberName("none")]
    None = 0,

    /// <summary>
    /// The party UUID.
    /// </summary>
    [JsonStringEnumMemberName("uuid")]
    PartyUuid = 1 << 0,

    /// <summary>
    /// The party ID.
    /// </summary>
    [JsonStringEnumMemberName("id")]
    PartyId = 1 << 1,

    /// <summary>
    /// The party type.
    /// </summary>
    [JsonStringEnumMemberName("type")]
    PartyType = 1 << 2,

    /// <summary>
    /// The party display-name.
    /// </summary>
    [JsonStringEnumMemberName("display-name")]
    PartyDisplayName = 1 << 3,

    /// <summary>
    /// The person identifier of the party, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person-id")]
    PartyPersonIdentifier = 1 << 4,

    /// <summary>
    /// The organization identifier of the party, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org-id")]
    PartyOrganizationIdentifier = 1 << 5,

    /// <summary>
    /// The time when the party was created.
    /// </summary>
    [JsonStringEnumMemberName("created")]
    PartyCreatedAt = 1 << 6,

    /// <summary>
    /// The time when the party was last modified.
    /// </summary>
    [JsonStringEnumMemberName("modified")]
    PartyModifiedAt = 1 << 7,

    /// <summary>
    /// Whether the party is deleted.
    /// </summary>
    [JsonStringEnumMemberName("deleted")]
    PartyIsDeleted = 1 << 8,

    /// <summary>
    /// The version ID of the party.
    /// </summary>
    [JsonStringEnumMemberName("version")]
    PartyVersionId = 1 << 9,

    /// <summary>
    /// The UUID of the owner party, if any.
    /// </summary>
    PartyOwnerUuid = 1 << 10,

    /// <summary>
    /// All party identifiers.
    /// </summary>
    [JsonStringEnumMemberName("identifiers")]
    Identifiers = PartyUuid | PartyId | PartyPersonIdentifier | PartyOrganizationIdentifier,

    /// <summary>
    /// All party fields.
    /// </summary>
    [JsonStringEnumMemberName("party")]
    Party = Identifiers | PartyType | PartyDisplayName | PartyCreatedAt | PartyModifiedAt | PartyIsDeleted | PartyVersionId | PartyOwnerUuid,

    /// <summary>
    /// The first name of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.first-name")]
    PersonFirstName = 1 << 11,

    /// <summary>
    /// The middle name of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.middle-name")]
    PersonMiddleName = 1 << 12,

    /// <summary>
    /// The last name of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.last-name")]
    PersonLastName = 1 << 13,

    /// <summary>
    /// The short name of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.short-name")]
    PersonShortName = 1 << 14,

    /// <summary>
    /// All person name fields.
    /// </summary>
    [JsonStringEnumMemberName("person.name")]
    PersonName = PersonFirstName | PersonMiddleName | PersonLastName | PersonShortName,

    /// <summary>
    /// The address of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.address")]
    PersonAddress = 1 << 15,

    /// <summary>
    /// The mailing address of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.mailing-address")]
    PersonMailingAddress = 1 << 16,

    /// <summary>
    /// The date of birth of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.date-of-birth")]
    PersonDateOfBirth = 1 << 17,

    /// <summary>
    /// The date of death of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.date-of-death")]
    PersonDateOfDeath = 1 << 18,

    /// <summary>
    /// All person fields.
    /// </summary>
    [JsonStringEnumMemberName("person")]
    Person = PersonFirstName | PersonMiddleName | PersonLastName | PersonShortName | PersonAddress | PersonMailingAddress | PersonDateOfBirth | PersonDateOfDeath,

    /// <summary>
    /// The organization unit status, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.status")]
    OrganizationUnitStatus = 1 << 19,

    /// <summary>
    /// The organization unit type, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.type")]
    OrganizationUnitType = 1 << 20,

    /// <summary>
    /// The organization telephone number, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.telephone")]
    OrganizationTelephoneNumber = 1 << 21,

    /// <summary>
    /// The organization mobile number, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.mobile")]
    OrganizationMobileNumber = 1 << 22,

    /// <summary>
    /// The organization fax number, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.fax")]
    OrganizationFaxNumber = 1 << 23,

    /// <summary>
    /// The organization email address, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.email")]
    OrganizationEmailAddress = 1 << 24,

    /// <summary>
    /// The organization internet address, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.internet")]
    OrganizationInternetAddress = 1 << 25,

    /// <summary>
    /// The organization mailing address, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.mailing-address")]
    OrganizationMailingAddress = 1 << 26,

    /// <summary>
    /// The organization business address, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.business-address")]
    OrganizationBusinessAddress = 1 << 27,

    /// <summary>
    /// All organization fields.
    /// </summary>
    [JsonStringEnumMemberName("org")]
    Organization = OrganizationUnitStatus | OrganizationUnitType | OrganizationTelephoneNumber | OrganizationMobileNumber | OrganizationFaxNumber
        | OrganizationEmailAddress | OrganizationInternetAddress | OrganizationMailingAddress | OrganizationBusinessAddress,

    /// <summary>
    /// Include subunits (if party is an organization).
    /// </summary>
    [JsonStringEnumMemberName("org.subunits")]
    SubUnits = 1 << 28,

    /// <summary>
    /// The user id(s), if the party has an associated user.
    /// </summary>
    [JsonStringEnumMemberName("user.id")]
    UserId = 1 << 29,

    /// <summary>
    /// The username, if the party has an associated user.
    /// </summary>
    [JsonStringEnumMemberName("user.name")]
    Username = 1 << 30,

    /// <summary>
    /// All user fields.
    /// </summary>
    [JsonStringEnumMemberName("user")]
    User = UserId | Username,
}
