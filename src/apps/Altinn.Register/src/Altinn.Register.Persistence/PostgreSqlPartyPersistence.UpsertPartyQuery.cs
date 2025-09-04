using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties.Records;
using CommunityToolkit.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence;

/// <content>
/// Contains the upsert party functionality.
/// </content>
internal partial class PostgreSqlPartyPersistence
{
    /// <summary>
    /// Upsert party query helper.
    /// </summary>
    internal abstract class UpsertPartyQuery
    {
        private const int MAX_BATCH_SIZE = 1_000;

        private static readonly UpsertPersonQuery _person = new();
        private static readonly UpsertOrganizationParty _org = new();
        private static readonly UpsertSelfIdentifiedUserQuery _si = new();
        private static readonly UpsertSystemUserQuery _su = new();
        private static readonly UpsertEnterpriseUserQuery _eu = new();

        /// <summary>
        /// Upserts a party.
        /// </summary>
        /// <param name="conn">The connection.</param>
        /// <param name="party">The party.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>The updated party.</returns>
        public static ValueTask<Result<PartyRecord>> UpsertParty(
            NpgsqlConnection conn,
            PartyRecord party,
            CancellationToken cancellationToken)
            => UpsertParties(conn, new AsyncSingleton(party), cancellationToken)
                .FirstAsync(cancellationToken);

