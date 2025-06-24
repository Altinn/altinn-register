using System.Runtime.CompilerServices;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using CommunityToolkit.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence.ImportJobs;

/// <summary>
/// Postgres backed implementation of <see cref="IUserIdImportJobService"/>.
/// </summary>
internal class PostgresUserIdImportJobService
    : IUserIdImportJobService
{
    private readonly IUnitOfWorkHandle _handle;
    private readonly NpgsqlConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresUserIdImportJobService"/> class.
    /// </summary>
    public PostgresUserIdImportJobService(
        IUnitOfWorkHandle handle,
        NpgsqlConnection connection)
    {
        _handle = handle;
        _connection = connection;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<(Guid PartyUuid, PartyRecordType PartyType)> GetPartiesWithoutUserIdAndJobState(
        string jobId,
        IReadOnlySet<PartyRecordType> partyTypes,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            SELECT p."uuid", p."party_type"
            FROM register.party p 
            LEFT JOIN register."user" u 
               ON  u."uuid" = p."uuid" 
              AND u.is_active 
            LEFT JOIN register.import_job_party_state s
               ON  s.party_uuid = p."uuid"
              AND s.job_id = @jobId
            WHERE u."uuid" IS NULL
              AND s.party_uuid IS NULL
              AND p."party_type" = ANY(@partyTypes)
              AND (@from IS NULL OR p."uuid" > @from)
            ORDER BY p."uuid"
            LIMIT 10000
            """;

        Guard.IsNotNullOrEmpty(jobId);
        Guard.IsNotNull(partyTypes);
        Guard.IsNotEmpty(partyTypes);

        _handle.ThrowIfCompleted();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_handle.Token, cancellationToken);
        cancellationToken = cts.Token;

        await using var cmd = _connection.CreateCommand(QUERY);
        
        cmd.Parameters.Add<string>("jobId", NpgsqlDbType.Text).TypedValue = jobId;
        cmd.Parameters.Add<List<PartyRecordType>>("partyTypes").TypedValue = [.. partyTypes];

        var fromParam = cmd.Parameters.Add<Guid?>("from", NpgsqlDbType.Uuid);
        fromParam.TypedValue = null;

        await cmd.PrepareAsync(cancellationToken);

        while (true)
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var uuidOrdinal = reader.GetOrdinal("uuid");
            var partyTypeOrdinal = reader.GetOrdinal("party_type");

            if (!await reader.ReadAsync(cancellationToken))
            {
                // No more parties to read.
                break;
            }

            Guid partyUuid;
            PartyRecordType partyType;
            do
            {
                partyUuid = await reader.GetFieldValueAsync<Guid>(uuidOrdinal, cancellationToken);
                partyType = await reader.GetFieldValueAsync<PartyRecordType>(partyTypeOrdinal, cancellationToken);
                yield return (partyUuid, partyType);
            }
            while (await reader.ReadAsync(cancellationToken));

            fromParam.TypedValue = partyUuid;
        }
    }
}
