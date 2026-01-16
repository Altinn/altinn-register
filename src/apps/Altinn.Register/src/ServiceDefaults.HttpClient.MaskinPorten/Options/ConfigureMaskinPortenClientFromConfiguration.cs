using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Options;

/// <summary>
/// Configures instances of <see cref="MaskinPortenClientOptions"/> using values from an <see cref="IConfiguration"/>
/// source.
/// </summary>
internal sealed class ConfigureMaskinPortenClientFromConfiguration
    : IConfigureNamedOptions<MaskinPortenClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureMaskinPortenClientFromConfiguration"/> class.
    /// </summary>
    public ConfigureMaskinPortenClientFromConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private readonly IConfiguration _configuration;

    /// <inheritdoc/>
    public void Configure(string? name, MaskinPortenClientOptions options)
    {
        var section = _configuration.GetSection($"Altinn:MaskinPorten:Clients:{name}");
        section.Bind(options);

        var key = section.GetValue<string?>("Key", defaultValue: null);
        if (!string.IsNullOrEmpty(key))
        {
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(key));
                options.Key = JsonWebKey.Create(json);
            }
            catch (Exception e)
            {
                throw new FormatException($"Config 'Altinn:MaskinPorten:Clients:{name}:Key' is not a valid base64-encoded JWK", e);
            }
        }
    }

    /// <inheritdoc/>
    public void Configure(MaskinPortenClientOptions options)
    {
    }
}
