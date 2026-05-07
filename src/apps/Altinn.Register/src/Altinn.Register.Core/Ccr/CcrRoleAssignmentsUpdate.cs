namespace Altinn.Register.Core.Ccr;

/// <summary>
/// Represents an update to a role assignment in the context of the Norwegian Central Coordinating Register for Legal Entities (CCR).
/// </summary>
public class CcrRoleAssignmentsUpdate
{
    /// <summary>
    /// Gets or sets a value indicating whether the update is a full update.
    /// </summary>
    public bool IsFullUpdate { get; set; }

    /// <summary>
    /// Gets or sets the collection of role assignments associated with the current entity.
    /// To be added or updated role assignments should be included in this collection.
    /// For a full update, this collection should contain all role assignments that should exist after the update.
    /// Contains both personal role assignments (Rolle) and connection role assignments (Knytning),
    /// distinguished by the RoleAssignmentType property of each CcrRoleAssignment.
    /// </summary>
    public List<CcrRoleAssignment> RoleAssignments { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of role assignments to be removed
    /// </summary>
    public List<CcrRoleAssignment> RemoveRoleAssignments { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of role assignments to be removed in bulk operations.
    /// </summary>
    public List<CcrRoleAssignment> BulkRemoveRoleAssignments { get; set; } = [];
}
