using System.Data;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ServiceDefaults.Leases;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Persistence.Leases;
using Altinn.Register.TestUtils;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;

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
    public async Task Acquire_New()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);

        var result = await Provider.TryAcquireLease(id, duration);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeTrue();
            result.Lease.Should().NotBeNull();
            result.Lease.LeaseId.Should().Be(id);
            result.Lease.Expires.Should().Be(now + duration);
            result.Lease.Token.Should().NotBeEmpty();
            result.Expires.Should().Be(now + duration);
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: result.Lease.Token,
            expectedExpires: now + duration,
            expectedAcquired: now,
            expectedReleased: null);
    }

    [Fact]
    public async Task Acquire_New_WithCondition()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);
        var ifNotAcquiredFor = TimeSpan.FromMinutes(5);

        var result = await Provider.TryAcquireLease(id, duration, ifNotAcquiredFor);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeTrue();
            result.Lease.Should().NotBeNull();
            result.Lease.LeaseId.Should().Be(id);
            result.Lease.Expires.Should().Be(now + duration);
            result.Lease.Token.Should().NotBeEmpty();
            result.Expires.Should().Be(now + duration);
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: result.Lease.Token,
            expectedExpires: now + duration,
            expectedAcquired: now,
            expectedReleased: null);
    }

    [Fact]
    public async Task Acquire_Expired()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);

        await WriteLease(id);

        var result = await Provider.TryAcquireLease(id, duration);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeTrue();
            result.Lease.Should().NotBeNull();
            result.Lease.LeaseId.Should().Be(id);
            result.Lease.Expires.Should().Be(now + duration);
            result.Lease.Token.Should().NotBeEmpty();
            result.Expires.Should().Be(now + duration);
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: result.Lease.Token,
            expectedExpires: now + duration,
            expectedAcquired: now,
            expectedReleased: null);
    }

    [Fact]
    public async Task Acquire_Expired_WithUnmetCondition()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);
        var ifNotAcquiredFor = TimeSpan.FromMinutes(5);

        var oldToken = Guid.NewGuid();
        await WriteLease(id, token: oldToken, acquired: now, released: now);

        var result = await Provider.TryAcquireLease(id, duration, ifNotAcquiredFor);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeFalse();
            result.Lease.Should().BeNull();
            result.Expires.Should().Be(DateTimeOffset.MinValue);
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().Be(now);
        }

        await CheckDatabase(
            id,
            expectedToken: oldToken,
            expectedExpires: DateTimeOffset.MinValue,
            expectedAcquired: now,
            expectedReleased: now);
    }

    [Fact]
    public async Task Acquire_Expired_WithMetCondition()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var then = now - TimeSpan.FromMinutes(10);
        var duration = TimeSpan.FromMinutes(1);
        var ifNotAcquiredFor = TimeSpan.FromMinutes(5);

        await WriteLease(id, acquired: then, released: then);

        var result = await Provider.TryAcquireLease(id, duration, ifNotAcquiredFor);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeTrue();
            result.Lease.Should().NotBeNull();
            result.Lease.LeaseId.Should().Be(id);
            result.Lease.Expires.Should().Be(now + duration);
            result.Lease.Token.Should().NotBeEmpty();
            result.Expires.Should().Be(now + duration);
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: result.Lease.Token,
            expectedExpires: now + duration,
            expectedAcquired: now,
            expectedReleased: null);
    }

    [Fact]
    public async Task Acquire_Held()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);

        var oldToken = Guid.NewGuid();
        await WriteLease(id, token: oldToken, expires: now + TimeSpan.FromMinutes(5), acquired: now, released: FieldValue.Null);

        var result = await Provider.TryAcquireLease(id, duration);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeFalse();
            result.Lease.Should().BeNull();
            result.Expires.Should().Be(now + TimeSpan.FromMinutes(5));
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: oldToken,
            expectedExpires: now + TimeSpan.FromMinutes(5),
            expectedAcquired: now,
            expectedReleased: null);
    }

    [Fact]
    public async Task Renew_New()
    {
        var id = "test";
        var duration = TimeSpan.FromMinutes(1);
        var ticket = new LeaseTicket(id, Guid.NewGuid(), TimeProvider.GetUtcNow() + duration);

        var result = await Provider.TryRenewLease(ticket, duration);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeFalse();
            result.Expires.Should().Be(DateTimeOffset.MinValue);
            result.LastAcquiredAt.Should().BeNull();
            result.LastReleasedAt.Should().BeNull();
        }

        // check that db is stil empty
        await using var uow = await GetRequiredService<IUnitOfWorkManager>().CreateAsync();
        var conn = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = conn.CreateCommand(
            /*strpsql*/"""
            SELECT id, token, expires, acquired, released
            FROM register.lease
            WHERE id = @id
            """);

        cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = id;
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.SingleResult);
        var read = await reader.ReadAsync();

        read.Should().BeFalse();
    }

    [Fact]
    public async Task Renew_WrongToken()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);
        var ticket = new LeaseTicket(id, Guid.NewGuid(), now + duration);

        var oldToken = Guid.NewGuid();
        await WriteLease(id, token: oldToken, expires: now + duration, acquired: now, released: FieldValue.Null);

        var result = await Provider.TryRenewLease(ticket, duration);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeFalse();
            result.Expires.Should().Be(now + duration);
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: oldToken,
            expectedExpires: now + duration,
            expectedAcquired: now,
            expectedReleased: null);
    }

    [Fact]
    public async Task Renew_Expired()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);
        var ticket = new LeaseTicket(id, Guid.NewGuid(), now + duration);

        await WriteLease(id, token: ticket.Token, expires: now - duration, acquired: now - (duration * 2), released: FieldValue.Null);

        var result = await Provider.TryRenewLease(ticket, duration);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeFalse();
            result.Expires.Should().Be(now - duration);
            result.LastAcquiredAt.Should().Be(now - (duration * 2));
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: ticket.Token,
            expectedExpires: now - duration,
            expectedAcquired: now - (duration * 2),
            expectedReleased: null);
    }

    [Fact]
    public async Task Renew_Released()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);
        var ticket = new LeaseTicket(id, Guid.NewGuid(), now + duration);

        var oldToken = Guid.NewGuid();
        await WriteLease(id, token: oldToken, expires: DateTimeOffset.MinValue, acquired: now, released: now);

        var result = await Provider.TryRenewLease(ticket, duration);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeFalse();
            result.Expires.Should().Be(DateTimeOffset.MinValue);
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: oldToken,
            expectedExpires: DateTimeOffset.MinValue,
            expectedAcquired: now,
            expectedReleased: now);
    }

    [Fact]
    public async Task Renew_Active()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);
        var ticket = new LeaseTicket(id, Guid.NewGuid(), now + duration);

        await WriteLease(id, token: ticket.Token, expires: ticket.Expires, acquired: now, released: FieldValue.Null);

        var result = await Provider.TryRenewLease(ticket, duration * 2);

        using (new AssertionScope())
        {
            result.IsLeaseAcquired.Should().BeTrue();
            result.Lease.Should().NotBeNull();
            result.Lease.LeaseId.Should().Be(id);
            result.Lease.Expires.Should().Be(now + (duration * 2));
            result.Lease.Token.Should().NotBeEmpty();
            result.Expires.Should().Be(now + (duration * 2));
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: result.Lease.Token,
            expectedExpires: now + (duration * 2),
            expectedAcquired: now,
            expectedReleased: null);
    }

    [Fact]
    public async Task Release_New()
    {
        var id = "test";
        var duration = TimeSpan.FromMinutes(1);
        var ticket = new LeaseTicket(id, Guid.NewGuid(), TimeProvider.GetUtcNow() + duration);

        var result = await Provider.ReleaseLease(ticket);

        using (new AssertionScope())
        {
            result.IsReleased.Should().BeFalse();
            result.Expires.Should().Be(DateTimeOffset.MinValue);
            result.LastAcquiredAt.Should().BeNull();
            result.LastReleasedAt.Should().BeNull();
        }

        // check that db is stil empty
        await using var uow = await GetRequiredService<IUnitOfWorkManager>().CreateAsync();
        var conn = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = conn.CreateCommand(
            /*strpsql*/"""
            SELECT id, token, expires, acquired, released
            FROM register.lease
            WHERE id = @id
            """);

        cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = id;
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.SingleResult);
        var read = await reader.ReadAsync();

        read.Should().BeFalse();
    }

    [Fact]
    public async Task Release_WrongToken()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);
        var ticket = new LeaseTicket(id, Guid.NewGuid(), now + duration);

        var oldToken = Guid.NewGuid();
        await WriteLease(id, token: oldToken, expires: now + duration, acquired: now, released: FieldValue.Null);

        var result = await Provider.ReleaseLease(ticket);

        using (new AssertionScope())
        {
            result.IsReleased.Should().BeFalse();
            result.Expires.Should().Be(now + duration);
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: oldToken,
            expectedExpires: now + duration,
            expectedAcquired: now,
            expectedReleased: null);
    }

    [Fact]
    public async Task Release_Expired()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);
        var ticket = new LeaseTicket(id, Guid.NewGuid(), now + duration);

        await WriteLease(id, token: ticket.Token, expires: now - duration, acquired: now - (duration * 2), released: FieldValue.Null);

        var result = await Provider.ReleaseLease(ticket);

        using (new AssertionScope())
        {
            result.IsReleased.Should().BeFalse();
            result.Expires.Should().Be(now - duration);
            result.LastAcquiredAt.Should().Be(now - (duration * 2));
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: ticket.Token,
            expectedExpires: now - duration,
            expectedAcquired: now - (duration * 2),
            expectedReleased: null);
    }

    [Fact]
    public async Task Release_Released()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);
        var ticket = new LeaseTicket(id, Guid.NewGuid(), now + duration);

        var oldToken = Guid.NewGuid();
        await WriteLease(id, token: oldToken, expires: DateTimeOffset.MinValue, acquired: now, released: now);

        var result = await Provider.ReleaseLease(ticket);

        using (new AssertionScope())
        {
            result.IsReleased.Should().BeFalse();
            result.Expires.Should().Be(DateTimeOffset.MinValue);
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().BeNull();
        }

        await CheckDatabase(
            id,
            expectedToken: oldToken,
            expectedExpires: DateTimeOffset.MinValue,
            expectedAcquired: now,
            expectedReleased: now);
    }

    [Fact]
    public async Task Release_Active()
    {
        var id = "test";
        var now = TimeProvider.GetUtcNow();
        var duration = TimeSpan.FromMinutes(1);
        var ticket = new LeaseTicket(id, Guid.NewGuid(), now + duration);

        await WriteLease(id, token: ticket.Token, expires: ticket.Expires, acquired: now, released: FieldValue.Null);

        var result = await Provider.ReleaseLease(ticket);

        using (new AssertionScope())
        {
            result.IsReleased.Should().BeTrue();
            result.Expires.Should().Be(DateTimeOffset.MinValue);
            result.LastAcquiredAt.Should().Be(now);
            result.LastReleasedAt.Should().Be(now);
        }

        await CheckDatabase(
            id,
            expectedToken: null,
            expectedExpires: DateTimeOffset.MinValue,
            expectedAcquired: now,
            expectedReleased: now);
    }

    private async Task WriteLease(
        string id,
        FieldValue<Guid> token = default,
        FieldValue<DateTimeOffset> expires = default,
        FieldValue<DateTimeOffset> acquired = default,
        FieldValue<DateTimeOffset> released = default)
    {
        await using var uow = await GetRequiredService<IUnitOfWorkManager>().CreateAsync();
        var conn = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = conn.CreateCommand(
            /*strpsql*/"""
            INSERT INTO register.lease (id, token, expires, acquired, released)
            VALUES (@id, @token, @expires, @acquired, @released)
            ON CONFLICT (id) DO UPDATE SET
                token = EXCLUDED.token,
                expires = EXCLUDED.expires,
                acquired = EXCLUDED.acquired,
                released = EXCLUDED.released
            """);

        var now = TimeProvider.GetUtcNow();
        cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = id;
        cmd.Parameters.Add<Guid>("token", NpgsqlDbType.Uuid).TypedValue = token.OrDefault(Guid.NewGuid());
        cmd.Parameters.Add<DateTimeOffset>("expires", NpgsqlDbType.TimestampTz).TypedValue = expires.OrDefault(DateTimeOffset.MinValue);
        cmd.Parameters.Add<DateTimeOffset?>("acquired", NpgsqlDbType.TimestampTz).TypedValue = acquired switch { { HasValue: true, Value: var value } => value, { IsNull: true } => null, _ => now };
        cmd.Parameters.Add<DateTimeOffset?>("released", NpgsqlDbType.TimestampTz).TypedValue = released switch { { HasValue: true, Value: var value } => value, { IsNull: true } => null, _ => now };

        await cmd.PrepareAsync();
        await cmd.ExecuteNonQueryAsync();
        await uow.CommitAsync();
    }

    private async Task CheckDatabase(
        string id,
        Guid? expectedToken,
        DateTimeOffset expectedExpires,
        DateTimeOffset? expectedAcquired,
        DateTimeOffset? expectedReleased)
    {
        await using var uow = await GetRequiredService<IUnitOfWorkManager>().CreateAsync();
        var conn = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = conn.CreateCommand(
            /*strpsql*/"""
            SELECT id, token, expires, acquired, released
            FROM register.lease
            WHERE id = @id
            """);

        cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = id;
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.SingleResult);
        var read = await reader.ReadAsync();
        
        using var scope = new AssertionScope();
        read.Should().BeTrue();

        (await reader.GetFieldValueAsync<string>("id")).Should().Be(id);
        (await reader.GetFieldValueAsync<DateTimeOffset>("expires")).Should().Be(expectedExpires);
        (await reader.GetFieldValueAsync<DateTimeOffset?>("acquired")).Should().Be(expectedAcquired);
        (await reader.GetFieldValueAsync<DateTimeOffset?>("released")).Should().Be(expectedReleased);

        if (expectedToken.HasValue)
        {
            (await reader.GetFieldValueAsync<Guid>("token")).Should().Be(expectedToken.Value);
        }
    }
}
