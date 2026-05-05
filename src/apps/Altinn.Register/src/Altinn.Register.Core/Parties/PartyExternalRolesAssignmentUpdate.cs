using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Base class for updates to party external role assignments.
/// This is a union between a <see cref="Full"/> and a <see cref="Delta"/>.
/// </summary>
public abstract record PartyExternalRolesAssignmentUpdate
{
    // Prevents extending this record outside of this class.
    private PartyExternalRolesAssignmentUpdate()
    {
    }

    /// <summary>
    /// Gets the source of the role assignment.
    /// </summary>
    public ExternalRoleSource RoleSource { get; init; }

    /// <summary>
    /// Represents a full update of party external role assignments, where the provided list of assignments
    /// is the complete and authoritative list of assignments for the party from the source.
    /// </summary>
    public sealed record Full
        : PartyExternalRolesAssignmentUpdate
    {
        /// <summary>
        /// Gets the complete list of party external role assignments for the party from the source.
        /// </summary>
        public required ImmutableValueArray<PartyExternalRoleAssignment> Assignments { get; init; }
    }

    /// <summary>
    /// Represents a delta update of party external role assignments, where the provided lists of assignments to add and remove
    /// are applied as changes to the existing assignments for the party from the source.
    /// </summary>
    public sealed record Delta
        : PartyExternalRolesAssignmentUpdate
    {
        /// <summary>
        /// Gets the list of party external role assignments to remove by identifier,
        /// where each item identifies an assignment to remove by its external role identifier.
        /// </summary>
        public required ImmutableValueArray<PartyExternalRoleByIdentifierBulkRemoval> ToBulkRemove { get; init; }

        /// <summary>
        /// Gets the list of party external role assignments to remove by reference,
        /// where each item identifies an assignment to remove by its reference.
        /// </summary>
        public required ImmutableValueArray<PartyExternalRoleRemoval> ToRemove { get; init; }

        /// <summary>
        /// Gets the list of party external role assignments to add,
        /// where each item identifies an assignment to add by its external role identifier.
        /// </summary>
        public required ImmutableValueArray<PartyExternalRoleAssignment> ToAdd { get; init; }
    }
}

/// <summary>
/// Represents a reference to a party in the context of an external-role assignment.
/// </summary>
public abstract record PartyExternalRoleAssignmentPartyRef
{
    private PartyExternalRoleAssignmentPartyRef()
    {
    }

    /// <summary>
    /// A reference to a party by its UUID.
    /// </summary>
    public sealed record PartyUuid
        : PartyExternalRoleAssignmentPartyRef
    {
        /// <summary>
        /// Gets the UUID of the party.
        /// </summary>
        public required Guid Uuid { get; init; }
    }

    /// <summary>
    /// A reference to a party by its organization identifier.
    /// </summary>
    public sealed record Person
        : PartyExternalRoleAssignmentPartyRef
    {
        /// <summary>
        /// Gets the person identifier of the party.
        /// </summary>
        public required PersonIdentifier PersonIdentifier { get; init; }

        // TODO: name and address and stuff
    }

    /// <summary>
    /// A reference to a party by its person identifier.
    /// </summary>
    public sealed record Organization
        : PartyExternalRoleAssignmentPartyRef
    {
        /// <summary>
        /// Gets the organization identifier of the party.
        /// </summary>
        public required OrganizationIdentifier OrganizationIdentifier { get; init; }
    }
}

/// <summary>
/// Represents an assignment of an external role to a party, where the party is identified by a reference and the role is identified by its external role identifier.
/// </summary>
public sealed record PartyExternalRoleAssignment
{
    /// <summary>
    /// Gets the reference to the party to which the external role is assigned.
    /// </summary>
    public required PartyExternalRoleAssignmentPartyRef ToParty { get; init; }

    /// <summary>
    /// Gets the identifier of the external role to assign to the party.
    /// </summary>
    public required string ExternalRoleIdentifier { get; init; }
}

/// <summary>
/// Represents a removal of an external role assignment from a party, where the party is identified by a reference and the role is identified by its external role identifier.
/// </summary>
public sealed record PartyExternalRoleRemoval
{
    /// <summary>
    /// Gets the reference to the party to which the external role is assigned.
    /// </summary>
    public required PartyExternalRoleAssignmentPartyRef ToParty { get; init; }

    /// <summary>
    /// Gets the identifier of the external role to remove from the party.
    /// </summary>
    public required string ExternalRoleIdentifier { get; init; }
}

/// <summary>
/// Represents a removal of an external role assignment from a party, where the assignment to remove is identified by its external role identifier.
/// </summary>
public sealed record PartyExternalRoleByIdentifierBulkRemoval
{
    /// <summary>
    /// Gets the identifier of the external role to remove in bulk.
    /// </summary>
    public required string ExternalRoleIdentifier { get; init; }
}
