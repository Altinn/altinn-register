using System;
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
    /// Method that fetches a party based on a party uuid
    /// </summary>
    /// <param name="partyUuid">The party uuid</param>
    /// <returns></returns>
    Task<Party> GetPartyByUuid(Guid partyUuid);

    /// <summary>
    /// Method that fetches a party based on social security number or organisation number.
    /// </summary>
    /// <param name="lookupValue">SSN or org number</param>
    /// <returns></returns>
    Task<Party> LookupPartyBySSNOrOrgNo(string lookupValue);

    /// <summary>
    /// Method that fetches the names of a list of parties based on their social security number or organisation number.
    /// </summary>
    /// <param name="partyNamesLookup">The list of SSN or org number</param>
    /// <returns></returns>
    Task<PartyNamesLookupResult> LookupPartyNames(PartyNamesLookup partyNamesLookup);

    /// <summary>
    /// Returns a list of Parties based on a list of partyIds.
    /// </summary>
    /// <param name="partyIds">List of partyIds.</param>
    /// <param name="fetchSubUnits">flag indicating whether subunits should be included</param>
    /// <returns>A list of Parties.</returns>
    Task<List<Party>> GetPartyList(List<int> partyIds, bool fetchSubUnits = false);

    /// <summary>
    /// Returns a list of Parties based on a list of party uuids.
    /// </summary>
    /// <param name="partyUuids">List of party uuids.</param>
    /// <param name="fetchSubUnits">flag indicating whether subunits should be included</param>
    /// <returns>A list of Parties.</returns>
    Task<List<Party>> GetPartyListByUuid(List<Guid> partyUuids, bool fetchSubUnits = false);
}
