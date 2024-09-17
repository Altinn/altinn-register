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
    , IPartyRolePersistence
{
    private readonly NpgsqlConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlPartyPersistence"/> class.
    /// </summary>
    public PostgreSqlPartyPersistence(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    #region Party

    /// <inheritdoc/>
    public IAsyncEnumerable<PartyRecord> GetPartyById(
        Guid partyUuid, 
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default)
    {
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
        IList<Guid>? partyUuids = null,
        IList<int>? partyIds = null,
        IList<OrganizationIdentifier>? organizationIdentifiers = null,
        IList<PersonIdentifier>? personIdentifiers = null,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default)
    {
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
            // filter out organization fields as result is guaranteed to be a person
            include &= ~(PartyFieldIncludes.Organization & PartyFieldIncludes.SubUnits);
        }

        if (!persons)
        {
            // filter out person fields as result is guaranteed to be an organization
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
                query.AddPartyUuidListParameter(cmd, partyUuids);
            }
            
            if (partyIds is { Count: > 0 })
            {
                query.AddPartyIdListParameter(cmd, partyIds);
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

    [Flags]
    private enum PartyFilters
        : byte
    {
        None = 0,
        PartyId = 1 << 0,
        PartyUuid = 1 << 1,
        PersonIdentifier = 1 << 2,
        OrganizationIdentifier = 1 << 3,

        Multiple = 1 << 7,
    }

    #endregion

    #region Roles

    /// <inheritdoc/>
    public IAsyncEnumerable<PartyRoleRecord> GetRolesFromParty(
        Guid partyUuid,
        PartyRoleFieldIncludes include = PartyRoleFieldIncludes.Role,
        CancellationToken cancellationToken = default)
    {
        var query = PartyRoleQuery.Get(include, PartyRoleFilter.FromParty);
        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            query.AddFromPartyParameter(cmd, partyUuid);

            return PrepareAndReadPartyRolesAsync(cmd, query, cancellationToken);
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<PartyRoleRecord>(e).Using(cmd);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<PartyRoleRecord> GetRolesToParty(
        Guid partyUuid,
        PartyRoleFieldIncludes include = PartyRoleFieldIncludes.Role,
        CancellationToken cancellationToken = default)
    {
        var query = PartyRoleQuery.Get(include, PartyRoleFilter.ToParty);
        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            query.AddToPartyParameter(cmd, partyUuid);

            return PrepareAndReadPartyRolesAsync(cmd, query, cancellationToken);
        }
        catch (Exception e)
        {
            return new ThrowingAsyncEnumerable<PartyRoleRecord>(e).Using(cmd);
        }
    }

    private async IAsyncEnumerable<PartyRoleRecord> PrepareAndReadPartyRolesAsync(
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
            var role = query.ReadRole(reader);
            yield return role;
        }
    }

    private enum PartyRoleFilter
        : byte
    {
        FromParty,
        ToParty,
    }

    #endregion
}
