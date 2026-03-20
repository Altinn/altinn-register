using Altinn.Register.Core.RateLimiting;

namespace Altinn.Register.Tests.Mocks;

public sealed class MockRateLimitProvider
    : IRateLimitProvider
{
    public ValueTask<RateLimitStatus> GetStatus(
        string policyName,
        string resource,
        string subject,
        BlockedRequestBehavior blockedRequestBehavior,
        TimeSpan blockDuration,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ValueTask<RateLimitStatus> Record(
        string policyName,
        string resource,
        string subject,
        ushort cost,
        int limit,
        TimeSpan windowDuration,
        RateLimitWindowBehavior windowBehavior,
        TimeSpan blockDuration,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
