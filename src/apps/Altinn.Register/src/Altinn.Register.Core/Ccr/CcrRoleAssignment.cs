using Altinn.Authorization.ModelUtils;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Core.Ccr;

/// <summary>
/// Represents a role assignment with associated role code and optional personal identifier.
/// </summary>
public sealed class CcrRoleAssignment
{
    private CcrRoleAssignment(RoleAssignmentType type, string code, string? pid = default, string? org = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            ThrowHelper.ThrowArgumentException("Role code cannot be null or whitespace.", nameof(code));
        }

        if (type == RoleAssignmentType.Connection && string.IsNullOrWhiteSpace(org))
        {
            ThrowHelper.ThrowArgumentException("A connection role assignment must have an organization number.", nameof(org));
        }

        if (type == RoleAssignmentType.RoleAssignment && string.IsNullOrWhiteSpace(pid))
        {
            ThrowHelper.ThrowArgumentException("A personal role assignment must have a personal identifier.", nameof(pid));
        }

        if (type == RoleAssignmentType.BulkRoleAssignmentRemoval)
        {
            // For bulk role assignment removals, we require a role code but not a personal identifier or organization number.
            if (!string.IsNullOrWhiteSpace(pid) || !string.IsNullOrWhiteSpace(org))
            {
                ThrowHelper.ThrowArgumentException("Bulk role assignment removals should not have a personal identifier or organization number.", nameof(type));
            }
        }

        if (type == RoleAssignmentType.RoleAssignment && !string.IsNullOrWhiteSpace(org))
        {
            ThrowHelper.ThrowArgumentException("A personal role assignment should not have an organization number.", nameof(org));
        }

        if (type == RoleAssignmentType.Connection && !string.IsNullOrWhiteSpace(pid))
        {
            ThrowHelper.ThrowArgumentException("A connection role assignment should not have a personal identifier.", nameof(pid));
        }

        if (!string.IsNullOrWhiteSpace(org) && !string.IsNullOrWhiteSpace(pid))
        {
            ThrowHelper.ThrowArgumentException("Should not have both a pid and an org");
        }

