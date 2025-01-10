namespace Altinn.Register.TestUtils.Http.Filters;

internal sealed class RouteValueFilter
    : IFakeRequestFilter
{
    public static IFakeRequestFilter Create(string key, Predicate<object?> predicate)
        => new RouteValueFilter(key, predicate);

    private readonly string _key;
    private readonly Predicate<object?> _predicate;

    private RouteValueFilter(string key, Predicate<object?> predicate)
    {
        _key = key;
        _predicate = predicate;
    }

    public string Description => $"has route value '{_key}' that matches predicate";

    public bool Matches(FakeHttpRequestMessage request)
    {
        if (!request.RouteData.Values.TryGetValue(_key, out var value))
        {
            return false;
        }

        return _predicate(value);
    }
}
