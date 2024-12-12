namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// Represents the context of a request.
/// </summary>
public class FakeRequestContext
{
    internal static async Task<FakeRequestContext> Create(FakeHttpMessageHandler fakeHttpMessageHandler, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        FakeHttpRequestMessage? requestClone = null;
        FakeHttpResponseMessage? response = null;

        try
        {
            requestClone = await FakeHttpRequestMessage.Create(request, cancellationToken);
            response = new(requestClone);

            FakeRequestContext ret = new(fakeHttpMessageHandler, requestClone, response);
            requestClone = null;
            response = null;
            return ret;
        }
        finally
        {
            response?.Dispose();
            requestClone?.Dispose();
        }
    }

    private readonly FakeHttpMessageHandler _handler;
    private readonly FakeHttpRequestMessage _request;
    private readonly FakeHttpResponseMessage _response;

    private FakeRequestContext(FakeHttpMessageHandler fakeHttpMessageHandler, FakeHttpRequestMessage request, FakeHttpResponseMessage response)
    {
        _handler = fakeHttpMessageHandler;
        _request = request;
        _response = response;
    }

    /// <summary>
    /// Gets the request.
    /// </summary>
    public FakeHttpRequestMessage Request
        => _request;

    /// <summary>
    /// Gets the response.
    /// </summary>
    public FakeHttpResponseMessage Response
        => _response;
}
