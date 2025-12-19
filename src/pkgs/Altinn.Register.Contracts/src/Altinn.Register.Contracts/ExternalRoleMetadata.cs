namespace Altinn.Register.Contracts;

/// <summary>
/// Represents metadata for an external role, including localized names and descriptions.
/// </summary>
public sealed record ExternalRoleMetadata
    : ExternalRoleRef
{
    /// <summary>
    /// Gets the localized names of the external role.
    /// </summary>
    public required TranslatedText Name { get; init; }

    /// <summary>
    /// Gets the localized descriptions of the external role.
    /// </summary>
    public required TranslatedText Description { get; init; }
}
