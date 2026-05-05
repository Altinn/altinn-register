using Altinn.Register.Contracts;

namespace Altinn.Register.Core.Sire;

/// <summary>
/// Represents a person as returned by the NPR API, including their name, address, date of birth/death, and guardianship information.
/// </summary>
public sealed record SireOrganization
{
    /// <summary>
    /// Gets the identifier of the organization.
    /// </summary>
    public required OrganizationIdentifier OrganizationIdentifier { get; init; }

    // TODO: Add the remaining properties
}