        RoleAssignmentType = type;
        RoleCode = code;
        RolePersonalIdentifier = pid;
        RoleOrganizationNumber = org;
    }

    /// <summary>
    /// True for Rolle and False for Knytning.
    /// Samendring can represent either a role change (Rolle) or a connection change (Knytning).
    /// </summary>
    public RoleAssignmentType RoleAssignmentType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the identifier represents a personal (user-specific) identity.
    /// </summary>
    public bool IsPersonal => RolePersonalIdentifier != null;

    /// <summary>
    /// Gets a value indicating whether the role is associated with an organization.
    /// </summary>
    public bool IsOrganizational => RoleOrganizationNumber != null;

    /// <summary>
    /// Gets or sets the code that identifies the user's role.
    /// </summary>
    public string RoleCode { get; init; }

    /// <summary>
    /// Gets or sets the unique personal identifier associated with the role.
    /// </summary>
    public string? RolePersonalIdentifier { get; init; }

    /// <summary>
    /// Gets or sets the organization number associated with the role.
    /// </summary>
    public string? RoleOrganizationNumber { get; init; }

    #region BulkRoleAssignmentRemoval

    /// <summary>
    /// Creates a bulk role assignment removal for the specified set of roles ( implisit by code )
    /// </summary>
    /// <param name="code">The code of the roleset to remove. Cannot be null, empty, or consist only of white-space characters.</param>
    /// <returns>A <see cref="CcrRoleAssignment"/> representing the bulk role assignment removal for the specified role and
    /// subject.</returns>
    public static CcrRoleAssignment CreateBulkRoleAssignmentRemoval(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            ThrowHelper.ThrowArgumentException("Role code cannot be null or whitespace.", nameof(code));
        }

        return new CcrRoleAssignment(RoleAssignmentType.BulkRoleAssignmentRemoval, code, null, null)
        {
            RoleCode = code
        };
    }
    #endregion
    #region RoleAssignment

    /// <summary>
    /// Creates a new personal role assignment for the specified role code and personal identifier.
    /// </summary>
    /// <param name="code">The code representing the role to assign. Cannot be null or whitespace.</param>
    /// <param name="pid">The personal identifier associated with the role assignment. Cannot be null or whitespace.</param>
    /// <returns>A new instance of <see cref="CcrRoleAssignment"/> representing the personal role assignment.</returns>
    public static CcrRoleAssignment CreatePersonalRoleAssignment(string code, string pid)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            ThrowHelper.ThrowArgumentException("Role code cannot be null or whitespace.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(pid))
        {
            ThrowHelper.ThrowArgumentException("A person role assignment must have a personal identifier.", nameof(pid));
        }

        return new CcrRoleAssignment(RoleAssignmentType.RoleAssignment, code, pid, null)
        {
            RolePersonalIdentifier = pid
        };
    }

    /// <summary>
    /// Sets the fields related to only the roleassignment type for the current instance.
    /// </summary>
    public void SetRoleAssignmentFields(
        FieldValue<string> rolleAnsvarsDel,
        FieldValue<string> rolleFratraadt,
        FieldValue<string> rolleValgtav,
        FieldValue<string> rolleRekkefoelge,
        FieldValue<string> rolleFoedselsnr,
        FieldValue<string> fornavn,
        FieldValue<string> mellomnavn,
        FieldValue<string> slektsnavn,
        FieldValue<string> postnr,
        FieldValue<string> adr1,
        FieldValue<string> adr2,
        FieldValue<string> adr3,
        FieldValue<string> location,
        FieldValue<string> freeTextLine,
        FieldValue<string> freeTextRole,
        FieldValue<string> countryCode,
        FieldValue<string> personStatus)
    {
        RolleAnsvarsandel = rolleAnsvarsDel;
        RolleFratraadt = rolleFratraadt;
        RolleValgtav = rolleValgtav;
        RolleRekkefoelge = rolleRekkefoelge;
        RolleFoedselsnr = rolleFoedselsnr;
        Fornavn = fornavn;
        Mellomnavn = mellomnavn;
        Slektsnavn = slektsnavn;
        Postnr = postnr;
        Adresse1 = adr1;
        Adresse2 = adr2;
        Adresse3 = adr3;
        Location = location;
        FreeTextLine = freeTextLine;
        FreeTextRole = freeTextRole;
        AdresseLandkode = countryCode;
        Personstatus = personStatus;
    }

    /// <summary>
    /// The following list of fields are for ROLLE type "R"
    /// </summary>
    public FieldValue<string> RolleAnsvarsandel { get; private set; }

    /// <summary>
    /// Gets the date on which the role was resigned or ended.
    /// </summary>
    public FieldValue<string> RolleFratraadt { get; private set; }

    /// <summary>
    /// Gets the selected role value.
    /// </summary>
    public FieldValue<string> RolleValgtav { get; private set; }

    /// <summary>
    /// Gets the role sequence value.
    /// </summary>
    public FieldValue<string> RolleRekkefoelge { get; private set; }

    /// <summary>
    /// Gets the national identification number associated with the role.
    /// </summary>
    public FieldValue<string> RolleFoedselsnr { get; private set; }

    /// <summary>
    /// Gets the first name value associated with the field.
    /// </summary>
    public FieldValue<string> Fornavn { get; private set; }

    /// <summary>
    /// Gets the middle name associated with the entity.
    /// </summary>
    public FieldValue<string> Mellomnavn { get; private set; }

    /// <summary>
    /// Gets the family name associated with the entity.
    /// </summary>
    public FieldValue<string> Slektsnavn { get; private set; }

    /// <summary>
    /// Gets the postal code associated with the address.
    /// </summary>
    public FieldValue<string> Postnr { get; private set; }

    /// <summary>
    /// Gets the first line of the address.
    /// </summary>
    public FieldValue<string> Adresse1 { get; private set; }

    /// <summary>
    /// Gets the second line of the address, such as an apartment,
    /// suite, or building information.
    /// </summary>
    public FieldValue<string> Adresse2 { get; private set; }

    /// <summary>
    /// Gets the third line of the address.
    /// </summary>
    public FieldValue<string> Adresse3 { get; private set; }

    /// <summary>
    /// From Samendring Case "S" plassering
    /// </summary>
    public FieldValue<string> Location { get; private set; }

    /// <summary>
    /// From samendring Case "S" SamendringfritTekstlinje
    /// </summary>
    public FieldValue<string> FreeTextLine { get; private set; }

    /// <summary>
    /// from samendring Case "R" RollefritFoedselsnr
    /// </summary>
    public FieldValue<string> FreeTextRoleIdentifier { get; private set; }

    /// <summary>
    /// From samendring Case "R" RollefritTekstLinje
    /// </summary>
    public FieldValue<string> FreeTextRole { get; private set; }

    /// <summary>
    /// Gets the country code associated with the address.
    /// </summary>
    public FieldValue<string> AdresseLandkode { get; private set; }

    /// <summary>
    /// Gets the status of the person as a string value.
    /// </summary>
    public FieldValue<string> Personstatus { get; private set; }

    #endregion

    #region Connection

    /// <summary>
    /// Creates a new connection role assignment with the specified connection code and organization number.
    /// </summary>
    /// <param name="code">The unique code identifying the connection. Cannot be null, empty, or consist only of white-space characters.</param>
    /// <param name="org">The organization number associated with the connection. Cannot be null, empty, or consist only of white-space
    /// characters.</param>
    /// <returns>A new instance of <see cref="CcrRoleAssignment"/> representing the connection role assignment.</returns>
    public static CcrRoleAssignment CreateConnection(string code, string? org = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            ThrowHelper.ThrowArgumentException("Connection code cannot be null or whitespace.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(org))
        {
            ThrowHelper.ThrowArgumentException("A connection must have an organization number.", nameof(org));
        }

        return new CcrRoleAssignment(RoleAssignmentType.Connection, code, org: org)
        {
            RoleCode = code,
            RoleOrganizationNumber = org
        };
    }

    /// <summary>
    /// Sets the connection-related field values for the current instance.
    /// </summary>
    /// <param name="knytningfritTekstLinje">The free text line field value to assign. May be null if not applicable.</param>
    /// <param name="knytningAnsvarsandel">The responsibility share field value to assign. May be null if not applicable.</param>
    /// <param name="knytningFratraadt">The resignation field value to assign. May be null if not applicable.</param>
    /// <param name="knytningValgtav">The selection field value to assign. May be null if not applicable.</param>
    /// <param name="knytningRekkefoelge">The sequence field value to assign. May be null if not applicable.</param>
    public void SetConnectionFields(
        FieldValue<string> knytningfritTekstLinje,
        FieldValue<string> knytningAnsvarsandel,
        FieldValue<string> knytningFratraadt,
        FieldValue<string> knytningValgtav,
        FieldValue<string> knytningRekkefoelge)
    {
        KnytningfritTekstLinje = knytningfritTekstLinje;
        KnytningAnsvarsandel = knytningAnsvarsandel;
        KnytningFratraadt = knytningFratraadt;
        KnytningValgtav = knytningValgtav;
        KnytningRekkefoelge = knytningRekkefoelge;
    }

    /// <summary>
    /// From samendring Case "K" KnytningfritOrganisasjonsnummer
    /// </summary>
    public FieldValue<string> KnytningfritOrganisasjonsnummer { get; private set; }

    /// <summary>
    /// From samendring Case "K" KnytningfritTekstLinje
    /// </summary>
    public FieldValue<string> KnytningfritTekstLinje { get; private set; }

    /// <summary>
    /// Gets the responsibility share associated with the connection.
    /// </summary>
    public FieldValue<string> KnytningAnsvarsandel { get; private set; }

    /// <summary>
    /// Gets the date when the association was terminated.
    /// </summary>
    public FieldValue<string> KnytningFratraadt { get; private set; }

    /// <summary>
    /// Gets the organization number associated with the connection.
    /// </summary>
    public FieldValue<string> KnytningOrganisasjonsnummer { get; private set; }

    /// <summary>
    /// Gets the selected value for the 'KnytningValgtav' field.
    /// </summary>
    public FieldValue<string> KnytningValgtav { get; private set; }

    /// <summary>
    /// Gets the sequence number used to determine the order of association.
    /// </summary>
    public FieldValue<string> KnytningRekkefoelge { get; private set; }

    #endregion
}

/// <summary>
/// Specifies the type of role assignment operation to perform.
/// </summary>
/// <remarks>Use this enumeration to indicate whether the operation involves assigning a role, establishing a
/// connection, or performing a bulk removal of role assignments. This class is to be reworked into a Union in the future.</remarks>
public enum RoleAssignmentType
{
    /// <summary>
    /// Add new roles
    /// </summary>
    RoleAssignment,

    /// <summary>
    /// Add new connection for the org
    /// </summary>
    Connection,

    /// <summary>
    /// Removes multiple role assignments in a single batch. Such as the entire Board (styret)
    /// </summary>
    BulkRoleAssignmentRemoval
}
