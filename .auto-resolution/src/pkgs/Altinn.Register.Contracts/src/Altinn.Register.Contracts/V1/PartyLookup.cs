using System.ComponentModel.DataAnnotations;

namespace Altinn.Register.Contracts.V1;

/// <summary>
/// Represents a lookup criteria when looking for a Party. Only one of the properties can be used at a time.
/// If none or more than one property have a value the lookup operation will respond with bad request.
/// </summary>
public record PartyLookup
    : IValidatableObject
{
    /// <summary>
    /// Gets or sets the social security number of the party to look for.
    /// </summary>
    [JsonPropertyName("ssn")]
    [RegularExpression("^[0-9]{11}$", ErrorMessage = "Value needs to be exactly 11 digits.")]
    public string? Ssn { get; set; }

    /// <summary>
    /// Gets or sets the organization number of the party to look for.
    /// </summary>
    [JsonPropertyName("orgNo")]
    [RegularExpression("^[0-9]{9}$", ErrorMessage = "Value needs to be exactly 9 digits.")]
    public string? OrgNo { get; set; }

    /// <summary>
    /// Error message for when both <see cref="Ssn"/> and <see cref="OrgNo"/> are null.
    /// </summary>
    internal static readonly string SsnOrOrgNoRequiredMessage = $"Either {nameof(Ssn)} or {nameof(OrgNo)} is required.";

    /// <summary>
    /// Error message for when both <see cref="Ssn"/> and <see cref="OrgNo"/> are set.
    /// </summary>
    internal static readonly string SsnAndOrgNoExclusiveMessage = $"Only one of {nameof(Ssn)} and {nameof(OrgNo)} is allowed.";

    /// <summary>
    /// Determines if this instance of the model is valid.
    /// </summary>
    /// <param name="validationContext">The current context of the validation check</param>
    /// <returns>A list of validation results.</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Ssn is null && OrgNo is null)
        {
            yield return new ValidationResult(SsnOrOrgNoRequiredMessage, [nameof(Ssn), nameof(OrgNo)]);
        }
        else if (Ssn is not null && OrgNo is not null)
        {
            yield return new ValidationResult(SsnAndOrgNoExclusiveMessage, [nameof(OrgNo)]);
        }
    }
}
