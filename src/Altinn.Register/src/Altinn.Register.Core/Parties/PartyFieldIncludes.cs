using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Fields to include when fetching a <see cref="PartyRecord"/>.
/// </summary>
[Flags]
public enum PartyFieldIncludes
    : uint
{
    /// <summary>
    /// No extra information (default).
    /// </summary>
    None = 0,

    /// <summary>
    /// The party UUID.
    /// </summary>
    PartyUuid = 1 << 0,

    /// <summary>
    /// The party ID.
    /// </summary>
    PartyId = 1 << 1,

    /// <summary>
    /// The party type.
    /// </summary>
    PartyType = 1 << 2,

    /// <summary>
    /// The party name.
    /// </summary>
    PartyName = 1 << 3,

    /// <summary>
    /// The person identifier of the party, if the party is a person.
    /// </summary>
    PartyPersonIdentifier = 1 << 4,

    /// <summary>
    /// The organization identifier of the party, if the party is an organization.
    /// </summary>
    PartyOrganizationIdentifier = 1 << 5,

    /// <summary>
    /// The time when the party was created.
    /// </summary>
    PartyCreatedAt = 1 << 6,

    /// <summary>
    /// The time when the party was last modified.
    /// </summary>
    PartyModifiedAt = 1 << 7,

    /// <summary>
    /// All party fields.
    /// </summary>
    Party = PartyUuid | PartyId | PartyType | PartyName | PartyPersonIdentifier | PartyOrganizationIdentifier | PartyCreatedAt | PartyModifiedAt,

    /// <summary>
    /// The first name of the person, if the party is a person.
    /// </summary>
    PersonFirstName = 1 << 8,

    /// <summary>
    /// The middle name of the person, if the party is a person.
    /// </summary>
    PersonMiddleName = 1 << 9,

    /// <summary>
    /// The last name of the person, if the party is a person.
    /// </summary>
    PersonLastName = 1 << 10,

    /// <summary>
    /// The address of the person, if the party is a person.
    /// </summary>
    PersonAddress = 1 << 11,

    /// <summary>
    /// The mailing address of the person, if the party is a person.
    /// </summary>
    PersonMailingAddress = 1 << 12,

    /// <summary>
    /// The date of birth of the person, if the party is a person.
    /// </summary>
    PersonDateOfBirth = 1 << 13,

    /// <summary>
    /// The date of death of the person, if the party is a person.
    /// </summary>
    PersonDateOfDeath = 1 << 14,

    /// <summary>
    /// All person fields.
    /// </summary>
    Person = PersonFirstName | PersonMiddleName | PersonLastName | PersonAddress | PersonMailingAddress | PersonDateOfBirth | PersonDateOfDeath,

    /// <summary>
    /// The organization unit status, if the party is an organization.
    /// </summary>
    OrganizationUnitStatus = 1 << 15,

    /// <summary>
    /// The organization unit type, if the party is an organization.
    /// </summary>
    OrganizationUnitType = 1 << 16,

    /// <summary>
    /// The organization telephone number, if the party is an organization.
    /// </summary>
    OrganizationTelephoneNumber = 1 << 17,

    /// <summary>
    /// The organization mobile number, if the party is an organization.
    /// </summary>
    OrganizationMobileNumber = 1 << 18,

    /// <summary>
    /// The organization fax number, if the party is an organization.
    /// </summary>
    OrganizationFaxNumber = 1 << 19,

    /// <summary>
    /// The organization email address, if the party is an organization.
    /// </summary>
    OrganizationEmailAddress = 1 << 20,

    /// <summary>
    /// The organization internet address, if the party is an organization.
    /// </summary>
    OrganizationInternetAddress = 1 << 21,

    /// <summary>
    /// The organization mailing address, if the party is an organization.
    /// </summary>
    OrganizationMailingAddress = 1 << 22,

    /// <summary>
    /// The organization business address, if the party is an organization.
    /// </summary>
    OrganizationBusinessAddress = 1 << 23,

    /// <summary>
    /// All organization fields.
    /// </summary>
    Organization = OrganizationUnitStatus | OrganizationUnitType | OrganizationTelephoneNumber | OrganizationMobileNumber | OrganizationFaxNumber
        | OrganizationEmailAddress | OrganizationInternetAddress | OrganizationMailingAddress | OrganizationBusinessAddress,

    /// <summary>
    /// Include subunits (if party is an organization).
    /// </summary>
    SubUnits = 1 << 30,
}
