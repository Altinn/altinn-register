using Microsoft.Extensions.Internal;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Fakes;

internal sealed class FakeClock(TimeProvider provider)
    : ISystemClock
{
    public DateTimeOffset UtcNow => provider.GetUtcNow();
}
