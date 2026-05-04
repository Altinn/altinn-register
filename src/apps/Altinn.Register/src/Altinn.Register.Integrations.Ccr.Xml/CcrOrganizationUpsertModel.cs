using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// Represents a full update to a Norwegian Central Coordinating Register for Legal Entities (CCR) (Enhetsregisteret ER) party,
/// including all relevant party information.
/// </summary>
/// <remarks> Use this class when a complete replacement of the party's data is required, rather than a partial update.
/// Inherits from CcrPartyUpdate, which provides base update functionality.</remarks>
public sealed class CcrOrganizationUpsertModel
    : CcrPartyUpdate
{
    /// <summary>
    /// Gets the organization identifier of the party, or <see langword="null"/> if the party is not an organization.
    /// </summary>
    public required OrganizationIdentifier OrganizationIdentifier { get; init; }

    /// <summary>
    /// Gets the display-name of the party.
    /// </summary>
    public required FieldValue<string> DisplayName { get; set; }

    /// <summary>
    /// Gets whether the party is deleted.
    /// </summary>
    public required bool IsDeleted { get; set; }

    /// <summary>
    /// Gets when the party was deleted.
    /// </summary>
    public required DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the organisation form.(Organisasjonsform)
    /// </summary>
    public FieldValue<string> UnitType { get; set; }

    /// <summary>
    /// Gets or sets the main case type.
    /// </summary>
    public FieldValue<string> Hovedsakstype { get; set; }

    /// <summary>
    /// Gets or sets the sub case type.
    /// </summary>
    public FieldValue<string> Undersakstype { get; set; }

    /// <summary>
    /// Gets or sets the first transfer date.
    /// </summary>
    public bool FoersteOverfoering { get; set; }

    /// <summary>
    /// Gets or sets the date of birth.
    /// </summary>
    public FieldValue<DateTimeOffset> DatoFoedt { get; set; }

    /// <summary>
    /// Gets or sets the date last changed.
    /// </summary>
    public FieldValue<DateTimeOffset> DatoSistEndret { get; set; }

    /// <summary>
    /// Gets the email address of the organization.
    /// </summary>
    public required FieldValue<string> EmailAddress { get; set; }

    /// <summary>
    /// Gets the internet address of the organization.
    /// </summary>
    public required FieldValue<string> InternetAddress { get; set; }

    /// <summary>
    /// Gets the telephone number of the organization.
    /// </summary>
    public required FieldValue<string> TelephoneNumber { get; set; }

    /// <summary>
    /// Gets the mobile number of the organization.
    /// </summary>
    public required FieldValue<string> MobileNumber { get; set; }

    /// <summary>
    /// Gets the fax number of the organization.
    /// </summary>
    public required FieldValue<string> FaxNumber { get; set; }

    /// <summary>
    /// Gets the mailing address of the organization.
    /// </summary>
    public required FieldValue<MailingAddressRecord> MailingAddress { get; set; }

    /// <summary>
    /// Gets the business address of the organization.
    /// </summary>
    public required FieldValue<MailingAddressRecord> BusinessAddress { get; set; }

    /// <summary>
    /// Gets or sets the co-changes.
    /// </summary>
    public List<CcrSamendring> Samendringer { get; set; } = [];
}
