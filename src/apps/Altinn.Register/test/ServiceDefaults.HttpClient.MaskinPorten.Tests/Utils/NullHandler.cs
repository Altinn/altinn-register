namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Utils;

internal sealed class NullHandler
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.NotImplemented)
        {
            RequestMessage = request
        };

        return Task.FromResult(response);
    }
}
