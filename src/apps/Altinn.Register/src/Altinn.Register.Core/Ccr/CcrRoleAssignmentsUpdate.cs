namespace Altinn.Register.Core.Ccr;

/// <summary>
/// Represents an update to a role assignment in the context of the Norwegian Central Coordinating Register for Legal Entities (CCR).
/// </summary>
/// <remarks>
/// Equality is order-insensitive: two instances are considered equal when their role-assignment collections contain the same items
/// with the same multiplicities, regardless of order.
/// </remarks>
public sealed record CcrRoleAssignmentsUpdate
{
    /// <summary>
    /// Gets a value indicating whether the update is a full update.
    /// </summary>
    public bool IsFullUpdate { get; init; }

    /// <summary>
    /// Gets the collection of role assignments associated with the current entity.
    /// To be added or updated role assignments should be included in this collection.
    /// For a full update, this collection should contain all role assignments that should exist after the update.
    /// Contains both personal role assignments (Rolle) and connection role assignments (Knytning),
    /// distinguished by the RoleAssignmentType property of each CcrRoleAssignment.
    /// </summary>
    public IReadOnlyList<CcrRoleAssignment> RoleAssignments { get; init; } = [];

    /// <summary>
    /// Gets the collection of role assignments to be removed.
    /// RD or KD fratraadt
    /// </summary>
    public IReadOnlyList<CcrRoleAssignment> RemoveRoleAssignments { get; init; } = [];

    /// <summary>
    /// Gets the collection of SAMU role assignments to be removed in bulk operations.
    /// STYR: LEDE, NEST, MEDL, OBS.
    /// DELT: DTSO, DTPR
    /// SIGN: SIGN, SIFE, SIHV
    /// PROK: PROK, KENK, KGRL
    /// </summary>
    public IReadOnlyList<CcrRoleAssignment> BulkRemoveRoleAssignments { get; init; } = [];

    /// <inheritdoc />
    public bool Equals(CcrRoleAssignmentsUpdate? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return IsFullUpdate == other.IsFullUpdate
            && MultisetEqual(RoleAssignments, other.RoleAssignments)
            && MultisetEqual(RemoveRoleAssignments, other.RemoveRoleAssignments)
            && MultisetEqual(BulkRemoveRoleAssignments, other.BulkRemoveRoleAssignments);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(IsFullUpdate);
        hash.Add(MultisetHash(RoleAssignments));
        hash.Add(MultisetHash(RemoveRoleAssignments));
        hash.Add(MultisetHash(BulkRemoveRoleAssignments));
        return hash.ToHashCode();
    }

    private static bool MultisetEqual(IReadOnlyList<CcrRoleAssignment> a, IReadOnlyList<CcrRoleAssignment> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        if (a.Count == 0)
        {
            return true;
        }

        var counts = new Dictionary<CcrRoleAssignment, int>(a.Count);
        foreach (var item in a)
        {
            counts[item] = counts.TryGetValue(item, out var c) ? c + 1 : 1;
        }

        foreach (var item in b)
        {
            if (!counts.TryGetValue(item, out var c) || c == 0)
            {
                return false;
            }

            counts[item] = c - 1;
        }

        return true;
    }

    private static int MultisetHash(IReadOnlyList<CcrRoleAssignment> items)
    {
        int hash = 0;
        foreach (var item in items)
        {
            hash = unchecked(hash + item.GetHashCode());
        }

        return hash;
    }
}
