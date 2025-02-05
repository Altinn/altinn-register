namespace Altinn.Register.Persistence.DbArgTypes;

/// <summary>
/// Argument to the database function register.upsert_external_role_assignments.
/// </summary>
internal readonly record struct ArgUpsertExternalRoleAssignment
{
    /// <summary>
    /// Gets the party that the role is assigned to.
    /// </summary>
    public required readonly Guid ToParty { get; init; }

    /// <summary>
    /// Gets the identifier of the role that is assigned.
    /// </summary>
    public required readonly string Identifier { get; init; }
}
