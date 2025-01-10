using System.Buffers;
using Microsoft.AspNetCore.Routing;
using Nerdbank.Streams;

namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// A fake http request message.
/// </summary>
public class FakeHttpRequestMessage
    : HttpRequestMessage
{
    private static HttpRequestOptionsKey<T> CreateTypeKey<T>()
        => new($"{nameof(FakeHttpExtensions)}.{typeof(T).Name}");

    private static readonly HttpRequestOptionsKey<RouteData> _routeDataKey = CreateTypeKey<RouteData>();

    internal static async Task<FakeHttpRequestMessage> Create(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Sequence<byte>? buffer = null;
        SequenceHttpContent? sequenceContent = null;
        try
        {
            if (request.Content is { } content)
            {
                buffer = new(ArrayPool<byte>.Shared);
                await content.CopyToAsync(buffer.AsStream(), cancellationToken);
                sequenceContent = new SequenceHttpContent(buffer);
                buffer = null;

                foreach (var h in content.Headers)
                {
                    sequenceContent.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }
            }

            FakeHttpRequestMessage ret = new(request, sequenceContent);
            sequenceContent = null;
            return ret;
        }
        finally
        {
            sequenceContent?.Dispose();
            buffer?.Dispose();
        }
    }

    private FakeHttpRequestMessage(HttpRequestMessage original, SequenceHttpContent? content)
        : base(original.Method, original.RequestUri)
    {
        base.Content = content;
        Version = original.Version;
        VersionPolicy = original.VersionPolicy;
        
        foreach (var h in original.Headers)
        {
            Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        foreach (var o in original.Options)
        {
            ((IDictionary<string, object?>)Options)[o.Key] = o.Value;
        }
    }

    /// <inheritdoc cref="HttpRequestMessage.Content"/>
    public new SequenceHttpContent? Content
    {
        get => (SequenceHttpContent?)base.Content;
    }

    /// <summary>
    /// Gets or sets the route data for the request.
    /// </summary>
    public RouteData RouteData
    {
        get => Options.TryGetValue(_routeDataKey, out var routeData) ? routeData : throw new InvalidOperationException("Route data not set.");
        set => Options.Set(_routeDataKey, value);
    }
}
