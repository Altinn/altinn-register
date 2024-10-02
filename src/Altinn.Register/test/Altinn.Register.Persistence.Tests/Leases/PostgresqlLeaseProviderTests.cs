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
    }

    [Fact]
    public async Task Can_Reacquire_Lease_After_Releasing()
    {
        var result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));
        Assert.True(result.IsLeaseAcquired);

        var released = await Provider.ReleaseLease(result.Lease);
        released.Should().BeTrue();

        var result2 = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));
        Assert.True(result2.IsLeaseAcquired);

        result.Lease.Token.Should().NotBe(result2.Lease.Token);
    }

    [Fact]
    public async Task Cannot_Acquire_Same_Lease_Twice()
    {
        var result1 = await Provider.TryAcquireLease("test1", TimeSpan.FromMinutes(1));
        Assert.True(result1.IsLeaseAcquired);

        var result2 = await Provider.TryAcquireLease("test1", TimeSpan.FromMinutes(1));
        Assert.False(result2.IsLeaseAcquired);

        result2.Expires.Should().Be(result1.Expires);
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
    }
}
