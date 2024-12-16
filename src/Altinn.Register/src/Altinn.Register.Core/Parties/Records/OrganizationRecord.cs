using System.Text.Json.Serialization;
using Altinn.Register.Core.Utils;

namespace Altinn.Register.Core.Parties.Records;

/// <summary>
/// A database record for an organization.
/// </summary>
[JsonConverter(typeof(PartyRecordJsonConverter))]
public sealed record OrganizationRecord
    : PartyRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationRecord"/> class.
    /// </summary>
    public OrganizationRecord()
        : base(Parties.PartyType.Organization)
    {
    }

    /// <summary>
    /// Gets the status of the organization.
    /// </summary>
    public required FieldValue<string> UnitStatus { get; init; }

    /// <summary>
    /// Gets the type of the organization.
    /// </summary>
    public required FieldValue<string> UnitType { get; init; }

    /// <summary>
    /// Gets the telephone number of the organization.
    /// </summary>
    public required FieldValue<string> TelephoneNumber { get; init; }

    /// <summary>
    /// Gets the mobile number of the organization.
    /// </summary>
    public required FieldValue<string> MobileNumber { get; init; }

    /// <summary>
    /// Gets the fax number of the organization.
    /// </summary>
    public required FieldValue<string> FaxNumber { get; init; }

    /// <summary>
    /// Gets the email address of the organization.
    /// </summary>
    public required FieldValue<string> EmailAddress { get; init; }

    /// <summary>
    /// Gets the internet address of the organization.
    /// </summary>
    public required FieldValue<string> InternetAddress { get; init; }

    /// <summary>
    /// Gets the mailing address of the organization.
    /// </summary>
    public required FieldValue<MailingAddress> MailingAddress { get; init; }

    /// <summary>
    /// Gets the business address of the organization.
    /// </summary>
    public required FieldValue<MailingAddress> BusinessAddress { get; init; }

    /// <summary>
    /// Gets the parent organization of the organization (if any).
    /// </summary>
    [JsonIgnore]
    public FieldValue<Guid> ParentOrganizationUuid { get; init; }
}
