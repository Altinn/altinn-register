using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.Utils;
using Altinn.Register.Persistence.AsyncEnumerables;
using Altinn.Register.Persistence.UnitOfWork;
using CommunityToolkit.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence;

/// <summary>
/// Implementation of <see cref="IV1PartyService"/> backed by a PostgreSQL database.
/// </summary>
internal partial class PostgreSqlPartyPersistence
    : IPartyPersistence
    , IPartyRolePersistence
{
    private readonly NpgsqlConnection _connection;
    private readonly SavePointManager _savePointManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlPartyPersistence"/> class.
    /// </summary>
    public PostgreSqlPartyPersistence(
        NpgsqlConnection connection,
        SavePointManager savePointManager)
    {
        _connection = connection;
        _savePointManager = savePointManager;
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

    /// <inheritdoc/>
    public IAsyncEnumerable<PartyRecord> GetPartyStream(
        ulong from,
        ushort limit,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default)
    {
        Guard.IsFalse(include.HasFlag(PartyFieldIncludes.SubUnits), nameof(include), $"{nameof(PartyFieldIncludes)}.{nameof(PartyFieldIncludes.SubUnits)} is not allowed");

        var query = PartyQuery.Get(include, PartyFilters.StreamPage);
        NpgsqlCommand? cmd = null;
        try
        {
            cmd = _connection.CreateCommand();
            cmd.CommandText = query.CommandText;

            query.AddStreamPageParameters(cmd, from, limit);

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

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = QUERY;

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            ThrowHelper.ThrowInvalidOperationException("No rows returned from MAX(version_id) query");
        }

        if (reader.IsDBNull(0))
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
        return party switch
        {
            PersonRecord person => UpsertPerson(person, cancellationToken),
            OrganizationRecord org => UpsertOrg(org, cancellationToken),
            _ => ThrowHelper.ThrowArgumentException<Task<Result<PartyRecord>>>("Unsupported party type"),
        };

        async Task<Result<PartyRecord>> TryInsertParty(
            PartyRecord party,
            CancellationToken cancellationToken)
        {
            const string SAVEPOINT_NAME = "try_insert_party";
            const string QUERY =
                /*strpsql*/"""
                INSERT INTO register.party (uuid, id, party_type, name, person_identifier, organization_identifier, created, updated, is_deleted)
                VALUES (@uuid, @id, @party_type, @name, @person_identifier, @organization_identifier, @created, @updated, @is_deleted)
                RETURNING uuid, id, party_type, name, person_identifier, organization_identifier, created, updated, is_deleted, version_id
                """;

            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = QUERY;

            cmd.Parameters.Add<Guid>("uuid", NpgsqlDbType.Uuid).TypedValue = party.PartyUuid.Value;
            cmd.Parameters.Add<int>("id", NpgsqlDbType.Integer).TypedValue = party.PartyId.Value;
            cmd.Parameters.Add<PartyType>("party_type").TypedValue = party.PartyType.Value;
            cmd.Parameters.Add<string>("name", NpgsqlDbType.Text).TypedValue = party.Name.Value;
            cmd.Parameters.Add<string>("person_identifier", NpgsqlDbType.Text).TypedValue = party.PersonIdentifier.Value?.ToString();
            cmd.Parameters.Add<string>("organization_identifier", NpgsqlDbType.Text).TypedValue = party.OrganizationIdentifier.Value?.ToString();
            cmd.Parameters.Add<DateTimeOffset>("created", NpgsqlDbType.TimestampTz).TypedValue = party.CreatedAt.Value;
            cmd.Parameters.Add<DateTimeOffset>("updated", NpgsqlDbType.TimestampTz).TypedValue = party.ModifiedAt.Value;
            cmd.Parameters.Add<bool>("is_deleted", NpgsqlDbType.Boolean).TypedValue = party.IsDeleted.Value;

            await cmd.PrepareAsync(cancellationToken);
            await using var savePoint = await _savePointManager.CreateSavePoint(SAVEPOINT_NAME, cancellationToken);

            PartyRecord result;
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                var read = await reader.ReadAsync(cancellationToken);

                Debug.Assert(read, "INSERT should return a row");

                result = new PartyRecord(await reader.GetConditionalFieldValueAsync<PartyType>("party_type", cancellationToken))
                {
                    PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>("uuid", cancellationToken),
                    PartyId = await reader.GetConditionalFieldValueAsync<int>("id", cancellationToken),
                    Name = await reader.GetConditionalFieldValueAsync<string>("name", cancellationToken),
                    PersonIdentifier = await reader.GetConditionalParsableFieldValueAsync<PersonIdentifier>("person_identifier", cancellationToken),
                    OrganizationIdentifier = await reader.GetConditionalParsableFieldValueAsync<OrganizationIdentifier>("organization_identifier", cancellationToken),
                    CreatedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("created", cancellationToken),
                    ModifiedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("updated", cancellationToken),
                    IsDeleted = await reader.GetConditionalFieldValueAsync<bool>("is_deleted", cancellationToken),
                    VersionId = await reader.GetConditionalFieldValueAsync<long>("version_id").Select(static v => (ulong)v),
                };
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation && e.ConstraintName == "party_pkey")
            {
                await savePoint.RollbackAsync(cancellationToken);
                return Problems.InvalidPartyUpdate;
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // duplicate id, orgno or ssn
                await savePoint.RollbackAsync(cancellationToken);
                return Problems.PartyConflict;
            }

            Debug.Assert(result.PartyUuid == party.PartyUuid, "PartyUuid should match");

            await savePoint.ReleaseAsync(cancellationToken);
            return result;
        }

        async Task<Result<PartyRecord>> DoUpdateParty(
            PartyRecord party,
            CancellationToken cancellationToken)
        {
            const string QUERY =
                /*strpsql*/"""
                UPDATE register.party
                SET name = @name, updated = @updated, is_deleted = @is_deleted
                WHERE 
                        uuid = @uuid
                    AND id = @id
                    AND party_type = @party_type
                    AND person_identifier IS NOT DISTINCT FROM @person_identifier
                    AND organization_identifier IS NOT DISTINCT FROM @organization_identifier
                RETURNING uuid, id, party_type, name, person_identifier, organization_identifier, created, updated, is_deleted, version_id
                """;

            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = QUERY;

            cmd.Parameters.Add<Guid>("uuid", NpgsqlDbType.Uuid).TypedValue = party.PartyUuid.Value;
            cmd.Parameters.Add<int>("id", NpgsqlDbType.Integer).TypedValue = party.PartyId.Value;
            cmd.Parameters.Add<PartyType>("party_type").TypedValue = party.PartyType.Value;
            cmd.Parameters.Add<string>("name", NpgsqlDbType.Text).TypedValue = party.Name.Value;
            cmd.Parameters.Add<string>("person_identifier", NpgsqlDbType.Text).TypedValue = party.PersonIdentifier.Value?.ToString();
            cmd.Parameters.Add<string>("organization_identifier", NpgsqlDbType.Text).TypedValue = party.OrganizationIdentifier.Value?.ToString();
            cmd.Parameters.Add<DateTimeOffset>("updated", NpgsqlDbType.TimestampTz).TypedValue = party.ModifiedAt.Value;
            cmd.Parameters.Add<bool>("is_deleted", NpgsqlDbType.Boolean).TypedValue = party.IsDeleted.Value;

            await cmd.PrepareAsync(cancellationToken);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var read = await reader.ReadAsync(cancellationToken);

            if (!read)
            {
                // no rows affected, this means that the [uuid, id, party_type, person_identifier, organization_identifier] fields
                // does not match what is present in the database, however, since we arrived here we've alredy hit a unique
                // constraint trying to insert the party - thus the update is invalid.
                return Problems.InvalidPartyUpdate;
            }

            var result = new PartyRecord(await reader.GetConditionalFieldValueAsync<PartyType>("party_type", cancellationToken))
            {
                PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>("uuid", cancellationToken),
                PartyId = await reader.GetConditionalFieldValueAsync<int>("id", cancellationToken),
                Name = await reader.GetConditionalFieldValueAsync<string>("name", cancellationToken),
                PersonIdentifier = await reader.GetConditionalParsableFieldValueAsync<PersonIdentifier>("person_identifier", cancellationToken),
                OrganizationIdentifier = await reader.GetConditionalParsableFieldValueAsync<OrganizationIdentifier>("organization_identifier", cancellationToken),
                CreatedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("created", cancellationToken),
                ModifiedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("updated", cancellationToken),
                IsDeleted = await reader.GetConditionalFieldValueAsync<bool>("is_deleted", cancellationToken),
                VersionId = await reader.GetConditionalFieldValueAsync<long>("version_id").Select(static v => (ulong)v),
            };

            Debug.Assert(result.PartyUuid == party.PartyUuid, "PartyUuid should match");
            return result;
        }

        async Task<Result<PartyRecord>> DoUpsertParty(
            PartyRecord party,
            CancellationToken cancellationToken)
        {
            Debug.Assert(party.PartyUuid.HasValue, "party must have PartyUuid set");
            Debug.Assert(party.PartyId.HasValue, "party must have PartyId set");
            Debug.Assert(party.PartyType.HasValue, "party must have PartyType set");
            Debug.Assert(party.Name.HasValue, "party must have Name set");
            Debug.Assert(party.PersonIdentifier.IsSet, "party must have PersonIdentifier set");
            Debug.Assert(party.OrganizationIdentifier.IsSet, "party must have OrganizationIdentifier set");
            Debug.Assert(party.CreatedAt.HasValue, "party must have CreatedAt set");
            Debug.Assert(party.ModifiedAt.HasValue, "party must have ModifiedAt set");
            Debug.Assert(party.IsDeleted.HasValue, "party must have IsDeleted set");

            // Note: we're running inside a transaction, so we don't need to worry about data races except in the case where the entire transaction fails
            var result = await TryInsertParty(party, cancellationToken);
            if (result.IsProblem && result.Problem.ErrorCode == Problems.InvalidPartyUpdate.ErrorCode)
            {
                return await DoUpdateParty(party, cancellationToken);
            }

            return result;
        }

        async Task<Result<PartyRecord>> UpsertPerson(
            PersonRecord record,
            CancellationToken cancellationToken)
        {
            const string QUERY =
                /*strpsql*/"""
                INSERT INTO register.person (uuid, first_name, middle_name, last_name, address, mailing_address, date_of_birth, date_of_death)
                VALUES (@uuid, @first_name, @middle_name, @last_name, @address, @mailing_address, @date_of_birth, @date_of_death)
                ON CONFLICT (uuid) DO UPDATE SET 
                    first_name = EXCLUDED.first_name,
                    middle_name = EXCLUDED.middle_name,
                    last_name = EXCLUDED.last_name,
                    address = EXCLUDED.address,
                    mailing_address = EXCLUDED.mailing_address,
                    date_of_birth = EXCLUDED.date_of_birth,
                    date_of_death = EXCLUDED.date_of_death
                RETURNING first_name, middle_name, last_name, address, mailing_address, date_of_birth, date_of_death
                """;

            Debug.Assert(record.FirstName.HasValue, "person must have FirstName set");
            Debug.Assert(record.MiddleName.IsSet, "person must have MiddleName set");
            Debug.Assert(record.LastName.HasValue, "person must have LastName set");
            Debug.Assert(record.Address.IsSet, "person must have Address set");
            Debug.Assert(record.MailingAddress.IsSet, "person must have MailingAddress set");
            Debug.Assert(record.DateOfBirth.HasValue, "person must have DateOfBirth set");
            Debug.Assert(record.DateOfDeath.IsSet, "person must have DateOfDeath set");

            var partyResult = await DoUpsertParty(record, cancellationToken);

            if (partyResult.IsProblem)
            {
                return partyResult;
            }

            var partyData = partyResult.Value;

            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = QUERY;

            cmd.Parameters.Add<Guid>("uuid", NpgsqlDbType.Uuid).TypedValue = partyData.PartyUuid.Value;
            cmd.Parameters.Add<string>("first_name", NpgsqlDbType.Text).TypedValue = record.FirstName.Value;
            cmd.Parameters.Add<string>("middle_name", NpgsqlDbType.Text).TypedValue = record.MiddleName.Value;
            cmd.Parameters.Add<string>("last_name", NpgsqlDbType.Text).TypedValue = record.LastName.Value;
            cmd.Parameters.Add<StreetAddress>("address").TypedValue = record.Address.Value;
            cmd.Parameters.Add<MailingAddress>("mailing_address").TypedValue = record.MailingAddress.Value;
            cmd.Parameters.Add<DateOnly>("date_of_birth", NpgsqlDbType.Date).TypedValue = record.DateOfBirth.Value;
            cmd.Parameters.Add<DateOnly?>("date_of_death", NpgsqlDbType.Date).TypedValue = record.DateOfDeath.HasValue ? record.DateOfDeath.Value : null;

            await cmd.PrepareAsync(cancellationToken);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var read = await reader.ReadAsync(cancellationToken);

            Debug.Assert(read, "INSERT should return a row");

            return new PersonRecord
            {
                PartyUuid = partyData.PartyUuid,
                PartyId = partyData.PartyId,
                Name = partyData.Name,
                PersonIdentifier = partyData.PersonIdentifier,
                OrganizationIdentifier = partyData.OrganizationIdentifier,
                CreatedAt = partyData.CreatedAt,
                ModifiedAt = partyData.ModifiedAt,
                IsDeleted = partyData.IsDeleted,
                VersionId = partyData.VersionId,
                FirstName = await reader.GetConditionalFieldValueAsync<string>("first_name", cancellationToken),
                MiddleName = await reader.GetConditionalFieldValueAsync<string>("middle_name", cancellationToken),
                LastName = await reader.GetConditionalFieldValueAsync<string>("last_name", cancellationToken),
                Address = await reader.GetConditionalFieldValueAsync<StreetAddress>("address", cancellationToken),
                MailingAddress = await reader.GetConditionalFieldValueAsync<MailingAddress>("mailing_address", cancellationToken),
                DateOfBirth = await reader.GetConditionalFieldValueAsync<DateOnly>("date_of_birth", cancellationToken),
                DateOfDeath = await reader.GetConditionalFieldValueAsync<DateOnly>("date_of_death", cancellationToken),
            };
        }

        async Task<Result<PartyRecord>> UpsertOrg(OrganizationRecord record, CancellationToken cancellationToken)
        {
            const string QUERY =
                /*strpsql*/"""
                INSERT INTO register.organization (
                    uuid, unit_status, unit_type, telephone_number, mobile_number, fax_number,
                    email_address, internet_address, mailing_address, business_address)
                VALUES (
                    @uuid, @unit_status, @unit_type, @telephone_number, @mobile_number, @fax_number,
                    @email_address, @internet_address, @mailing_address, @business_address)
                ON CONFLICT (uuid) DO UPDATE SET
                    unit_status = EXCLUDED.unit_status,
                    unit_type = EXCLUDED.unit_type,
                    telephone_number = EXCLUDED.telephone_number,
                    mobile_number = EXCLUDED.mobile_number,
                    fax_number = EXCLUDED.fax_number,
                    email_address = EXCLUDED.email_address,
                    internet_address = EXCLUDED.internet_address,
                    mailing_address = EXCLUDED.mailing_address,
                    business_address = EXCLUDED.business_address
                RETURNING 
                    unit_status, unit_type, telephone_number, mobile_number, fax_number,
                    email_address, internet_address, mailing_address, business_address
                """;

            Debug.Assert(record.UnitStatus.HasValue, "organization must have UnitStatus set");
            Debug.Assert(record.UnitType.HasValue, "organization must have UnitType set");
            Debug.Assert(record.TelephoneNumber.IsSet, "organization must have TelephoneNumber set");
            Debug.Assert(record.MobileNumber.IsSet, "organization must have MobileNumber set");
            Debug.Assert(record.FaxNumber.IsSet, "organization must have FaxNumber set");
            Debug.Assert(record.EmailAddress.IsSet, "organization must have EmailAddress set");
            Debug.Assert(record.InternetAddress.IsSet, "organization must have InternetAddress set");
            Debug.Assert(record.MailingAddress.IsSet, "organization must have MailingAddress set");
            Debug.Assert(record.BusinessAddress.IsSet, "organization must have BusinessAddress set");

            var partyResult = await DoUpsertParty(record, cancellationToken);

            if (partyResult.IsProblem)
            {
                return partyResult;
            }

            var partyData = partyResult.Value;

            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = QUERY;

            cmd.Parameters.Add<Guid>("uuid", NpgsqlDbType.Uuid).TypedValue = partyData.PartyUuid.Value;
            cmd.Parameters.Add<string>("unit_status", NpgsqlDbType.Text).TypedValue = record.UnitStatus.Value;
            cmd.Parameters.Add<string>("unit_type", NpgsqlDbType.Text).TypedValue = record.UnitType.Value;
            cmd.Parameters.Add<string>("telephone_number", NpgsqlDbType.Text).TypedValue = record.TelephoneNumber.Value;
            cmd.Parameters.Add<string>("mobile_number", NpgsqlDbType.Text).TypedValue = record.MobileNumber.Value;
            cmd.Parameters.Add<string>("fax_number", NpgsqlDbType.Text).TypedValue = record.FaxNumber.Value;
            cmd.Parameters.Add<string>("email_address", NpgsqlDbType.Text).TypedValue = record.EmailAddress.Value;
            cmd.Parameters.Add<string>("internet_address", NpgsqlDbType.Text).TypedValue = record.InternetAddress.Value;
            cmd.Parameters.Add<MailingAddress>("mailing_address").TypedValue = record.MailingAddress.Value;
            cmd.Parameters.Add<MailingAddress>("business_address").TypedValue = record.BusinessAddress.Value;

            await cmd.PrepareAsync(cancellationToken);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var read = await reader.ReadAsync(cancellationToken);

            Debug.Assert(read, "INSERT should return a row");

            return new OrganizationRecord
            {
                PartyUuid = partyData.PartyUuid,
                PartyId = partyData.PartyId,
                Name = partyData.Name,
                PersonIdentifier = partyData.PersonIdentifier,
                OrganizationIdentifier = partyData.OrganizationIdentifier,
                CreatedAt = partyData.CreatedAt,
                ModifiedAt = partyData.ModifiedAt,
                IsDeleted = partyData.IsDeleted,
                VersionId = partyData.VersionId,
                UnitStatus = await reader.GetConditionalFieldValueAsync<string>("unit_status", cancellationToken),
                UnitType = await reader.GetConditionalFieldValueAsync<string>("unit_type", cancellationToken),
                TelephoneNumber = await reader.GetConditionalFieldValueAsync<string>("telephone_number", cancellationToken),
                MobileNumber = await reader.GetConditionalFieldValueAsync<string>("mobile_number", cancellationToken),
                FaxNumber = await reader.GetConditionalFieldValueAsync<string>("fax_number", cancellationToken),
                EmailAddress = await reader.GetConditionalFieldValueAsync<string>("email_address", cancellationToken),
                InternetAddress = await reader.GetConditionalFieldValueAsync<string>("internet_address", cancellationToken),
                MailingAddress = await reader.GetConditionalFieldValueAsync<MailingAddress>("mailing_address", cancellationToken),
                BusinessAddress = await reader.GetConditionalFieldValueAsync<MailingAddress>("business_address", cancellationToken),

                ParentOrganizationUuid = FieldValue.Unset,
            };
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
            var role = await query.ReadRole(reader, cancellationToken);
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
