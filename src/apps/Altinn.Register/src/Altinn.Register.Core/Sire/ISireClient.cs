using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts;

namespace Altinn.Register.Core.Sire;

/// <summary>
/// Interface for calling SIRE API.
/// </summary>
public interface ISireClient
{
    /// <summary>
    /// Gets the organization information from SIRE based on the provided <see cref="OrganizationIdentifier"/>.
    /// </summary>
    /// <param name="organizationIdentifier">The identifier of the person.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the person information.</returns>
    Task<Result<SireOrganization>> GetPerson(OrganizationIdentifier organizationIdentifier, CancellationToken cancellationToken);
}

/// <summary>
/// Temporary implementation of <see cref="ISireClient"/> that throws <see cref="NotImplementedException"/> for all methods. This is a placeholder and should be replaced with a real implementation that calls the SIRE API.
/// Move this to a new integrations project when implementing.
/// </summary>
public sealed class TempSireClient
    : ISireClient
{
    /// <inheritdoc/>
    public Task<Result<SireOrganization>> GetPerson(OrganizationIdentifier organizationIdentifier, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