        /// <summary>
        /// Upserts multiple parties.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="parties">The parties.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>The updated parties.</returns>
        public static async IAsyncEnumerable<Result<PartyRecord>> UpsertParties(
            NpgsqlConnection connection,
            IAsyncEnumerable<PartyRecord> parties,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var batch = connection.CreateBatch();

            try
            {
                await foreach (var party in parties.WithCancellation(cancellationToken))
                {
                    switch (party)
                    {
                        case PersonRecord person:
                            _person.EnqueuePartyUpsert(batch, person);
                            break;

                        case OrganizationRecord org:
                            _org.EnqueuePartyUpsert(batch, org);
                            break;

                        case SelfIdentifiedUserRecord siu:
                            _si.EnqueuePartyUpsert(batch, siu);
                            break;

                        case SystemUserRecord su:
                            _su.EnqueuePartyUpsert(batch, su);
                            break;

                        case EnterpriseUserRecord eu:
                            _eu.EnqueuePartyUpsert(batch, eu);
                            break;

                        default:
                            ThrowHelper.ThrowArgumentException<Result<PartyRecord>>("Unsupported party type for batch upsert", nameof(party));
                            break;
                    }

                    if (batch.BatchCommands.Count >= MAX_BATCH_SIZE)
                    {
                        await foreach (var result in ExecuteBatch(batch, cancellationToken))
                        {
                            yield return result;
                            
                            if (result.IsProblem)
                            {
                                yield break;
                            }
                        }

                        await batch.DisposeAsync();
                        batch = connection.CreateBatch();
                    }
                }

                if (batch.BatchCommands.Count > 0)
                {
                    await foreach (var result in ExecuteBatch(batch, cancellationToken))
                    {
                        yield return result;
                        
                        if (result.IsProblem)
                        {
                            yield break;
                        }
                    }
                }
            }
            finally
            {
                await batch.DisposeAsync();
            }

            static async IAsyncEnumerable<Result<PartyRecord>> ExecuteBatch(
                NpgsqlBatch batch,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                await using var batchEnumerator = ExecuteBatchInner(batch, cancellationToken).GetAsyncEnumerator(cancellationToken);

                bool hasMore = false;
                ProblemInstance? problem = null;

                while (true)
                {
                    try
                    {
                        hasMore = await batchEnumerator.MoveNextAsync();
                    }
                    catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
                    {
                        problem = Problems.PartyConflict.Create([
                            new("constraint", e.ConstraintName ?? "<unknown>"),
                        ]);
                    }
                    catch (PostgresException e) when (e.SqlState == "ZZ001")
                    {
                        // ZZ001 is a custom SQLSTATE code used to indicate that the party update is invalid
                        problem = Problems.InvalidPartyUpdate.Create([
                            new("message", e.MessageText),
                            new("column", e.ColumnName ?? "<unknown>"),
                        ]);
                    }

                    if (problem is not null)
                    {
                        yield return problem;
                        break;
                    }

                    if (!hasMore)
                    {
                        break;
                    }

                    yield return batchEnumerator.Current;
                }
            }

            static async IAsyncEnumerable<Result<PartyRecord>> ExecuteBatchInner(
                NpgsqlBatch batch,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                await batch.PrepareAsync(cancellationToken);
                await using var reader = await batch.ExecuteReaderAsync(cancellationToken);

                do
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var partyType = await reader.GetConditionalFieldValueAsync<PartyRecordType>("p_party_type", cancellationToken);
                        Debug.Assert(partyType.HasValue);

                        Result<PartyRecord> result = partyType.Value switch
                        {
                            PartyRecordType.Person => await _person.ReadResult(reader, cancellationToken),
                            PartyRecordType.Organization => await _org.ReadResult(reader, cancellationToken),
                            PartyRecordType.SelfIdentifiedUser => await _si.ReadResult(reader, cancellationToken),
                            PartyRecordType.EnterpriseUser => await _eu.ReadResult(reader, cancellationToken),
                            PartyRecordType.SystemUser => await _su.ReadResult(reader, cancellationToken),
                            _ => ThrowHelper.ThrowInvalidOperationException<Result<PartyRecord>>("Unsupported party type from batch upsert"),
                        };

                        yield return result;
                    }
                }
                while (await reader.NextResultAsync(cancellationToken));
            }
        }

        /// <summary>
        /// Upsert a party's user information.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="partyUuid">The party UUID.</param>
        /// <param name="user">The user info.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>The updated party user information.</returns>
        public static async Task<Result<PartyUserRecord>> UpsertPartyUser(
            NpgsqlConnection connection,
            Guid partyUuid,
            PartyUserRecord user,
            CancellationToken cancellationToken)
        {
            const string QUERY =
                /*strpsql*/"""
                SELECT *
                FROM register.upsert_user(
                    @uuid,
                    @user_ids,
                    @set_username,
                    @username)
                """;

            await using var cmd = connection.CreateCommand(QUERY);

            var userIds = user.UserIds.Select(static ids => ids.Select(static id => checked((int)id)).ToArray());
            Debug.Assert(userIds.HasValue);
            Debug.Assert(userIds.Value.Length > 0);

            cmd.Parameters.Add<Guid>("uuid", NpgsqlDbType.Uuid).TypedValue = partyUuid;
            cmd.Parameters.Add<int[]>("user_ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array).TypedValue = userIds.Value;
            cmd.Parameters.Add<bool>("set_username", NpgsqlDbType.Boolean).TypedValue = user.Username.IsSet;
            cmd.Parameters.Add<string?>("username", NpgsqlDbType.Text).TypedValue = user.Username.Value;

            await cmd.PrepareAsync(cancellationToken);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            var read = await reader.ReadAsync(cancellationToken);
            Debug.Assert(read, "Expected a row from upsert_user");

            var result = await ReadUser(reader, cancellationToken);
            Debug.Assert(result.HasValue, "Expected a user record from upsert_user");

            return result.Value;
        }

        /// <summary>
        /// Upserts a user record for a party.
        /// </summary>
        /// <param name="connection">The connection</param>
        /// <param name="partyUuid">The party UUID.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="username">The username.</param>
        /// <param name="isActive">Whether the user is active.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        public static async Task<Result> UpsertUserRecord(
            NpgsqlConnection connection,
            Guid partyUuid,
            ulong userId,
            FieldValue<string> username,
            bool isActive,
            CancellationToken cancellationToken)
        {
            const string QUERY =
                /*strpsql*/"""
                SELECT *
                FROM register.upsert_user_record(
                    @party_uuid,
                    @user_id,
                    @set_username,
                    @username,
                    @is_active)
                """;

            await using var cmd = connection.CreateCommand(QUERY);

            cmd.Parameters.Add<Guid>("party_uuid", NpgsqlDbType.Uuid).TypedValue = partyUuid;
            cmd.Parameters.Add<long>("user_id", NpgsqlDbType.Bigint).TypedValue = checked((long)userId);
            cmd.Parameters.Add<bool>("set_username", NpgsqlDbType.Boolean).TypedValue = username.IsSet;
            cmd.Parameters.Add<string?>("username", NpgsqlDbType.Text).TypedValue = username.HasValue ? username.Value : null;
            cmd.Parameters.Add<bool>("is_active", NpgsqlDbType.Boolean).TypedValue = isActive;

            await cmd.PrepareAsync(cancellationToken);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            var read = await reader.ReadAsync(cancellationToken);
            Debug.Assert(read, "Expected a row from upsert_user_record");

            return Result.Success;
        }

        private const string DEFAULT_QUERY =
            /*strpsql*/"""
            SELECT *
            FROM register.upsert_party(
                @uuid,
                @id,
                @user_ids,
                @set_username,
                @username,
                @party_type,
                @display_name,
                @person_id,
                @org_id,
                @created_at,
                @modified_at,
                @set_is_deleted,
                @is_deleted,
                @set_owner,
                @owner)
            """;

        /// <summary>
        /// Reads user information from a data reader.
        /// </summary>
        protected static async Task<FieldValue<PartyUserRecord>> ReadUser(NpgsqlDataReader reader, CancellationToken cancellationToken)
        {
            var fromDbUserIds = await reader.GetConditionalFieldValueAsync<int[]>("p_user_ids", cancellationToken);
            var username = await reader.GetConditionalFieldValueAsync<string>("p_username", cancellationToken);

            if (!fromDbUserIds.HasValue)
            {
                return FieldValue.Unset;
            }

            var userIds = fromDbUserIds.Value.Select(static id => checked((uint)id)).ToImmutableValueArray();
            var userId = userIds[0];
            return new PartyUserRecord(userId: userId, username: username, userIds: userIds);
        }

        private abstract class Typed<T>(PartyRecordType type, string query = DEFAULT_QUERY)
            : UpsertPartyQuery
            where T : PartyRecord
        {
            protected virtual void ValidateFields(T party)
            {
                var userIds = party.User.SelectFieldValue(static u => u.UserIds);

                Debug.Assert(party.PartyUuid.HasValue);
                Debug.Assert(party.User.IsUnset || (userIds.HasValue && !userIds.Value.IsDefaultOrEmpty));
                Debug.Assert(party.PartyType.HasValue && party.PartyType.Value == type);
                Debug.Assert(party.DisplayName.HasValue);
                Debug.Assert(party.PersonIdentifier.IsSet);
                Debug.Assert(party.OrganizationIdentifier.IsSet);
                Debug.Assert(party.CreatedAt.HasValue);
                Debug.Assert(party.ModifiedAt.HasValue);
                Debug.Assert(!party.IsDeleted.IsNull);

                if (type is PartyRecordType.Person or PartyRecordType.Organization or PartyRecordType.SelfIdentifiedUser)
                {
                    Debug.Assert(party.PartyId.HasValue);
                }
                else
                {
                    Debug.Assert(party.PartyId.IsNull);
                }
            }

            protected virtual void AddPartyParameters(NpgsqlParameterCollection parameters, T party)
            {
                var userIds = party.User
                    .SelectFieldValue(static u => u.UserIds)
                    .Select(static ids => ids.Select(static id => checked((int)id)).ToArray());

                var username = party.User.SelectFieldValue(static u => u.Username);

                parameters.Add<Guid>("uuid", NpgsqlDbType.Uuid).TypedValue = party.PartyUuid.Value;
                parameters.Add<int?>("id", NpgsqlDbType.Bigint).TypedValue = party.PartyId.IsNull ? null : checked((int)party.PartyId.Value);
                parameters.Add<int[]?>("user_ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array).TypedValue = userIds.OrDefault();
                parameters.Add<bool>("set_username", NpgsqlDbType.Boolean).TypedValue = username.IsSet;
                parameters.Add<string?>("username", NpgsqlDbType.Text).TypedValue = username.Value;
                parameters.Add<PartyRecordType>("party_type").TypedValue = party.PartyType.Value;
                parameters.Add<string>("display_name", NpgsqlDbType.Text).TypedValue = party.DisplayName.Value;
                parameters.Add<string>("person_id", NpgsqlDbType.Text).TypedValue = party.PersonIdentifier.IsNull ? null : party.PersonIdentifier.Value!.ToString();
                parameters.Add<string>("org_id", NpgsqlDbType.Text).TypedValue = party.OrganizationIdentifier.IsNull ? null : party.OrganizationIdentifier.Value!.ToString();
                parameters.Add<DateTimeOffset>("created_at", NpgsqlDbType.TimestampTz).TypedValue = party.CreatedAt.Value;
                parameters.Add<DateTimeOffset>("modified_at", NpgsqlDbType.TimestampTz).TypedValue = party.ModifiedAt.Value;
                parameters.Add<bool>("set_is_deleted", NpgsqlDbType.Boolean).TypedValue = party.IsDeleted.IsSet;
                parameters.Add<bool?>("is_deleted", NpgsqlDbType.Boolean).TypedValue = party.IsDeleted.HasValue ? party.IsDeleted.Value : null;
                parameters.Add<bool>("set_owner", NpgsqlDbType.Boolean).TypedValue = party.OwnerUuid.IsSet;
                parameters.Add<Guid?>("owner", NpgsqlDbType.Uuid).TypedValue = party.OwnerUuid.HasValue ? party.OwnerUuid.Value : null;
            }

            public abstract Task<T> ReadResult(NpgsqlDataReader reader, CancellationToken cancellationToken);

            public void EnqueuePartyUpsert(NpgsqlBatch batch, T party)
            {
                ValidateFields(party);

                var cmd = batch.CreateBatchCommand(query);
                AddPartyParameters(cmd.Parameters, party);
            }
        }

        private sealed class UpsertPersonQuery()
            : Typed<PersonRecord>(PartyRecordType.Person, QUERY)
        {
            private const string QUERY =
                /*strpsql*/"""
                SELECT *
                FROM register.upsert_party_pers(
                    @uuid,
                    @id,
                    @user_ids,
                    @set_username,
                    @username,
                    @party_type,
                    @display_name,
                    @person_id,
                    @org_id,
                    @created_at,
                    @modified_at,
                    @set_is_deleted,
                    @is_deleted,
                    @set_owner,
                    @owner,
                    @first_name,
                    @middle_name,
                    @last_name,
                    @short_name,
                    @date_of_birth,
                    @date_of_death,
                    @address,
                    @mailing_address)
                """;

            protected override void ValidateFields(PersonRecord party)
            {
                base.ValidateFields(party);
                Debug.Assert(party.FirstName.HasValue, "person must have FirstName set");
                Debug.Assert(party.MiddleName.IsSet, "person must have MiddleName set");
                Debug.Assert(party.LastName.HasValue, "person must have LastName set");
                Debug.Assert(party.ShortName.HasValue, "person must have ShortName set");
                Debug.Assert(party.Address.IsSet, "person must have Address set");
                Debug.Assert(party.MailingAddress.IsSet, "person must have MailingAddress set");
                Debug.Assert(party.DateOfBirth.IsSet, "person must have DateOfBirth set");
                Debug.Assert(party.DateOfDeath.IsSet, "person must have DateOfDeath set");
                Debug.Assert(!party.OwnerUuid.HasValue, "person cannot have OwnerUuid set");
            }

            protected override void AddPartyParameters(NpgsqlParameterCollection parameters, PersonRecord party)
            {
                base.AddPartyParameters(parameters, party);
                parameters.Add<string>("first_name", NpgsqlDbType.Text).TypedValue = party.FirstName.Value;
                parameters.Add<string?>("middle_name", NpgsqlDbType.Text).TypedValue = party.MiddleName.IsNull ? null : party.MiddleName.Value;
                parameters.Add<string>("last_name", NpgsqlDbType.Text).TypedValue = party.LastName.Value;
                parameters.Add<string>("short_name", NpgsqlDbType.Text).TypedValue = party.ShortName.Value;
                parameters.Add<DateOnly>("date_of_birth").TypedValue = party.DateOfBirth.Value;
                parameters.Add<DateOnly?>("date_of_death").TypedValue = party.DateOfDeath.IsNull ? null : party.DateOfDeath.Value;
                parameters.Add<StreetAddressRecord>("address").TypedValue = party.Address.Value;
                parameters.Add<MailingAddressRecord>("mailing_address").TypedValue = party.MailingAddress.Value;
            }

            public override async Task<PersonRecord> ReadResult(NpgsqlDataReader reader, CancellationToken cancellationToken)
            {
                return new PersonRecord
                {
                    PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_uuid", cancellationToken),
                    OwnerUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_owner", cancellationToken),
                    PartyId = await reader.GetConditionalFieldValueAsync<int>("p_id", cancellationToken).Select(static id => checked((uint)id)),
                    User = await ReadUser(reader, cancellationToken),
                    DisplayName = await reader.GetConditionalFieldValueAsync<string>("p_display_name", cancellationToken),
                    PersonIdentifier = await reader.GetConditionalParsableFieldValueAsync<PersonIdentifier>("p_person_identifier", cancellationToken),
                    OrganizationIdentifier = await reader.GetConditionalParsableFieldValueAsync<OrganizationIdentifier>("p_organization_identifier", cancellationToken),
                    CreatedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("p_created", cancellationToken),
                    ModifiedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("p_updated", cancellationToken),
                    IsDeleted = await reader.GetConditionalFieldValueAsync<bool>("p_is_deleted", cancellationToken),
                    VersionId = await reader.GetConditionalFieldValueAsync<long>("o_version_id", cancellationToken).Select(static v => (ulong)v),

                    FirstName = await reader.GetConditionalFieldValueAsync<string>("p_first_name", cancellationToken),
                    MiddleName = await reader.GetConditionalFieldValueAsync<string>("p_middle_name", cancellationToken),
                    LastName = await reader.GetConditionalFieldValueAsync<string>("p_last_name", cancellationToken),
                    ShortName = await reader.GetConditionalFieldValueAsync<string>("p_short_name", cancellationToken),
                    DateOfBirth = await reader.GetConditionalFieldValueAsync<DateOnly>("p_date_of_birth", cancellationToken),
                    DateOfDeath = await reader.GetConditionalFieldValueAsync<DateOnly>("p_date_of_death", cancellationToken),
                    Address = await reader.GetConditionalFieldValueAsync<StreetAddressRecord>("p_address", cancellationToken),
                    MailingAddress = await reader.GetConditionalFieldValueAsync<MailingAddressRecord>("p_mailing_address", cancellationToken),
                };
            }
        }

        private sealed class UpsertOrganizationParty()
            : Typed<OrganizationRecord>(PartyRecordType.Organization, QUERY)
        {
            private const string QUERY =
                /*strpsql*/"""
                SELECT *
                FROM register.upsert_party_org(
                    @uuid,
                    @id,
                    @user_ids,
                    @set_username,
                    @username,
                    @party_type,
                    @display_name,
                    @person_id,
                    @org_id,
                    @created_at,
                    @modified_at,
                    @set_is_deleted,
                    @is_deleted,
                    @set_owner,
                    @owner,
                    @unit_status,
                    @unit_type,
                    @telephone_number,
                    @mobile_number,
                    @fax_number,
                    @email_address,
                    @internet_address,
                    @mailing_address,
                    @business_address)
                """;

            protected override void ValidateFields(OrganizationRecord party)
            {
                base.ValidateFields(party);
                Debug.Assert(party.UnitStatus.HasValue);
                Debug.Assert(party.UnitType.HasValue);
                Debug.Assert(party.TelephoneNumber.IsSet);
                Debug.Assert(party.MobileNumber.IsSet);
                Debug.Assert(party.FaxNumber.IsSet);
                Debug.Assert(party.EmailAddress.IsSet);
                Debug.Assert(party.InternetAddress.IsSet);
                Debug.Assert(party.MailingAddress.IsSet);
                Debug.Assert(party.BusinessAddress.IsSet);
                Debug.Assert(!party.OwnerUuid.HasValue, "organization cannot have OwnerUuid set");
            }

            protected override void AddPartyParameters(NpgsqlParameterCollection parameters, OrganizationRecord party)
            {
                base.AddPartyParameters(parameters, party);
                parameters.Add<string>("unit_status", NpgsqlDbType.Text).TypedValue = party.UnitStatus.Value;
                parameters.Add<string>("unit_type", NpgsqlDbType.Text).TypedValue = party.UnitType.Value;
                parameters.Add<string>("telephone_number", NpgsqlDbType.Text).TypedValue = party.TelephoneNumber.Value;
                parameters.Add<string>("mobile_number", NpgsqlDbType.Text).TypedValue = party.MobileNumber.Value;
                parameters.Add<string>("fax_number", NpgsqlDbType.Text).TypedValue = party.FaxNumber.Value;
                parameters.Add<string>("email_address", NpgsqlDbType.Text).TypedValue = party.EmailAddress.Value;
                parameters.Add<string>("internet_address", NpgsqlDbType.Text).TypedValue = party.InternetAddress.Value;
                parameters.Add<MailingAddressRecord>("mailing_address").TypedValue = party.MailingAddress.Value;
                parameters.Add<MailingAddressRecord>("business_address").TypedValue = party.BusinessAddress.Value;
            }

            public override async Task<OrganizationRecord> ReadResult(NpgsqlDataReader reader, CancellationToken cancellationToken)
            {
                return new OrganizationRecord
                {
                    PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_uuid", cancellationToken),
                    OwnerUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_owner", cancellationToken),
                    PartyId = await reader.GetConditionalFieldValueAsync<int>("p_id", cancellationToken).Select(static id => checked((uint)id)),
                    User = await ReadUser(reader, cancellationToken),
                    DisplayName = await reader.GetConditionalFieldValueAsync<string>("p_display_name", cancellationToken),
                    PersonIdentifier = await reader.GetConditionalParsableFieldValueAsync<PersonIdentifier>("p_person_identifier", cancellationToken),
                    OrganizationIdentifier = await reader.GetConditionalParsableFieldValueAsync<OrganizationIdentifier>("p_organization_identifier", cancellationToken),
                    CreatedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("p_created", cancellationToken),
                    ModifiedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("p_updated", cancellationToken),
                    IsDeleted = await reader.GetConditionalFieldValueAsync<bool>("p_is_deleted", cancellationToken),
                    VersionId = await reader.GetConditionalFieldValueAsync<long>("o_version_id", cancellationToken).Select(static v => (ulong)v),

                    UnitStatus = await reader.GetConditionalFieldValueAsync<string>("p_unit_status", cancellationToken),
                    UnitType = await reader.GetConditionalFieldValueAsync<string>("p_unit_type", cancellationToken),
                    TelephoneNumber = await reader.GetConditionalFieldValueAsync<string>("p_telephone_number", cancellationToken),
                    MobileNumber = await reader.GetConditionalFieldValueAsync<string>("p_mobile_number", cancellationToken),
                    FaxNumber = await reader.GetConditionalFieldValueAsync<string>("p_fax_number", cancellationToken),
                    EmailAddress = await reader.GetConditionalFieldValueAsync<string>("p_email_address", cancellationToken),
                    InternetAddress = await reader.GetConditionalFieldValueAsync<string>("p_internet_address", cancellationToken),
                    MailingAddress = await reader.GetConditionalFieldValueAsync<MailingAddressRecord>("p_mailing_address", cancellationToken),
                    BusinessAddress = await reader.GetConditionalFieldValueAsync<MailingAddressRecord>("p_business_address", cancellationToken),

                    ParentOrganizationUuid = FieldValue.Unset,
                };
            }
        }

        private sealed class UpsertSelfIdentifiedUserQuery()
            : Typed<SelfIdentifiedUserRecord>(PartyRecordType.SelfIdentifiedUser)
        {
            public override async Task<SelfIdentifiedUserRecord> ReadResult(NpgsqlDataReader reader, CancellationToken cancellationToken)
            {
                return new SelfIdentifiedUserRecord
                {
                    PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_uuid", cancellationToken),
                    OwnerUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_owner", cancellationToken),
                    PartyId = await reader.GetConditionalFieldValueAsync<int>("p_id", cancellationToken).Select(static id => checked((uint)id)),
                    User = await ReadUser(reader, cancellationToken),
                    DisplayName = await reader.GetConditionalFieldValueAsync<string>("p_display_name", cancellationToken),
                    PersonIdentifier = await reader.GetConditionalParsableFieldValueAsync<PersonIdentifier>("p_person_identifier", cancellationToken),
                    OrganizationIdentifier = await reader.GetConditionalParsableFieldValueAsync<OrganizationIdentifier>("p_organization_identifier", cancellationToken),
                    CreatedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("p_created", cancellationToken),
                    ModifiedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("p_updated", cancellationToken),
                    IsDeleted = await reader.GetConditionalFieldValueAsync<bool>("p_is_deleted", cancellationToken),
                    VersionId = await reader.GetConditionalFieldValueAsync<long>("o_version_id", cancellationToken).Select(static v => (ulong)v),
                };
            }
        }

        private sealed class UpsertSystemUserQuery()
            : Typed<SystemUserRecord>(PartyRecordType.SystemUser)
        {
            public override async Task<SystemUserRecord> ReadResult(NpgsqlDataReader reader, CancellationToken cancellationToken)
            {
                return new SystemUserRecord
                {
                    PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_uuid", cancellationToken),
                    OwnerUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_owner", cancellationToken),
                    PartyId = await reader.GetConditionalFieldValueAsync<int>("p_id", cancellationToken).Select(static id => checked((uint)id)),
                    User = await ReadUser(reader, cancellationToken),
                    DisplayName = await reader.GetConditionalFieldValueAsync<string>("p_display_name", cancellationToken),
                    PersonIdentifier = await reader.GetConditionalParsableFieldValueAsync<PersonIdentifier>("p_person_identifier", cancellationToken),
                    OrganizationIdentifier = await reader.GetConditionalParsableFieldValueAsync<OrganizationIdentifier>("p_organization_identifier", cancellationToken),
                    CreatedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("p_created", cancellationToken),
                    ModifiedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("p_updated", cancellationToken),
                    IsDeleted = await reader.GetConditionalFieldValueAsync<bool>("p_is_deleted", cancellationToken),
                    VersionId = await reader.GetConditionalFieldValueAsync<long>("o_version_id", cancellationToken).Select(static v => (ulong)v),
                };
            }
        }

        private sealed class UpsertEnterpriseUserQuery()
            : Typed<EnterpriseUserRecord>(PartyRecordType.EnterpriseUser)
        {
            public override async Task<EnterpriseUserRecord> ReadResult(NpgsqlDataReader reader, CancellationToken cancellationToken)
            {
                return new EnterpriseUserRecord
                {
                    PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_uuid", cancellationToken),
                    OwnerUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_owner", cancellationToken),
                    PartyId = await reader.GetConditionalFieldValueAsync<int>("p_id", cancellationToken).Select(static id => checked((uint)id)),
                    User = await ReadUser(reader, cancellationToken),
                    DisplayName = await reader.GetConditionalFieldValueAsync<string>("p_display_name", cancellationToken),
                    PersonIdentifier = await reader.GetConditionalParsableFieldValueAsync<PersonIdentifier>("p_person_identifier", cancellationToken),
                    OrganizationIdentifier = await reader.GetConditionalParsableFieldValueAsync<OrganizationIdentifier>("p_organization_identifier", cancellationToken),
                    CreatedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("p_created", cancellationToken),
                    ModifiedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>("p_updated", cancellationToken),
                    IsDeleted = await reader.GetConditionalFieldValueAsync<bool>("p_is_deleted", cancellationToken),
                    VersionId = await reader.GetConditionalFieldValueAsync<long>("o_version_id", cancellationToken).Select(static v => (ulong)v),
                };
            }
        }

        /// <summary>
        /// Singleton <see cref="IAsyncEnumerable{T}"/> for a <see cref="PartyRecord"/>.
        /// </summary>
        internal sealed class AsyncSingleton(PartyRecord party)
            : IAsyncEnumerable<PartyRecord>
            , IAsyncEnumerator<PartyRecord>
        {
            private readonly PartyRecord _party = party;
            private int _index = -1;

            /// <inheritdoc/>
            PartyRecord IAsyncEnumerator<PartyRecord>.Current
                => _party;

            /// <inheritdoc/>
            ValueTask IAsyncDisposable.DisposeAsync()
                => ValueTask.CompletedTask;

            /// <inheritdoc/>
            IAsyncEnumerator<PartyRecord> IAsyncEnumerable<PartyRecord>.GetAsyncEnumerator(CancellationToken cancellationToken)
                => this;

            /// <inheritdoc/>
            ValueTask<bool> IAsyncEnumerator<PartyRecord>.MoveNextAsync()
                => new(Interlocked.Increment(ref _index) == 0);
        }
    }
}
