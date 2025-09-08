﻿using Altinn.Authorization.ServiceDefaults.Leases;
using Altinn.Register.TestUtils;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence.Tests.Leases;

public class LeaseManagerTests
    : DatabaseTestBase
{
    protected override bool SeedData => false;

    private ILeaseProvider? _provider;
    private LeaseManager? _manager;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        _provider = GetRequiredService<ILeaseProvider>();
        _manager = GetRequiredService<LeaseManager>();
    }

    protected override async ValueTask ConfigureServices(IServiceCollection services)
    {
        await base.ConfigureServices(services);
        
        services.AddLeaseManager();
    }

    private ILeaseProvider Provider
        => _provider!;

    private LeaseManager Manager
        => _manager!;

    [Fact]
    public async Task Returns_Null_When_Lease_Not_Acquired()
    {
        await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));

        await using var lease = await Manager.AcquireLease("test");
        Assert.False(lease.Acquired);
    }

    [Fact]
    public async Task Returns_Lease_When_Acquired()
    {
        await using var lease = await Manager.AcquireLease("test");
        Assert.True(lease.Acquired);
    }

    [Fact]
    public async Task Releases_Lease_When_Disposed()
    {
        {
            await using var lease = await Manager.AcquireLease("test");
            Assert.True(lease.Acquired);
        }

        {
            await using var lease2 = await Manager.AcquireLease("test");
            Assert.True(lease2.Acquired);
        }
    }

    [Fact]
    public async Task Lease_AutoRenews()
    {
        await using var lease = await Manager.AcquireLease("test");
        Assert.True(lease.Acquired);

        for (var i = 0; i < 10; i++)
        {
            var token = lease.Inner.LeaseToken;

            // forward time
            TimeProvider.SetUtcNow(lease.Inner.CurrentExpiry - TimeSpan.FromSeconds(1));

            // wait for tick task
            await lease.Inner.TickTask;

            lease.Inner.LeaseToken.Should().NotBe(token);
        }
    }

    [Fact]
    public async Task Lease_Token_Is_Linked()
    {
        using var tcs = new CancellationTokenSource();
        await using var lease = await Manager.AcquireLease("test", tcs.Token);
        Assert.True(lease.Acquired);

        lease.Token.IsCancellationRequested.Should().BeFalse();

        await tcs.CancelAsync();

        lease.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelling_Token_Stops_Renewal()
    {
        using var tcs = new CancellationTokenSource();
        await using var lease = await Manager.AcquireLease("test", tcs.Token);
        Assert.True(lease.Acquired);

        await tcs.CancelAsync();

        var token = lease.Inner.LeaseToken;

        // forward time
        TimeProvider.SetUtcNow(lease.Inner.CurrentExpiry - TimeSpan.FromSeconds(1));

        // wait for tick task
        await lease.Inner.TickTask;

        lease.Inner.LeaseToken.Should().Be(token);

        // forward time
        TimeProvider.Advance(OwnedLease.LeaseRenewalInterval - TimeSpan.FromSeconds(1));

        // check that the lease did expire
        var result = await Provider.TryAcquireLease(lease.LeaseId, TimeSpan.FromMinutes(1));
        result.IsLeaseAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task Loosing_The_Lease_Triggers_The_Token()
    {
        await using var lease = await Manager.AcquireLease("test");
        Assert.True(lease.Acquired);

        // forcefully acquire the lease
        await using var cmd = GetRequiredService<NpgsqlDataSource>().CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            UPDATE register.lease
            SET token = @token, expires = @expires
            WHERE id = @id
            """;

        cmd.Parameters.Add<Guid>("token", NpgsqlDbType.Uuid).TypedValue = Guid.NewGuid();
        cmd.Parameters.Add<DateTimeOffset>("expires", NpgsqlDbType.TimestampTz).TypedValue = TimeProvider.GetUtcNow() + TimeSpan.FromHours(1);
        cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = lease.LeaseId;
        await cmd.ExecuteNonQueryAsync();

        // forward time
        TimeProvider.SetUtcNow(lease.Inner.CurrentExpiry - TimeSpan.FromSeconds(1));

        // wait for tick task
        await lease.Inner.TickTask;

        lease.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task CanFilterLeases()
    {
        DateTimeOffset dt1 = TimeProvider.GetUtcNow();
        {
            // lease does not initially exist, so it should be acquired
            await using var lease = await GetLease();
            lease.Acquired.Should().BeTrue();
            lease.LastReleasedAt.Should().BeNull();
        }

        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        {
            // lease was just released, so it should not be acquired
            await using var lease = await GetLease();
            lease.Acquired.Should().BeFalse();
            lease.LastReleasedAt.Should().Be(dt1);
        }

        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        {
            // lease was just released, so it should not be acquired
            await using var lease = await GetLease();
            lease.Acquired.Should().BeFalse();
            lease.LastReleasedAt.Should().Be(dt1);
        }

        TimeProvider.Advance(TimeSpan.FromMinutes(15));
        DateTimeOffset dt2 = TimeProvider.GetUtcNow();
        {
            // lease was released more than 15 minutes ago, so it should be acquired
            await using var lease = await GetLease();
            lease.Acquired.Should().BeTrue();
            lease.LastReleasedAt.Should().Be(dt1);
        }

        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        {
            // lease was just released, so it should not be acquired
            await using var lease = await GetLease();
            lease.Acquired.Should().BeFalse();
            lease.LastReleasedAt.Should().Be(dt2);
        }

        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        {
            // lease was just released, so it should not be acquired
            await using var lease = await GetLease();
            lease.Acquired.Should().BeFalse();
            lease.LastReleasedAt.Should().Be(dt2);
        }

        async Task<Lease> GetLease()
        {
            var now = TimeProvider.GetUtcNow();
            return await Manager.AcquireLease(
                "test",
                info =>
                {
                    // Only run cleanup if the last time it was run was more than 15 minutes ago.
                    if (info.LastReleasedAt is null)
                    {
                        return true;
                    }

                    if (now - info.LastReleasedAt.Value < TimeSpan.FromMinutes(15))
                    {
                        return false;
                    }

                    return true;
                });
        }
    }
}
