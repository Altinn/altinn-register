#nullable enable
using Altinn.Register.Contracts.V1;

namespace Altinn.Register.Core
{
    /// <summary>
    /// Describes the methods required by a person check service.
    /// </summary>
    public interface IPersonLookup
    {
        /// <summary>
        /// Operation for checking if a given national identity number is connected to a person.
        /// </summary>
        /// <param name="nationalIdentityNumber">The national identity number to check.</param>
        /// <param name="lastName">The last name of the person. Must match the last name of the person.</param>
        /// <param name="activeUser">The unique id of the user performing the check.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The identified <see cref="Person"/> if last name was correct.</returns>
        Task<Person?> GetPerson(string nationalIdentityNumber, string lastName, int activeUser, CancellationToken cancellationToken = default);
    }
}
