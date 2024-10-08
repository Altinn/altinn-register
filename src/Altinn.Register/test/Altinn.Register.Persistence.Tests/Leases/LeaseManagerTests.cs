using Altinn.Register.Core.Leases;
using Altinn.Register.Persistence.Leases;
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

        _provider = GetRequiredService<PostgresqlLeaseProvider>();
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
        Assert.Null(lease);
    }

    [Fact]
    public async Task Returns_Lease_When_Acquired()
    {
        await using var lease = await Manager.AcquireLease("test");
        Assert.NotNull(lease);
    }

    [Fact]
    public async Task Releases_Lease_When_Disposed()
    {
        {
            await using var lease = await Manager.AcquireLease("test");
            Assert.NotNull(lease);
        }

        {
            await using var lease2 = await Manager.AcquireLease("test");
            Assert.NotNull(lease2);
        }
    }

    [Fact]
    public async Task Lease_AutoRenews()
    {
        await using var lease = await Manager.AcquireLease("test");
        Assert.NotNull(lease);

        for (var i = 0; i < 10; i++)
        {
            var token = lease.LeaseToken;

            // forward time
            TimeProvider.SetUtcNow(lease.CurrentExpiry - TimeSpan.FromSeconds(1));

            // wait for tick task
            await lease.TickTask;

            lease.LeaseToken.Should().NotBe(token);
        }
    }

    [Fact]
    public async Task Lease_Token_Is_Linked()
    {
        using var tcs = new CancellationTokenSource();
        await using var lease = await Manager.AcquireLease("test", tcs.Token);
        Assert.NotNull(lease);

        lease.Token.IsCancellationRequested.Should().BeFalse();

        tcs.Cancel();

        lease.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelling_Token_Stops_Renewal()
    {
        using var tcs = new CancellationTokenSource();
        await using var lease = await Manager.AcquireLease("test", tcs.Token);
        Assert.NotNull(lease);

        tcs.Cancel();

        var token = lease.LeaseToken;

        // forward time
        TimeProvider.SetUtcNow(lease.CurrentExpiry - TimeSpan.FromSeconds(1));

        // wait for tick task
        await lease.TickTask;

        lease.LeaseToken.Should().Be(token);

        // forward time
        TimeProvider.Advance(Lease.LeaseRenewalInterval - TimeSpan.FromSeconds(1));

        // check that the lease did expire
        var result = await Provider.TryAcquireLease(lease.LeaseId, TimeSpan.FromMinutes(1));
        result.IsLeaseAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task Loosing_The_Lease_Triggers_The_Token()
    {
        await using var lease = await Manager.AcquireLease("test");
        Assert.NotNull(lease);

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
        TimeProvider.SetUtcNow(lease.CurrentExpiry - TimeSpan.FromSeconds(1));

        // wait for tick task
        await lease.TickTask;

        lease.Token.IsCancellationRequested.Should().BeTrue();
    }
}
