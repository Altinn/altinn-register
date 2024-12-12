namespace Altinn.Register.TestUtils.Http.Filters;

internal sealed class HttpMethodFilter
    : IFakeRequestFilter
{
    public static IFakeRequestFilter Get(HttpMethod method)
        => new HttpMethodFilter(method);

    private readonly HttpMethod _method;

    private HttpMethodFilter(HttpMethod method)
    {
        _method = method;
    }

    public string Description => $"has method {_method}";

    public bool Matches(FakeHttpRequestMessage request)
        => request.Method == _method;
}
