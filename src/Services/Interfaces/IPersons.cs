#nullable enable

using System.Threading.Tasks;

using Altinn.Platform.Register.Models;

namespace Altinn.Register.Services.Interfaces;

/// <summary>
/// Interface handling methods for operations related to persons
/// </summary>
public interface IPersons
{
    /// <summary>
    /// Method that fetches a person based on a national identity number of the person.
    /// </summary>
    /// <param name="nationalIdentityNumber">The national identity number of the person to retrieve.</param>
    /// <returns>The identified person.</returns>
    Task<Person?> GetPerson(string nationalIdentityNumber);
}
