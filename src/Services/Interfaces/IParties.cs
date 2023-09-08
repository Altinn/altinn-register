using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;

namespace Altinn.Register.Services.Interfaces;

/// <summary>
/// Interface handling methods for operations related to parties
/// </summary>
public interface IParties
{
    /// <summary>
    /// Method that fetches a party based on a party id
    /// </summary>
    /// <param name="partyId">The party id</param>
    /// <returns></returns>
    Task<Party> GetParty(int partyId);

    /// <summary>
    /// Method that fetches a party based on social security number or organisation number.
    /// </summary>
    /// <param name="lookupValue">SSN or org number</param>
    /// <returns></returns>
    Task<Party> LookupPartyBySSNOrOrgNo(string lookupValue);

    /// <summary>
    /// Returns a list of Parties based on a list of partyIds.
    /// </summary>
    /// <param name="partyIds">List of partyIds.</param>
    /// <returns>A list of Parties.</returns>
    Task<List<Party>> GetPartyList(List<int> partyIds);
}
