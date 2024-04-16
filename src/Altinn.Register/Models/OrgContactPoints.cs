using System.Collections.Generic;

namespace Altinn.Register.Models;

/// <summary>
/// A class describing the contact points of a unit
/// </summary>
public class OrgContactPoints
{
    /// <summary>
    /// Gets or sets the organisation number of the unit
    /// </summary>
    public string OrganisationNumber { get; set; }

    /// <summary>
    /// Gets or sets the list containing all the units email address contact points
    /// </summary>
    public List<string> EmailList { get; set; } = [];

    /// <summary>
    /// Gets or sets the list contaning all the units mobile number contact points
    /// </summary>
    public List<string> MobileNumberList { get; set; } = [];
}

/// <summary>
/// A list representation of <see cref="OrgContactPoints"/>
/// </summary>
public class OrgContactPointsList
{
    /// <summary>
    /// Gets or sets the list of all contact points
    /// </summary>
    public List<OrgContactPoints> ContactPointsList { get; set; } = [];
}
