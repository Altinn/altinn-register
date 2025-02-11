using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Core.ExternalRoles;

/// <summary>
/// Persistence service for external role definitions.
/// </summary>
public interface IExternalRoleDefinitionPersistence
{
    /// <summary>
    /// Tries to get the role definition for the specified source and identifier.
    /// </summary>
    /// <param name="source">The role definition source.</param>
    /// <param name="identifier">The role definition identifier.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="ExternalRoleDefinition"/>, if found.</returns>
    public ValueTask<ExternalRoleDefinition?> TryGetRoleDefinition(ExternalRoleSource source, string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to get the role definition for the specified role-code.
    /// </summary>
    /// <param name="roleCode">The role definition role-code.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="ExternalRoleDefinition"/>, if found.</returns>
    public ValueTask<ExternalRoleDefinition?> TryGetRoleDefinitionByRoleCode(string roleCode, CancellationToken cancellationToken = default);
}
