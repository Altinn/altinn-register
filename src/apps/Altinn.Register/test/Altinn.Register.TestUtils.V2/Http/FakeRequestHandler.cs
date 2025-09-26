using System.Collections.Immutable;
using System.Text;

namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// A base class for fake request handlers that can configure request filtering.
/// </summary>
public sealed class FakeRequestHandler
    : BaseFakeRequestHandler
    , IFilterFakeRequest
    , IFakeRequestBuilder
{
    private ImmutableList<IFakeRequestFilter> _filters = [];

    /// <inheritdoc/>
    protected override bool CanHandle(FakeRequestContext context)
    {
        var filters = _filters;
        var request = context.Request;

        foreach (var filter in filters)
        {
            if (!filter.Matches(request))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    void IFilterFakeRequest.AddFilter(IFakeRequestFilter filter)
    {
        ImmutableInterlocked.Update(ref _filters, static (filters, filter) => filters.Add(filter), filter);
    }

    /// <inheritdoc/>
    protected override string Description
        => GetDescription();

    private string GetDescription()
    {
        var filters = _filters;

        if (filters.Count == 0)
        {
            return "Handler for all requests.";
        }

        if (filters.Count == 1)
        {
            return $"Handler for requests that {filters[0].Description}";
        }

        var sb = new StringBuilder("Handler for requests that:");
        foreach (var filter in filters)
        {
            sb.AppendLine().Append(" * ").Append(filter.Description);
        }

        return sb.ToString();
    }
}
