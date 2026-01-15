using Microsoft.Extensions.Options;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Options;

/// <summary>
/// Configures <see cref="MaskinPortenClientOptions"/> with default values from <see cref="MaskinPortenCommonOptions"/>.
/// </summary>
internal sealed class ConfigureMaskinPortenClientOptionsFromCommonOptions
    : IConfigureNamedOptions<MaskinPortenClientOptions>
{
    private readonly IOptionsMonitor<MaskinPortenCommonOptions> _common;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureMaskinPortenClientOptionsFromCommonOptions"/> class.
    /// </summary>
    public ConfigureMaskinPortenClientOptionsFromCommonOptions(IOptionsMonitor<MaskinPortenCommonOptions> common)
    {
        _common = common;
    }

    /// <inheritdoc/>
    public void Configure(string? name, MaskinPortenClientOptions options)
    {
        Configure(options, _common.CurrentValue);
    }

    /// <inheritdoc/>
    public void Configure(MaskinPortenClientOptions options)
    {
        Configure(options, _common.CurrentValue);
    }

    private void Configure(MaskinPortenClientOptions options, MaskinPortenCommonOptions common)
    {
        options.Endpoint ??= common.Endpoint;
        options.Audience ??= common.Audience;
        options.TokenDuration ??= common.TokenDuration;
    }
}
