using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Core.Utils;
using Altinn.Register.Persistence.AsyncEnumerables;
using Altinn.Register.Persistence.DbArgTypes;
using Altinn.Register.Persistence.UnitOfWork;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence;

/// <summary>
/// Implementation of <see cref="IV1PartyService"/> backed by a PostgreSQL database.
/// </summary>
internal partial class PostgreSqlPartyPersistence
    : IPartyPersistence
    , IPartyExternalRolePersistence
{
    private readonly IUnitOfWorkHandle _handle;
    private readonly NpgsqlConnection _connection;
    private readonly SavePointManager _savePointManager;
    private readonly ILogger<PostgreSqlPartyPersistence> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlPartyPersistence"/> class.
    /// </summary>
    public PostgreSqlPartyPersistence(
        IUnitOfWorkHandle handle,
        NpgsqlConnection connection,
        SavePointManager savePointManager,
        ILogger<PostgreSqlPartyPersistence> logger)
    {
        _handle = handle;
        _connection = connection;
        _savePointManager = savePointManager;
        _logger = logger;
    }

    #region Party

    /// <inheritdoc/>
    public IAsyncEnumerable<PartyRecord> GetPartyById(
        Guid partyUuid, 
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default)
    {
        _handle.ThrowIfCompleted();

        var query = PartyQuery.Get(include, PartyFilters.PartyUuid);

        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            query.AddPartyUuidParameter(cmd, partyUuid);

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
        _handle.ThrowIfCompleted();

        var query = PartyQuery.Get(include, PartyFilters.PartyId);

        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            query.AddPartyIdParameter(cmd, partyId);

            return PrepareAndReadPartiesAsync(cmd, query, cancellationToken);
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<PartyRecord>(e).Using(cmd);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<OrganizationRecord> GetOrganizationByIdentifier(
        OrganizationIdentifier identifier,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default)
    {
        _handle.ThrowIfCompleted();

        // filter out person fields as result is guaranteed to be an organization
        include &= ~PartyFieldIncludes.Person;

        var query = PartyQuery.Get(include, PartyFilters.OrganizationIdentifier);
        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            query.AddOrganizationIdentifierParameter(cmd, identifier.ToString());

            return PrepareAndReadPartiesAsync(cmd, query, cancellationToken).Cast<OrganizationRecord>();
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<OrganizationRecord>(e).Using(cmd);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<PersonRecord> GetPartyByPersonIdentifier(
        PersonIdentifier identifier,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default)
    {
        _handle.ThrowIfCompleted();

        // filter out organization fields as result is guaranteed to be a person
        include &= ~(PartyFieldIncludes.Organization & PartyFieldIncludes.SubUnits);

        var query = PartyQuery.Get(include, PartyFilters.PersonIdentifier);
        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            query.AddPersonIdentifierParameter(cmd, identifier.ToString());

            return PrepareAndReadPartiesAsync(cmd, query, cancellationToken).Cast<PersonRecord>();
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<PersonRecord>(e).Using(cmd);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<PartyRecord> LookupParties(
        IReadOnlyList<Guid>? partyUuids = null,
        IReadOnlyList<int>? partyIds = null,
        IReadOnlyList<OrganizationIdentifier>? organizationIdentifiers = null,
        IReadOnlyList<PersonIdentifier>? personIdentifiers = null,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default)
    {
        _handle.ThrowIfCompleted();

        bool any = false, orgs = false, persons = false;
        PartyFilters filters = PartyFilters.Multiple;
        
        if (partyUuids is { Count: > 0 })
        {
            any = orgs = persons = true;
            filters |= PartyFilters.PartyUuid;
        }

        if (partyIds is { Count: > 0 })
        {
            any = orgs = persons = true;
            filters |= PartyFilters.PartyId;
        }

        if (organizationIdentifiers is { Count: > 0 })
        {
            any = orgs = true;
            filters |= PartyFilters.OrganizationIdentifier;
        }

        if (personIdentifiers is { Count: > 0 })
        {
            any = persons = true;
            filters |= PartyFilters.PersonIdentifier;
        }

        if (!any)
        {
            return AsyncEnumerable.Empty<PartyRecord>();
        }

        if (!orgs)
        {
            // filter out organization fields as result is guaranteed to not be organizations
            include &= ~(PartyFieldIncludes.Organization & PartyFieldIncludes.SubUnits);
        }

        if (!persons)
        {
            // filter out person fields as result is guaranteed to not be persons
            include &= ~PartyFieldIncludes.Person;
        }

        var query = PartyQuery.Get(include, filters);
        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            if (partyUuids is { Count: > 0 })
            {
                query.AddPartyUuidListParameter(cmd, partyUuids.ToList());
            }
            
            if (partyIds is { Count: > 0 })
            {
                query.AddPartyIdListParameter(cmd, partyIds.ToList());
            }

            if (organizationIdentifiers is { Count: > 0 })
            {
                query.AddOrganizationIdentifierListParameter(cmd, organizationIdentifiers.Select(static o => o.ToString()).ToList());
            }

            if (personIdentifiers is { Count: > 0 })
            {
                query.AddPersonIdentifierListParameter(cmd, personIdentifiers.Select(static p => p.ToString()).ToList());
            }

            return PrepareAndReadPartiesAsync(cmd, query, cancellationToken);
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<PartyRecord>(e).Using(cmd);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<PartyRecord> GetPartyStream(
        ulong fromExclusive,
        ushort limit,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default)
    {
        _handle.ThrowIfCompleted();
        Guard.IsFalse(include.HasFlag(PartyFieldIncludes.SubUnits), nameof(include), $"{nameof(PartyFieldIncludes)}.{nameof(PartyFieldIncludes.SubUnits)} is not allowed");

        var query = PartyQuery.Get(include, PartyFilters.StreamPage);
        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            query.AddStreamPageParameters(cmd, fromExclusive, limit);

            return PrepareAndReadPartiesAsync(cmd, query, cancellationToken);
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<PartyRecord>(e).Using(cmd);
        }
    }

    /// <inheritdoc/>
    public async Task<ulong> GetMaxPartyVersionId(CancellationToken cancellationToken)
    {
        const string QUERY =
            /*strpsql*/"""
            SELECT MAX(version_id) FROM register.party
            """;

        _handle.ThrowIfCompleted();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = QUERY;

        await cmd.PrepareAsync(cancellationToken);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            ThrowHelper.ThrowInvalidOperationException("No rows returned from MAX(version_id) query");
        }

        if (await reader.IsDBNullAsync(0, cancellationToken))
        {
            return 0;
        }

        return (ulong)await reader.GetFieldValueAsync<long>(0, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Result<PartyRecord>> UpsertParty(
        PartyRecord party,
        CancellationToken cancellationToken = default)
    {
        _handle.ThrowIfCompleted();

        return UpsertPartyQuery.UpsertParty(_connection, party, cancellationToken);
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
            var parentUuid = await query.ReadParentUuid(reader, cancellationToken);
            if (parentUuid != lastParent)
            {
                lastParent = parentUuid;
                var parent = await query.ReadParentParty(reader, cancellationToken);
                yield return parent;
            }

            if (includeSubunits)
            {
                var childUuid = await query.ReadChildUuid(reader, cancellationToken);
                if (childUuid.HasValue)
                {
                    var child = await query.ReadChildParty(reader, parentUuid, cancellationToken);
                    yield return child;
                }
            }
        }
    }

    [Flags]
    private enum PartyFilters
        : byte
    {
        None = 0,
        PartyId = 1 << 0,
        PartyUuid = 1 << 1,
        PersonIdentifier = 1 << 2,
        OrganizationIdentifier = 1 << 3,
        StreamPage = 1 << 4,

        Multiple = 1 << 7,
    }

    #endregion

    #region Roles

    /// <inheritdoc/>
    IAsyncEnumerable<PartyExternalRoleAssignmentRecord> IPartyExternalRolePersistence.GetExternalRoleAssignmentsFromParty(
        Guid partyUuid,
        PartyExternalRoleAssignmentFieldIncludes include,
        CancellationToken cancellationToken)
        => GetExternalRoleAssignmentsFromParty(partyUuid, null, include, cancellationToken);

    /// <inheritdoc/>
    IAsyncEnumerable<PartyExternalRoleAssignmentRecord> IPartyExternalRolePersistence.GetExternalRoleAssignmentsFromParty(
        Guid partyUuid,
        ExternalRoleReference role,
        PartyExternalRoleAssignmentFieldIncludes include,
        CancellationToken cancellationToken)
        => GetExternalRoleAssignmentsFromParty(partyUuid, role, include, cancellationToken);

    /// <inheritdoc cref="IPartyExternalRolePersistence.GetExternalRoleAssignmentsFromParty(Guid, ExternalRoleReference, PartyExternalRoleAssignmentFieldIncludes, CancellationToken)"/>
    public IAsyncEnumerable<PartyExternalRoleAssignmentRecord> GetExternalRoleAssignmentsFromParty(
        Guid partyUuid,
        ExternalRoleReference? role = null,
        PartyExternalRoleAssignmentFieldIncludes include = PartyExternalRoleAssignmentFieldIncludes.RoleAssignment,
        CancellationToken cancellationToken = default)
    {
        _handle.ThrowIfCompleted();

        var filter = PartyRoleFilters.FromParty;
        if (role is not null)
        {
            filter |= PartyRoleFilters.Role;
        }

        var query = PartyRoleQuery.Get(include, filter);
        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            query.AddFromPartyParameter(cmd, partyUuid);

            if (role is not null)
            {
                query.AddRoleSourceParameter(cmd, role.Source);
                query.AddRoleIdentifierParameter(cmd, role.Identifier);
            }

            return PrepareAndReadPartyRolesAsync(cmd, query, cancellationToken);
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<PartyExternalRoleAssignmentRecord>(e).Using(cmd);
        }
    }

    /// <inheritdoc/>
    IAsyncEnumerable<PartyExternalRoleAssignmentRecord> IPartyExternalRolePersistence.GetExternalRoleAssignmentsToParty(
        Guid partyUuid,
        PartyExternalRoleAssignmentFieldIncludes include,
        CancellationToken cancellationToken)
        => GetExternalRoleAssignmentsToParty(partyUuid, null, include, cancellationToken);

    /// <inheritdoc/>
    IAsyncEnumerable<PartyExternalRoleAssignmentRecord> IPartyExternalRolePersistence.GetExternalRoleAssignmentsToParty(
        Guid partyUuid,
        ExternalRoleReference role,
        PartyExternalRoleAssignmentFieldIncludes include,
        CancellationToken cancellationToken)
        => GetExternalRoleAssignmentsToParty(partyUuid, role, include, cancellationToken);

    /// <inheritdoc cref="IPartyExternalRolePersistence.GetExternalRoleAssignmentsToParty(Guid, ExternalRoleReference, PartyExternalRoleAssignmentFieldIncludes, CancellationToken)"/>
    public IAsyncEnumerable<PartyExternalRoleAssignmentRecord> GetExternalRoleAssignmentsToParty(
        Guid partyUuid,
        ExternalRoleReference? role = null,
        PartyExternalRoleAssignmentFieldIncludes include = PartyExternalRoleAssignmentFieldIncludes.RoleAssignment,
        CancellationToken cancellationToken = default)
    {
        _handle.ThrowIfCompleted();

        var filter = PartyRoleFilters.ToParty;
        if (role is not null)
        {
            filter |= PartyRoleFilters.Role;
        }

        var query = PartyRoleQuery.Get(include, filter);
        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            query.AddToPartyParameter(cmd, partyUuid);

            if (role is not null)
            {
                query.AddRoleSourceParameter(cmd, role.Source);
                query.AddRoleIdentifierParameter(cmd, role.Identifier);
            }

            return PrepareAndReadPartyRolesAsync(cmd, query, cancellationToken);
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<PartyExternalRoleAssignmentRecord>(e).Using(cmd);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ExternalRoleAssignmentEvent> GetExternalRoleAssignmentStream(
        ulong fromExclusive,
        ushort limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            SELECT 
                "id",
                "type",
                "source",
                "identifier",
                "from_party",
                "to_party"
            FROM register.external_role_assignment_event
            WHERE "id" > @from
              AND "id" <= register.tx_max_safeval('register.external_role_assignment_event_id_seq')
            ORDER BY "id"
            LIMIT @limit
            """;

        _handle.ThrowIfCompleted();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = QUERY;

        cmd.Parameters.Add<long>("from", NpgsqlDbType.Bigint).TypedValue = checked((long)fromExclusive);
        cmd.Parameters.Add<short>("limit", NpgsqlDbType.Smallint).TypedValue = checked((short)limit);

        await cmd.PrepareAsync(cancellationToken);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("id");
        var typeOrdinal = reader.GetOrdinal("type");
        var sourceOrdinal = reader.GetOrdinal("source");
        var identifierOrdinal = reader.GetOrdinal("identifier");
        var fromPartyOrdinal = reader.GetOrdinal("from_party");
        var toPartyOrdinal = reader.GetOrdinal("to_party");

        while (await reader.ReadAsync(cancellationToken))
        {
            var id = (ulong)await reader.GetFieldValueAsync<long>(idOrdinal, cancellationToken);
            var type = await reader.GetFieldValueAsync<ExternalRoleAssignmentEvent.EventType>(typeOrdinal, cancellationToken);
            var roleSource = await reader.GetFieldValueAsync<ExternalRoleSource>(sourceOrdinal, cancellationToken);
            var identifier = await reader.GetFieldValueAsync<string>(identifierOrdinal, cancellationToken);
            var toParty = await reader.GetFieldValueAsync<Guid>(toPartyOrdinal, cancellationToken);
            var fromParty = await reader.GetFieldValueAsync<Guid>(fromPartyOrdinal, cancellationToken);

            yield return new ExternalRoleAssignmentEvent
            {
                VersionId = id,
                Type = type,
                RoleSource = roleSource,
                RoleIdentifier = identifier,
                ToParty = toParty,
                FromParty = fromParty,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ulong> GetMaxExternalRoleAssignmentVersionId(CancellationToken cancellationToken)
    {
        const string QUERY =
            /*strpsql*/"""
            SELECT MAX(id) FROM register.external_role_assignment_event
            """;

        _handle.ThrowIfCompleted();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = QUERY;

        await cmd.PrepareAsync(cancellationToken);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            ThrowHelper.ThrowInvalidOperationException("No rows returned from MAX(id) query");
        }

        if (await reader.IsDBNullAsync(0, cancellationToken))
        {
            return 0;
        }

        return (ulong)await reader.GetFieldValueAsync<long>(0, cancellationToken);
    }

    private async IAsyncEnumerable<PartyExternalRoleAssignmentRecord> PrepareAndReadPartyRolesAsync(
        NpgsqlCommand inCmd,
        PartyRoleQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Guard.IsNotNull(inCmd);
        Guard.IsNotNull(query);

        await using var cmd = inCmd;

        await cmd.PrepareAsync(cancellationToken);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var role = await query.ReadRole(reader, cancellationToken);
            yield return role;
        }
    }

    /// <inheritdoc/>
    public IAsyncSideEffectEnumerable<ExternalRoleAssignmentEvent> UpsertExternalRolesFromPartyBySource(
        Guid commandId,
        Guid partyUuid,
        ExternalRoleSource roleSource,
        IEnumerable<IPartyExternalRolePersistence.UpsertExternalRoleAssignment> assignments,
        CancellationToken cancellationToken = default)
    {
        _handle.ThrowIfCompleted();
        Guard.IsNotNull(assignments);
        Guard.IsNotDefault(commandId);

        var assignmentList = assignments.Select(static a => new ArgUpsertExternalRoleAssignment
        {
            ToParty = a.ToParty,
            Identifier = a.RoleIdentifier,
        }).ToList();

        Log.UpsertExternalRolesFromPartyBySource(_logger, assignmentList.Count, roleSource);
        var enumerable = new UpsertExternalRolesFromPartyBySourceAsyncSideEffectEnumerable(_connection, commandId, partyUuid, roleSource, assignmentList, cancellationToken);
        return enumerable.WrapExceptions(ex => new UpsertExternalRolesFromPartyBySourceException(commandId, partyUuid, roleSource, assignments, ex), cancellationToken);
    }

    private sealed class UpsertExternalRolesFromPartyBySourceAsyncSideEffectEnumerable(
        NpgsqlConnection connection,
        Guid commandId,
        Guid partyUuid,
        ExternalRoleSource roleSource,
        List<ArgUpsertExternalRoleAssignment> assignments,
        CancellationToken cancellationToken = default)
        : NpgsqlAsyncSideEffectEnumerable<ExternalRoleAssignmentEvent>(connection, QUERY, cancellationToken)
    {
        private const string QUERY =
            /*strpsql*/"""
                SELECT 
                    "version_id",
                    "type",
                    "identifier",
                    "to_party"
                FROM register.upsert_external_role_assignments(
                    @from_party,
                    @source,
                    @cmd_id,
                    @assignments
                )
                """;

        /// <inheritdoc/>
        protected override void PrepareParameters(NpgsqlParameterCollection parameters)
        {
            parameters.Add<Guid>("from_party", NpgsqlDbType.Uuid).TypedValue = partyUuid;
            parameters.Add<ExternalRoleSource>("source").TypedValue = roleSource;
            parameters.Add<Guid>("cmd_id", NpgsqlDbType.Uuid).TypedValue = commandId;
            parameters.Add<List<ArgUpsertExternalRoleAssignment>>("assignments").TypedValue = assignments;
        }

        /// <inheritdoc/>
        protected override async IAsyncEnumerator<ExternalRoleAssignmentEvent> Enumerate(
            NpgsqlDataReader reader,
            CancellationToken cancellationToken)
        {
            var versionIdOrdinal = reader.GetOrdinal("version_id");
            var typeOrdinal = reader.GetOrdinal("type");
            var identifierOrdinal = reader.GetOrdinal("identifier");
            var toPartyOrdinal = reader.GetOrdinal("to_party");

            while (await reader.ReadAsync(cancellationToken))
            {
                var versionId = (ulong)await reader.GetFieldValueAsync<long>(versionIdOrdinal, cancellationToken);
                var type = await reader.GetFieldValueAsync<ExternalRoleAssignmentEvent.EventType>(typeOrdinal, cancellationToken);
                var identifier = await reader.GetFieldValueAsync<string>(identifierOrdinal, cancellationToken);
                var toParty = await reader.GetFieldValueAsync<Guid>(toPartyOrdinal, cancellationToken);

                var evt = new ExternalRoleAssignmentEvent
                {
                    VersionId = versionId,
                    Type = type,
                    RoleSource = roleSource,
                    RoleIdentifier = identifier,
                    ToParty = toParty,
                    FromParty = partyUuid,
                };

                yield return evt;
            }
        }
    }
    
    [Flags]
    private enum PartyRoleFilters
        : byte
    {
        None = 0,
        FromParty = 1 << 0,
        ToParty = 1 << 1,

        RoleSource = 1 << 2,
        Role = RoleSource | (1 << 3),
    }

    #endregion

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Debug, "Upserting {Count} external role-assignments from {Source}")]
        public static partial void UpsertExternalRolesFromPartyBySource(ILogger logger, int count, ExternalRoleSource source);
    }

    private sealed class UpsertExternalRolesFromPartyBySourceException
        : InvalidOperationException
    {
        public Guid CommandId { get; }

        public Guid FromParty { get; }

        public ExternalRoleSource RoleSource { get; }

        // Note: We're explicitly not keeping the inner exception here, because the resulting exception is too long and causes issues in logging
        public UpsertExternalRolesFromPartyBySourceException(
            Guid commandId,
            Guid fromParty,
            ExternalRoleSource source,
            IEnumerable<IPartyExternalRolePersistence.UpsertExternalRoleAssignment> assignments,
            Exception innerException)
            : base(CreateMessage(commandId, fromParty, source, assignments, innerException))
        {
            CommandId = commandId;
            FromParty = fromParty;
            RoleSource = source;
        }

        private static string CreateMessage(
            Guid commandId,
            Guid fromParty,
            ExternalRoleSource source,
            IEnumerable<IPartyExternalRolePersistence.UpsertExternalRoleAssignment> assignments,
            Exception innerException)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Failed to upsert external role-assignments from party '{fromParty}' for source '{source}';");
            sb.AppendLine($"Cause By: {innerException.Message}");
            sb.AppendLine($"CommandId: {{{commandId}}}");

            foreach (var assignment in assignments)
            {
                sb.AppendLine($"  {assignment.RoleIdentifier} -> {assignment.ToParty}");
            }

            return sb.ToString();
        }
    }
}
