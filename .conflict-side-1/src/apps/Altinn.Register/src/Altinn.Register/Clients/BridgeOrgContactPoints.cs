using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Altinn.Register.Models;

/// <summary>
/// A class describing the official contact points of a unit
/// </summary>
public class BridgeOrgContactPoints
{
    /// <summary>
    /// Gets or sets the organization number of the unit
    /// </summary>
    [JsonPropertyName("organisationNumber")]
    public string OrganisationNumber { get; set; }

    /// <summary>
    /// Gets or sets the list containing all the units email address contact points
    /// </summary>
    [JsonPropertyName("emailList")]
    public List<string> EmailList { get; set; } = [];

    /// <summary>
    /// Gets or sets the list contaning all the units mobile number contact points
    /// </summary>
    [JsonPropertyName("mobileNumberList")]
    public List<string> MobileNumberList { get; set; } = [];
}

/// <summary>
/// A list representation of <see cref="OrgContactPoints"/>
/// </summary>
public class BridgeOrgContactPointsList
{
    /// <summary>
    /// Gets or sets the list of all contact points
    /// </summary>
    [JsonPropertyName("contactPointsList")]
    public List<BridgeOrgContactPoints> ContactPointsList { get; set; } = [];
}
