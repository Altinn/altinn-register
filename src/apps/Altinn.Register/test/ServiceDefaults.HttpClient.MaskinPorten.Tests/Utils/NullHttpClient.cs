namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Utils;

internal sealed class NullHttpClient
    : DelegatingHandler
{
    public NullHttpClient(ReadOnlySpan<DelegatingHandler> handlers)
    {
        HttpMessageHandler current = new NullHandler();
        for (int i = handlers.Length - 1; i >= 0; i--)
        {
            DelegatingHandler handler = handlers[i];
            handler.InnerHandler = current;
            current = handler;
        }

        InnerHandler = current;
    }

    public new Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => base.SendAsync(request, cancellationToken);
}
