namespace Altinn.Register.Core.Ccr;

/// <summary>
/// Settings for the <see cref="CcrService"/>.
/// </summary>
public sealed class CcrServiceSettings
{
    /// <summary>
    /// Allowed CCR clients, keyed by username.
    /// </summary>
    public Dictionary<string, CcrClientIdentitySettings> Clients { get; set; }
        = new(StringComparer.Ordinal);
}
