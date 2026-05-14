using System.Net;

namespace Altinn.Register.Core.Ccr;

/// <summary>
/// Options for a CCR client, used for validating incoming CCR update requests.
/// </summary>
public sealed class CcrClientIdentitySettings
{
    /// <summary>
    /// The password hash for the client. Generated using <see cref="Cryptography.PasswordHash"/>.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Allowed source networks for the client. If empty, requests from any network are allowed.
    /// </summary>
    public List<IPNetwork> AllowedSourceNetworks { get; set; } = [];
}
