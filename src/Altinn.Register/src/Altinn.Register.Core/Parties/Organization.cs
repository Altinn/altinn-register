namespace Altinn.Register.Core.Parties;

/// <summary>
/// Represents an organization.
/// </summary>
public sealed record Organization
    : Party
{
    /// <summary>
    /// Initialize a <see cref="Organization"/>.
    /// </summary>
    public Organization()
        : base(PartyType.Organization)
    {
    }

    /// <summary>
    /// Gets the status of the organization.
    /// </summary>
    public required string UnitStatus { get; init; }
    
    /// <summary>
    /// Gets the type of the organization.
    /// </summary>
    public required string UnitType { get; init; }
    
    /// <summary>
    /// Gets the telephone number of the organization.
    /// </summary>
    public required string? TelephoneNumber { get; init; }
    
    /// <summary>
    /// Gets the mobile number of the organization.
    /// </summary>
    public required string? MobileNumber { get; init; }
    
    /// <summary>
    /// Gets the fax number of the organization.
    /// </summary>
    public required string? FaxNumber { get; init; }
    
    /// <summary>
    /// Gets the email address of the organization.
    /// </summary>
    public required string? EmailAddress { get; init; }
    
    /// <summary>
    /// Gets the internet address of the organization.
    /// </summary>
    public required string? InternetAddress { get; init; }
    
    /// <summary>
    /// Gets the mailing address of the organization.
    /// </summary>
    public required MailingAddress? MailingAddress { get; init; }
    
    /// <summary>
    /// Gets the business address of the organization.
    /// </summary>
    public required MailingAddress? BusinessAddress { get; init; }
}
