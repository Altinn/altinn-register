using System.Data;
using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties;
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
    private abstract class UpsertPartyQuery
    {
        private static readonly UpsertPersonQuery _person = new();
        private static readonly UpsertOrganizationParty _org = new();
        private static readonly UpsertSelfIdentifiedUserQuery _si = new();

        public static Task<Result<PartyRecord>> UpsertParty(
            NpgsqlConnection conn,
            PartyRecord party,
            CancellationToken cancellationToken)
        {
            return party switch
            {
                PersonRecord person => _person.UpsertParty(conn, person, cancellationToken),
                OrganizationRecord org => _org.UpsertParty(conn, org, cancellationToken),
                SelfIdentifiedUserRecord siu => _si.UpsertParty(conn, siu, cancellationToken),
                _ => ThrowHelper.ThrowArgumentException<Task<Result<PartyRecord>>>("Unsupported party type"),
            };
        }

        private const string DEFAULT_QUERY =
            /*strpsql*/"""
            SELECT (register.upsert_party(
                @uuid,
                @id,
                @user_ids,
                @party_type,
                @display_name,
                @person_id,
                @org_id,
                @created_at,
                @modified_at,
                @is_deleted)).*
            """;

        private abstract class Typed<T>(PartyType type, string query = DEFAULT_QUERY)
            : UpsertPartyQuery
            where T : PartyRecord
        {
            protected virtual void ValidateFields(T party)
            {
                var userIds = party.User.SelectFieldValue(static u => u.UserIds);

                Debug.Assert(party.PartyUuid.HasValue);
                Debug.Assert(party.PartyId.HasValue);
                Debug.Assert(party.User.IsUnset || (userIds.HasValue && !userIds.Value.IsDefaultOrEmpty));
                Debug.Assert(party.PartyType.HasValue && party.PartyType.Value == type);
                Debug.Assert(party.DisplayName.HasValue);
                Debug.Assert(party.PersonIdentifier.IsSet);
                Debug.Assert(party.OrganizationIdentifier.IsSet);
                Debug.Assert(party.CreatedAt.HasValue);
                Debug.Assert(party.ModifiedAt.HasValue);
                Debug.Assert(party.IsDeleted.HasValue);
            }

            protected virtual void AddPartyParameters(NpgsqlCommand cmd, T party)
            {
                var userIds = party.User
                    .SelectFieldValue(static u => u.UserIds)
                    .Select(static ids => ids.Select(static id => checked((int)id)).ToArray());

                cmd.Parameters.Add<Guid>("uuid", NpgsqlDbType.Uuid).TypedValue = party.PartyUuid.Value;
                cmd.Parameters.Add<int>("id", NpgsqlDbType.Bigint).TypedValue = party.PartyId.Value;
                cmd.Parameters.Add<int[]?>("user_ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array).TypedValue = userIds.OrDefault();
                cmd.Parameters.Add<PartyType>("party_type").TypedValue = party.PartyType.Value;
                cmd.Parameters.Add<string>("display_name", NpgsqlDbType.Text).TypedValue = party.DisplayName.Value;
                cmd.Parameters.Add<string>("person_id", NpgsqlDbType.Text).TypedValue = party.PersonIdentifier.IsNull ? null : party.PersonIdentifier.Value!.ToString();
                cmd.Parameters.Add<string>("org_id", NpgsqlDbType.Text).TypedValue = party.OrganizationIdentifier.IsNull ? null : party.OrganizationIdentifier.Value!.ToString();
                cmd.Parameters.Add<DateTimeOffset>("created_at", NpgsqlDbType.TimestampTz).TypedValue = party.CreatedAt.Value;
                cmd.Parameters.Add<DateTimeOffset>("modified_at", NpgsqlDbType.TimestampTz).TypedValue = party.ModifiedAt.Value;
                cmd.Parameters.Add<bool>("is_deleted", NpgsqlDbType.Boolean).TypedValue = party.IsDeleted.Value;
            }

            protected async Task<FieldValue<PartyUserRecord>> ReadUser(NpgsqlDataReader reader, CancellationToken cancellationToken)
            {
                var fromDbUserIds = await reader.GetConditionalFieldValueAsync<int[]>("p_user_ids", cancellationToken);

                if (!fromDbUserIds.HasValue)
                {
                    return FieldValue.Unset;
                }

                var userIds = fromDbUserIds.Value.Select(static id => checked((uint)id)).ToImmutableValueArray();
                return new PartyUserRecord { UserIds = userIds };
            }

            protected abstract Task<T> ReadResult(NpgsqlDataReader reader, CancellationToken cancellationToken);

            public async Task<Result<PartyRecord>> UpsertParty(NpgsqlConnection connection, T party, CancellationToken cancellationToken)
            {
                ValidateFields(party);

                try
                {
                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = query;

                    AddPartyParameters(cmd, party);

                    await cmd.PrepareAsync(cancellationToken);
                    await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
                    var read = await reader.ReadAsync(cancellationToken);
                    Debug.Assert(read, "Expected a row from upsert_party");

                    var partyType = await reader.GetConditionalFieldValueAsync<PartyType>("p_party_type", cancellationToken);
                    Debug.Assert(partyType == type);

                    return await ReadResult(reader, cancellationToken);
                }
                catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    return Problems.PartyConflict;
                }
                catch (PostgresException e) when (e.SqlState == "ZZ001")
                {
                    // ZZ001 is a custom SQLSTATE code used to indicate that the party update is invalid
                    return Problems.InvalidPartyUpdate.Create([
                        new("message", e.MessageText),
                        new("column", e.ColumnName ?? "<unknown>"),
                    ]);
                }
            }
        }

        private sealed class UpsertPersonQuery()
            : Typed<PersonRecord>(PartyType.Person, QUERY)
        {
            private const string QUERY =
                /*strpsql*/"""
                SELECT (register.upsert_party_pers(
                    @uuid,
                    @id,
                    @user_ids,
                    @party_type,
                    @display_name,
                    @person_id,
                    @org_id,
                    @created_at,
                    @modified_at,
                    @is_deleted,
                    @first_name,
                    @middle_name,
                    @last_name,
                    @short_name,
                    @date_of_birth,
                    @date_of_death,
                    @address,
                    @mailing_address)).*
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
            }

            protected override void AddPartyParameters(NpgsqlCommand cmd, PersonRecord party)
            {
                base.AddPartyParameters(cmd, party);
                cmd.Parameters.Add<string>("first_name", NpgsqlDbType.Text).TypedValue = party.FirstName.Value;
                cmd.Parameters.Add<string?>("middle_name", NpgsqlDbType.Text).TypedValue = party.MiddleName.IsNull ? null : party.MiddleName.Value;
                cmd.Parameters.Add<string>("last_name", NpgsqlDbType.Text).TypedValue = party.LastName.Value;
                cmd.Parameters.Add<string>("short_name", NpgsqlDbType.Text).TypedValue = party.ShortName.Value;
                cmd.Parameters.Add<DateOnly>("date_of_birth").TypedValue = party.DateOfBirth.Value;
                cmd.Parameters.Add<DateOnly?>("date_of_death").TypedValue = party.DateOfDeath.IsNull ? null : party.DateOfDeath.Value;
                cmd.Parameters.Add<StreetAddress>("address").TypedValue = party.Address.Value;
                cmd.Parameters.Add<MailingAddress>("mailing_address").TypedValue = party.MailingAddress.Value;
            }

            protected override async Task<PersonRecord> ReadResult(NpgsqlDataReader reader, CancellationToken cancellationToken)
            {
                return new PersonRecord
                {
                    PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_uuid", cancellationToken),
                    PartyId = await reader.GetConditionalFieldValueAsync<int>("p_id", cancellationToken),
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
                    Address = await reader.GetConditionalFieldValueAsync<StreetAddress>("p_address", cancellationToken),
                    MailingAddress = await reader.GetConditionalFieldValueAsync<MailingAddress>("p_mailing_address", cancellationToken),
                };
            }
        }

        private sealed class UpsertOrganizationParty()
            : Typed<OrganizationRecord>(PartyType.Organization, QUERY)
        {
            private const string QUERY =
                /*strpsql*/"""
                SELECT (register.upsert_party_org(
                    @uuid,
                    @id,
                    @user_ids,
                    @party_type,
                    @display_name,
                    @person_id,
                    @org_id,
                    @created_at,
                    @modified_at,
                    @is_deleted,
                    @unit_status,
                    @unit_type,
                    @telephone_number,
                    @mobile_number,
                    @fax_number,
                    @email_address,
                    @internet_address,
                    @mailing_address,
                    @business_address)).*
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
            }

            protected override void AddPartyParameters(NpgsqlCommand cmd, OrganizationRecord party)
            {
                base.AddPartyParameters(cmd, party);
                cmd.Parameters.Add<string>("unit_status", NpgsqlDbType.Text).TypedValue = party.UnitStatus.Value;
                cmd.Parameters.Add<string>("unit_type", NpgsqlDbType.Text).TypedValue = party.UnitType.Value;
                cmd.Parameters.Add<string>("telephone_number", NpgsqlDbType.Text).TypedValue = party.TelephoneNumber.Value;
                cmd.Parameters.Add<string>("mobile_number", NpgsqlDbType.Text).TypedValue = party.MobileNumber.Value;
                cmd.Parameters.Add<string>("fax_number", NpgsqlDbType.Text).TypedValue = party.FaxNumber.Value;
                cmd.Parameters.Add<string>("email_address", NpgsqlDbType.Text).TypedValue = party.EmailAddress.Value;
                cmd.Parameters.Add<string>("internet_address", NpgsqlDbType.Text).TypedValue = party.InternetAddress.Value;
                cmd.Parameters.Add<MailingAddress>("mailing_address").TypedValue = party.MailingAddress.Value;
                cmd.Parameters.Add<MailingAddress>("business_address").TypedValue = party.BusinessAddress.Value;
            }

            protected override async Task<OrganizationRecord> ReadResult(NpgsqlDataReader reader, CancellationToken cancellationToken)
            {
                return new OrganizationRecord
                {
                    PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_uuid", cancellationToken),
                    PartyId = await reader.GetConditionalFieldValueAsync<int>("p_id", cancellationToken),
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
                    MailingAddress = await reader.GetConditionalFieldValueAsync<MailingAddress>("p_mailing_address", cancellationToken),
                    BusinessAddress = await reader.GetConditionalFieldValueAsync<MailingAddress>("p_business_address", cancellationToken),

                    ParentOrganizationUuid = FieldValue.Unset,
                };
            }
        }

        private sealed class UpsertSelfIdentifiedUserQuery()
            : Typed<SelfIdentifiedUserRecord>(PartyType.SelfIdentifiedUser)
        {
            protected override async Task<SelfIdentifiedUserRecord> ReadResult(NpgsqlDataReader reader, CancellationToken cancellationToken)
            {
                return new SelfIdentifiedUserRecord
                {
                    PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>("p_uuid", cancellationToken),
                    PartyId = await reader.GetConditionalFieldValueAsync<int>("p_id", cancellationToken),
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
    }
}
