namespace Altinn.Register.Core.Parties;

/// <summary>
/// Represents a mailing address.
/// </summary>
public record MailingAddress
{
    /// <summary>
    /// Gets the address part of the mailing address.
    /// </summary>
    public string? Address { get; init; }

    /// <summary>
    /// Gets the postal code of the mailing address.
    /// </summary>
    public string? PostalCode { get; init; }

    /// <summary>
    /// Gets the city of the mailing address.
    /// </summary>
    public string? City { get; init; }
}
