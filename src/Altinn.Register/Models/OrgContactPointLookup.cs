using System.Collections.Generic;

namespace Altinn.Register.Models;

/// <summary>
/// A class describing the query model for contact points for organisations
/// </summary>
public class OrgContactPointLookup
{
    /// <summary>
    /// Gets or sets the list of organisation numbers to lookup contact points for
    /// </summary>
    public List<string> OrganisationNumbers { get; set; }
}
