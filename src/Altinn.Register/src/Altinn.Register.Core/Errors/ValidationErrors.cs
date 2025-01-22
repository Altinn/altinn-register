using Altinn.Authorization.ProblemDetails;

namespace Altinn.Register.Core.Errors;

/// <summary>
/// Validation errors for Register.
/// </summary>
public static class ValidationErrors
{
    private static readonly ValidationErrorDescriptorFactory _factory
        = ValidationErrorDescriptorFactory.New("REG");

    /// <summary>Gets a <see cref="ValidationErrorDescriptor"/>.</summary>
    public static ValidationErrorDescriptor PartyFields_SubUnits_Forbidden { get; }
        = _factory.Create(0, "Cannot request party subunits");
}
