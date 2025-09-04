using Microsoft.AspNetCore.WebUtilities;

namespace Altinn.Register.TestUtils.Http.Filters;

internal sealed class QueryParamFilter
    : IFakeRequestFilter
{
    public static IFakeRequestFilter Create(string key, string value)
        => new QueryParamFilter(key, value);

    private readonly string _key;
    private readonly string _value;

    private QueryParamFilter(string key, string value)
    {
        _key = key;
        _value = value;
    }

    public string Description => $"has query parameter {_key} with value {_value}";

    public bool Matches(FakeHttpRequestMessage request)
    {
        var queryString = request.RequestUri?.Query;
        if (queryString is null)
        {
            return false;
        }

        var parsed = QueryHelpers.ParseQuery(queryString);
        return parsed.TryGetValue(_key, out var values) && values.Contains(_value);
    }
}
