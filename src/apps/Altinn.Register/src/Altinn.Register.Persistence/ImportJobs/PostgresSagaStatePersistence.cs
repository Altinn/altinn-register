using System.Buffers;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.UnitOfWork;
using CommunityToolkit.Diagnostics;
using Nerdbank.Streams;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence.ImportJobs;

/// <summary>
/// Postgres backed implementation of <see cref="ISagaStatePersistence"/>.
/// </summary>
public sealed class PostgresSagaStatePersistence
    : ISagaStatePersistence
{
    private static readonly JsonSerializerOptions _options = JsonSerializerOptions.Web;

    private static readonly JsonWriterOptions _writerOptions = new JsonWriterOptions
    {
        Encoder = _options.Encoder,
        Indented = _options.WriteIndented,
        IndentCharacter = _options.IndentCharacter,
        IndentSize = _options.IndentSize,
        MaxDepth = _options.MaxDepth,
        NewLine = _options.NewLine,
    };

    private static readonly JsonReaderOptions _readerOptions = new JsonReaderOptions
    {
        AllowTrailingCommas = _options.AllowTrailingCommas,
        CommentHandling = _options.ReadCommentHandling,
        MaxDepth = _options.MaxDepth,
    };

    private readonly IUnitOfWorkHandle _handle;
    private readonly NpgsqlConnection _connection;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresSagaStatePersistence"/> class.
    /// </summary>
    public PostgresSagaStatePersistence(
        IUnitOfWorkHandle handle,
        NpgsqlConnection connection,
        TimeProvider timeProvider)
    {
        _handle = handle;
        _connection = connection;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<SagaState<T>> GetState<T>(Guid sagaId, CancellationToken cancellationToken = default)
        where T : class, ISagaStateData<T>
    {
        const string QUERY =
            /*strpsql*/"""
            SELECT s.*
            FROM  register.saga_state s
            WHERE s.id = @id
            FOR NO KEY UPDATE
            """;

        _handle.ThrowIfCompleted();

        await using var cmd = _connection.CreateCommand(QUERY);

        cmd.Parameters.Add<Guid>("id", NpgsqlDbType.Uuid).TypedValue = sagaId;

        await cmd.PrepareAsync(cancellationToken);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new(sagaId, SagaStatus.InProgress, null, []);
        }

        var status = await reader.GetFieldValueAsync<SagaStatus>("status", cancellationToken);
        var type = await reader.GetFieldValueAsync<string>("state_type", cancellationToken);
        var messages = await reader.GetFieldValueAsync<Guid[]>("message_ids", cancellationToken);
        await using var stream = await reader.GetFieldValueAsync<Stream>("state_value", cancellationToken);

        using var seq = new Sequence<byte>(arrayPool: ArrayPool<byte>.Shared);
        await stream.CopyToAsync(seq.AsStream(), cancellationToken);

        return ReadFrom<T>(seq.AsReadOnlySequence, type) switch
        {
            null => throw new InvalidOperationException($"State in database is not deserializable to saga state of type '{typeof(T)}'"),
            T state => new(sagaId, status, state, messages),
        };
    }

    /// <inheritdoc/>
    public async Task SaveState<T>(SagaState<T> state, CancellationToken cancellationToken = default)
        where T : class, ISagaStateData<T>
    {
        const string QUERY =
            /*strpsql*/"""
            INSERT INTO register.saga_state ("id", "status", message_ids, state_type, state_value, created, updated)
            VALUES (@id, @status, @messageIds, @stateType, @stateValue, @now, @now)
            ON CONFLICT ("id")
            DO UPDATE
                 SET "status"    = EXCLUDED."status"
                   , message_ids = EXCLUDED.message_ids
                   , state_type  = EXCLUDED.state_type
                   , state_value = EXCLUDED.state_value
                   , updated     = EXCLUDED.updated
            """;

        _handle.ThrowIfCompleted();

        if (state.Data is null)
        {
            throw new ArgumentException($"{nameof(SagaState<>.Data)} cannot be null", nameof(state));
        }

        await using var cmd = _connection.CreateCommand(QUERY);

        cmd.Parameters.Add<DateTimeOffset>("now", NpgsqlDbType.TimestampTz).TypedValue = _timeProvider.GetUtcNow();
        cmd.Parameters.Add<Guid>("id", NpgsqlDbType.Uuid).TypedValue = state.SagaId;
        cmd.Parameters.Add<SagaStatus>("status").TypedValue = state.Status;
        cmd.Parameters.Add<string>("stateType", NpgsqlDbType.Text).TypedValue = T.StateType;
        cmd.Parameters.Add<List<Guid>>("messageIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid).TypedValue = [.. state.Messages];
        var stateValueParam = cmd.Parameters.Add<Stream>("stateValue", NpgsqlDbType.Jsonb);

        await cmd.PrepareAsync(cancellationToken);

        using var seq = new Sequence<byte>(arrayPool: ArrayPool<byte>.Shared);
        WriteTo(seq, state.Data);

        stateValueParam.TypedValue = seq.AsReadOnlySequence.AsStream();
        var updated = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (updated == 0)
        {
            throw new UnreachableException($"No records were updated - this should not happen. Saga id = '{state.SagaId}'");
        }
    }

    /// <inheritdoc/>
    public async Task DeleteState(Guid sagaId, CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            DELETE FROM register.saga_state
             WHERE id = @id
            """;

        _handle.ThrowIfCompleted();

        await using var cmd = _connection.CreateCommand(QUERY);

        cmd.Parameters.Add<Guid>("id", NpgsqlDbType.Uuid).TypedValue = sagaId;

        await cmd.PrepareAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void WriteTo<T>(Sequence<byte> seq, T state)
        where T : class, ISagaStateData<T>
    {
        using var writer = new Utf8JsonWriter(seq, _writerOptions);
        JsonSerializer.Serialize(writer, state, _options);
        writer.Flush();
    }

    private static T? ReadFrom<T>(ReadOnlySequence<byte> seq, string stateType)
        where T : class, ISagaStateData<T>
    {
        if (seq.Length <= 0)
        {
            return default;
        }

        var versionByte = seq.First.Span[0];
        seq = seq.Slice(1);

        return versionByte switch
        {
            1 => ReadFromV1<T>(seq, stateType),
            _ => ThrowHelper.ThrowNotSupportedException<T?>($"Unsupported JSONB version byte received: {versionByte}"),
        };
    }

    private static T? ReadFromV1<T>(ReadOnlySequence<byte> seq, string stateType)
        where T : ISagaStateData<T>
    {
        var reader = new Utf8JsonReader(seq, _readerOptions);

        return T.Read(ref reader, stateType, _options);
    }
}
