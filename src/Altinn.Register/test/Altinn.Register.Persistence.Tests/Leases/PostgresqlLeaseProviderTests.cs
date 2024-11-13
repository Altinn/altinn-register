using Altinn.Register.Persistence.Leases;

namespace Altinn.Register.Persistence.Tests.Leases;

public class PostgresqlLeaseProviderTests
    : DatabaseTestBase
{
    protected override bool SeedData => false;

    private PostgresqlLeaseProvider? _provider;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        _provider = GetRequiredService<PostgresqlLeaseProvider>();
    }

    private PostgresqlLeaseProvider Provider
        => _provider!;

    [Fact]
    public async Task Can_Acquire_Lease()
    {
        var now = TimeProvider.GetUtcNow();

        var result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));
        Assert.True(result.IsLeaseAcquired);

        result.Expires.Should().BeOnOrAfter(now.AddMinutes(1));
        result.Lease.Expires.Should().Be(result.Expires);
        result.Lease.Token.Should().NotBeEmpty();
        result.LastAcquiredAt.Should().Be(now);
        result.LastReleasedAt.Should().BeNull();
    }

    [Fact]
    public async Task Can_Conditionally_Acquire_Lease()
    {
        // before lease is created
        var result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), info =>
        {
            info.LastAcquiredAt.Should().BeNull();
            info.LastReleasedAt.Should().BeNull();

            // reject lease
            return false;
        });

        result.IsLeaseAcquired.Should().BeFalse();
        result.LastAcquiredAt.Should().BeNull();
        result.LastReleasedAt.Should().BeNull();
        result.Expires.Should().BeBefore(TimeProvider.GetUtcNow());

        var acquireTime = TimeProvider.GetUtcNow();

        // create lease
        result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), info =>
        {
            info.LastAcquiredAt.Should().BeNull();
            info.LastReleasedAt.Should().BeNull();

            // accept lease
            return true;
        });

        Assert.True(result.IsLeaseAcquired);
        result.LastAcquiredAt.Should().Be(acquireTime);
        result.LastReleasedAt.Should().BeNull();
        result.Expires.Should().Be(acquireTime.AddMinutes(1));

        TimeProvider.Advance(TimeSpan.FromSeconds(10));
        var releaseTime = TimeProvider.GetUtcNow();
        var released = await Provider.ReleaseLease(result.Lease);
        released.Should().BeTrue();

        TimeProvider.Advance(TimeSpan.FromSeconds(10));

        result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), info =>
        {
            info.LastAcquiredAt.Should().Be(acquireTime);
            info.LastReleasedAt.Should().Be(releaseTime);

            // reject lease
            return false;
        });

        result.IsLeaseAcquired.Should().BeFalse();
        result.LastAcquiredAt.Should().Be(acquireTime);
        result.LastReleasedAt.Should().Be(releaseTime);
        result.Expires.Should().BeBefore(TimeProvider.GetUtcNow());

        var now = TimeProvider.GetUtcNow();
        result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), info =>
        {
            info.LastAcquiredAt.Should().Be(acquireTime);
            info.LastReleasedAt.Should().Be(releaseTime);

            // accept lease
            return true;
        });

        result.IsLeaseAcquired.Should().BeTrue();
        result.LastAcquiredAt.Should().Be(now);
        result.LastReleasedAt.Should().Be(releaseTime);
        result.Expires.Should().Be(now + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Can_Reacquire_Lease_After_Releasing()
    {
        var result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));
        Assert.True(result.IsLeaseAcquired);

        TimeProvider.Advance(TimeSpan.FromSeconds(10));
        var releaseTime = TimeProvider.GetUtcNow();

        var released = await Provider.ReleaseLease(result.Lease);
        released.Should().BeTrue();

        TimeProvider.Advance(TimeSpan.FromSeconds(10));
        var reacquireTime = TimeProvider.GetUtcNow();

        var result2 = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));
        Assert.True(result2.IsLeaseAcquired);

        result.Lease.Token.Should().NotBe(result2.Lease.Token);
        result2.LastAcquiredAt.Should().Be(reacquireTime);
        result2.LastReleasedAt.Should().Be(releaseTime);
    }

    [Fact]
    public async Task Cannot_Acquire_Same_Lease_Twice()
    {
        var acquireTime = TimeProvider.GetUtcNow();
        var result1 = await Provider.TryAcquireLease("test1", TimeSpan.FromMinutes(1));
        Assert.True(result1.IsLeaseAcquired);

        TimeProvider.Advance(TimeSpan.FromSeconds(10));
        var result2 = await Provider.TryAcquireLease("test1", TimeSpan.FromMinutes(1));
        Assert.False(result2.IsLeaseAcquired);

        result2.Expires.Should().Be(result1.Expires);
        result2.LastAcquiredAt.Should().Be(acquireTime);
    }

    [Fact]
    public async Task Can_Acquire_Multiple_Leases_At_Once()
    {
        var result1 = await Provider.TryAcquireLease("test1", TimeSpan.FromMinutes(1));
        Assert.True(result1.IsLeaseAcquired);

        var result2 = await Provider.TryAcquireLease("test2", TimeSpan.FromMinutes(1));
        Assert.True(result2.IsLeaseAcquired);
    }

    [Fact]
    public async Task Can_Acquire_Expired_Lease()
    {
        var result1 = await Provider.TryAcquireLease("test1", TimeSpan.FromMinutes(1));
        Assert.True(result1.IsLeaseAcquired);

        TimeProvider.Advance(TimeSpan.FromMinutes(1));

        var result2 = await Provider.TryAcquireLease("test1", TimeSpan.FromMinutes(1));
        Assert.True(result2.IsLeaseAcquired);

        result2.Lease.Token.Should().NotBe(result1.Lease.Token);
        result2.Lease.Expires.Should().NotBe(result1.Lease.Expires);
        result2.LastReleasedAt.Should().BeNull();
    }
}
