using CommunityToolkit.Diagnostics;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Handlers;

/// <summary>
/// A handler that configures a request's MaskinPorten client name.
/// </summary>
/// <remarks>
/// This handler sets the MaskinPorten client name in the request options, allowing downstream handlers
/// to identify which MaskinPorten client configuration to use for the request.
/// </remarks>
internal sealed class MaskinPortenRequestConfigHandler
    : AsyncOnlyDelegatingHandler
{
    private readonly string _clientName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaskinPortenRequestConfigHandler"/> class.
    /// </summary>
    /// <param name="clientName">The MaskinPorten client name.</param>
    public MaskinPortenRequestConfigHandler(string clientName)
    {
        Guard.IsNotNullOrEmpty(clientName);
        _clientName = clientName;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Options.MaskinPortenClientName ??= _clientName;

        return base.SendAsync(request, cancellationToken);
    }
}
