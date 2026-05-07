using Altinn.Authorization.ModelUtils;
using Altinn.Register.Core.Parties.Records;
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
    /// <param name="fornavnMellomnavn">The given name, and optionally middle names</param>
    /// <param name="slektsnavn">The last name</param>
    /// <param name="postadresse">A <see cref="MailingAddressRecord"/></param>
    /// <returns>A new instance of <see cref="CcrRoleAssignment"/> representing the personal role assignment.</returns>
    public static CcrRoleAssignment CreatePersonalRoleAssignment(
        string code,
        string pid,
        FieldValue<string> fornavnMellomnavn,
        FieldValue<string> slektsnavn,
        FieldValue<MailingAddressRecord> postadresse)
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
            RolePersonalIdentifier = pid,
            Fornavn = fornavnMellomnavn,
            Slektsnavn = slektsnavn,
            Postadresse = postadresse
        };
    }

    /// <summary>
    /// Gets the first name value associated with the field.
    /// </summary>
    public FieldValue<string> Fornavn { get; private set; }

    /// <summary>
    /// Gets the family name associated with the entity.
    /// </summary>
    public FieldValue<string> Slektsnavn { get; private set; }

    /// <summary>
    /// Gets the postal address associated with the entity.
    /// </summary>
    public FieldValue<MailingAddressRecord> Postadresse { get; private set; }
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
