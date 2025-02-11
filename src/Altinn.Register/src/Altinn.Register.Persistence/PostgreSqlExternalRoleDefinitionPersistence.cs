using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.ExternalRoles;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Persistence;

/// <summary>
/// Persistence service for external role definitions.
/// </summary>
internal sealed partial class PostgreSqlExternalRoleDefinitionPersistence 
    : IExternalRoleDefinitionPersistence
{
    private readonly Cache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlExternalRoleDefinitionPersistence"/> class.
    /// </summary>
    public PostgreSqlExternalRoleDefinitionPersistence(Cache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Tries to get the role definition for the specified source and identifier.
    /// </summary>
    /// <param name="source">The role definition source.</param>
    /// <param name="identifier">The role definition identifier.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="ExternalRoleDefinition"/>, if found.</returns>
    public ValueTask<ExternalRoleDefinition?> TryGetRoleDefinition(ExternalRoleSource source, string identifier, CancellationToken cancellationToken = default)
        => _cache.TryGetRoleDefinition(source, identifier, cancellationToken);

    /// <summary>
    /// Tries to get the role definition for the specified role-code.
    /// </summary>
    /// <param name="roleCode">The role definition role-code.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="ExternalRoleDefinition"/>, if found.</returns>
    public ValueTask<ExternalRoleDefinition?> TryGetRoleDefinitionByRoleCode(string roleCode, CancellationToken cancellationToken = default)
        => _cache.TryGetRoleDefinitionByRoleCode(roleCode, cancellationToken);
}
