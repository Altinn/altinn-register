using Microsoft.AspNetCore.Http;

namespace Altinn.Register.TestUtils.Http.Filters;

internal sealed class RouteFilter
    : IFakeRequestFilter
{
    public static IFakeRequestFilter Create(PathString path)
        => new RouteFilter(path);

    private readonly PathString _path;

    private RouteFilter(PathString path)
    {
        _path = path;
    }

    public string Description => $"has path '{_path}'";

    public bool Matches(FakeHttpRequestMessage request)
    {
        if (request.RequestUri is null)
        {
            return false;
        }

        var baseUrl = FakeHttpMessageHandler.FakeBasePath;
        var relative = baseUrl.MakeRelativeUri(request.RequestUri).OriginalString;

        PathString path;
        var queryIndex = relative.IndexOf('?');

        if (queryIndex == -1)
        {
            path = "/" + relative;
        }
        else
        {
            path = string.Concat("/", relative.AsSpan(0, queryIndex));
        }

        // Paths are case-insensitive
        return string.Equals(_path, path, StringComparison.OrdinalIgnoreCase);
    }
}
