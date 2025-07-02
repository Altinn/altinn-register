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

    /// <summary>Gets a <see cref="ValidationErrorDescriptor"/>.</summary>
    public static ValidationErrorDescriptor TooManyItems { get; }
        = _factory.Create(1, "Too many items requested");

    /// <summary>Gets a <see cref="ValidationErrorDescriptor"/>.</summary>
    public static ValidationErrorDescriptor PartyUrn_Invalid { get; }
        = _factory.Create(2, "Invalid party URN");

    /// <summary>Gets a <see cref="ValidationErrorDescriptor"/>.</summary>
    public static ValidationErrorDescriptor Null { get; }
        = _factory.Create(3, "Value cannot be null");

    /// <summary>Gets a <see cref="ValidationErrorDescriptor"/>.</summary>
    public static ValidationErrorDescriptor Empty { get; }
        = _factory.Create(4, "List cannot be empty");
}
