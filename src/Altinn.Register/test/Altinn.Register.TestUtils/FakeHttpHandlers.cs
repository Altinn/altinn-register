using System.Collections.Concurrent;
using Altinn.Register.TestUtils.Http;

namespace Altinn.Register.TestUtils;

/// <summary>
/// A collection of fake HTTP handlers.
/// </summary>
public sealed class FakeHttpHandlers
    : IDisposable
{
    private readonly ConcurrentDictionary<string, FakeHttpMessageHandler> _handlers = new();

    /// <summary>
    /// Gets a fake HTTP handler for the specified name.
    /// </summary>
    /// <param name="name">The http client name.</param>
    /// <returns>The <see cref="FakeHttpMessageHandler"/> for the client.</returns>
    public FakeHttpMessageHandler For(string name)
        => _handlers.GetOrAdd(name, _ => new FakeHttpMessageHandler());

    /// <summary>
    /// Gets a fake HTTP handler for the specified client type.
    /// </summary>
    /// <typeparam name="TClient">The client type.</typeparam>
    /// <returns>The <see cref="FakeHttpMessageHandler"/> for the client.</returns>
    public FakeHttpMessageHandler For<TClient>()
    {
        string name = TypeNameHelper.GetTypeDisplayName(typeof(TClient), fullName: false);

        return For(name);
    }

    void IDisposable.Dispose()
    {
        // copy to only take the lock once
        var handlers = _handlers.ToArray();

        foreach (var (_, handler) in handlers)
        {
            handler.Dispose();
        }
    }
}
