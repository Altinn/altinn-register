using System.Runtime.CompilerServices;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Persistence.AsyncEnumerables;
using CommunityToolkit.Diagnostics;
using Npgsql;

namespace Altinn.Register.Persistence;

/// <summary>
/// Implementation of <see cref="IV1PartyService"/> backed by a PostgreSQL database.
/// </summary>
internal partial class PostgreSqlPartyPersistence
    : IPartyPersistence
{
    private readonly NpgsqlConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlPartyPersistence"/> class.
    /// </summary>
    public PostgreSqlPartyPersistence(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<PartyRecord> GetPartyById(
        Guid partyUuid, 
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default)
    {
        var query = PartyQuery.Get(include, PartyFilter.PartyUuid);

        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            cmd.Parameters.Add<Guid>(query.ParameterName).TypedValue = partyUuid;

            return PrepareAndReadPartiesAsync(cmd, query, cancellationToken);
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<PartyRecord>(e).Using(cmd);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<PartyRecord> GetPartyById(
        int partyId,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default)
    {
        var query = PartyQuery.Get(include, PartyFilter.PartyId);

        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            cmd.Parameters.Add<int>(query.ParameterName).TypedValue = partyId;

            return PrepareAndReadPartiesAsync(cmd, query, cancellationToken);
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<PartyRecord>(e).Using(cmd);
        }
    }

    private async IAsyncEnumerable<PartyRecord> PrepareAndReadPartiesAsync(
        NpgsqlCommand inCmd,
        PartyQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Guard.IsNotNull(inCmd);
        Guard.IsNotNull(query);

        await using var cmd = inCmd;

        await cmd.PrepareAsync(cancellationToken);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var includeSubunits = query.HasSubUnits;
        Guid lastParent = default;
        while (await reader.ReadAsync(cancellationToken))
        {
            var parentUuid = query.ReadParentUuid(reader);
            if (parentUuid != lastParent)
            {
                lastParent = parentUuid;
                var parent = query.ReadParentParty(reader);
                yield return parent;
            }

            if (includeSubunits)
            {
                var childUuid = query.ReadChildUuid(reader);
                if (childUuid.HasValue)
                {
                    var child = query.ReadChildParty(reader, parentUuid);
                    yield return child;
                }
            }
        }
    }

    private enum PartyFilter
        : byte
    {
        PartyId, // single
        PartyUuid, // single
        
        Multiple = 1 << 7,
    }
}
