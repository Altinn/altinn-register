using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts;

namespace Altinn.Register.Core.Npr;

/// <summary>
/// Interface for calling NPR API.
/// </summary>
public interface INprClient
{
    /// <summary>
    /// Gets the person information from NPR based on the provided <see cref="PersonIdentifier"/>.
    /// </summary>
    /// <param name="personIdentifier">The identifier of the person.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the person information.</returns>
    Task<Result<NprPerson>> GetPerson(PersonIdentifier personIdentifier, CancellationToken cancellationToken);
}
