using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Altinn.Register.Models;

/// <summary>
/// A class describing the query model for contact points for organisations
/// </summary>
public class OrgContactPointLookup
{
    /// <summary>
    /// Gets or sets the list of organisation numbers to lookup contact points for
    /// </summary>
    [JsonPropertyName("organisationNumbers")]
    public List<string> OrganisationNumbers { get; set; }
}
