using System.Collections.Concurrent;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Fakes;

internal sealed class FakeMaskinPortenClient(TimeProvider timeProvider)
    : IMaskinPortenClient
{
    private readonly ConcurrentDictionary<string, MaskinPortenCacheKey> _cacheKeys = new();
    private uint _callCount;

    public uint CallCount => Volatile.Read(ref _callCount);

    public Task<MaskinPortenToken> GetAccessToken(string clientName, CancellationToken cancellationToken = default)
    {
        var callCount = Interlocked.Increment(ref _callCount);
        var accessToken = callCount.ToString();
        var validTo = timeProvider.GetUtcNow().AddMinutes(2);
        var key = _cacheKeys.GetOrAdd(clientName, static name => new MaskinPortenCacheKey(name, "fake-client-id", "fake-scope", resource: null, consumerOrg: null));

        MaskinPortenToken token = new(key, accessToken, validTo);
        return Task.FromResult(token);
    }
}
