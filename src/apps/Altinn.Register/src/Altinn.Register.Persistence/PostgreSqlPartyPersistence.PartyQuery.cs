using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ModelUtils.EnumUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using CommunityToolkit.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence;

/// <content>
/// Contains the party query builder.
/// </content>
internal partial class PostgreSqlPartyPersistence
{
    /// <summary>
    /// Query for party records.
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1516:Elements should be separated by blank line", Justification = "This class is long enough already")]
    internal sealed class PartyQuery
    {
        private static ConcurrentDictionary<(PartyFieldIncludes Includes, PartyQueryFilters FilterBy), PartyQuery> _queries = new();

        /// <summary>
        /// Gets a cached query for the given includes and filters.
        /// </summary>
        /// <param name="includes">What fields to include.</param>
        /// <param name="filterBy">What to filter by.</param>
        /// <returns>A <see cref="PartyQuery"/>.</returns>
        internal static PartyQuery Get(PartyFieldIncludes includes, PartyQueryFilters filterBy)
        {
            includes |= PartyFieldIncludes.PartyUuid | PartyFieldIncludes.PartyType; // always include the UUID and type

            filterBy.Validate(nameof(filterBy));
            return _queries.GetOrAdd((Includes: includes, FilterBy: filterBy), static (key) => Builder.Create(key.Includes, key.FilterBy));
        }

        private PartyQuery(
            string commandText,
            PartyFields fields,
            FilterParameter paramPartyUuid,
            FilterParameter paramPartyId,
            FilterParameter paramPersonIdentifier,
            FilterParameter paramOrganizationIdentifier,
            FilterParameter paramUserId,
            FilterParameter paramUsername,
            FilterParameter paramPartyUuidList,
            FilterParameter paramPartyIdList,
            FilterParameter paramPersonIdentifierList,
            FilterParameter paramOrganizationIdentifierList,
            FilterParameter paramUserIdList,
            FilterParameter paramUsernameList,
            FilterParameter paramPartyTypeList,
            FilterParameter paramStreamFrom,
            FilterParameter paramStreamLimit)
        {
            CommandText = commandText;
            _fields = fields;
            _paramPartyUuid = paramPartyUuid;
            _paramPartyId = paramPartyId;
            _paramPersonIdentifier = paramPersonIdentifier;
            _paramOrganizationIdentifier = paramOrganizationIdentifier;
            _paramUserId = paramUserId;
            _paramUsername = paramUsername;
            _paramPartyUuidList = paramPartyUuidList;
            _paramPartyIdList = paramPartyIdList;
            _paramPersonIdentifierList = paramPersonIdentifierList;
            _paramOrganizationIdentifierList = paramOrganizationIdentifierList;
            _paramUserIdList = paramUserIdList;
            _paramUsernameList = paramUsernameList;
            _paramPartyTypeList = paramPartyTypeList;
            _paramStreamFromExclusive = paramStreamFrom;
            _paramStreamLimit = paramStreamLimit;

            HasSubUnits = _fields.ParentUuid != -1;
        }

        private readonly PartyFields _fields;
        private readonly FilterParameter _paramPartyUuid;
        private readonly FilterParameter _paramPartyId;
        private readonly FilterParameter _paramPersonIdentifier;
        private readonly FilterParameter _paramOrganizationIdentifier;
        private readonly FilterParameter _paramUserId;
        private readonly FilterParameter _paramUsername;
        private readonly FilterParameter _paramPartyUuidList;
        private readonly FilterParameter _paramPartyIdList;
        private readonly FilterParameter _paramPersonIdentifierList;
        private readonly FilterParameter _paramOrganizationIdentifierList;
        private readonly FilterParameter _paramUserIdList;
        private readonly FilterParameter _paramUsernameList;
        private readonly FilterParameter _paramPartyTypeList;
        private readonly FilterParameter _paramStreamFromExclusive;
        private readonly FilterParameter _paramStreamLimit;

        /// <summary>
        /// Gets the command text for the query.
        /// </summary>
        public string CommandText { get; }

        /// <summary>
        /// Gets whether or not the query has sub-units.
        /// </summary>
        public bool HasSubUnits { get; }

        /// <summary>
        /// Adds a party UUID parameter to the command.
        /// </summary>
        public NpgsqlParameter<Guid> AddPartyUuidParameter(NpgsqlCommand cmd, Guid value)
            => AddParameter(cmd, in _paramPartyUuid, value);

        /// <summary>
        /// Adds a party id parameter to the command.
        /// </summary>
        public NpgsqlParameter<long> AddPartyIdParameter(NpgsqlCommand cmd, long value)
            => AddParameter(cmd, in _paramPartyId, value);

        /// <summary>
        /// Adds a person identifier parameter to the command.
        /// </summary>
        public NpgsqlParameter<string> AddPersonIdentifierParameter(NpgsqlCommand cmd, string value)
            => AddParameter(cmd, in _paramPersonIdentifier, value);

        /// <summary>
        /// Adds a organization identifier parameter to the command.
        /// </summary>
        public NpgsqlParameter<string> AddOrganizationIdentifierParameter(NpgsqlCommand cmd, string value)
            => AddParameter(cmd, in _paramOrganizationIdentifier, value);

        /// <summary>
        /// Adds a user id parameter to the command.
        /// </summary>
        public NpgsqlParameter<long> AddUserIdParameter(NpgsqlCommand cmd, long value)
            => AddParameter(cmd, in _paramUserId, value);

        /// <summary>
        /// Adds a username parameter to the command.
        /// </summary>
        public NpgsqlParameter<string> AddUsernameParameter(NpgsqlCommand cmd, string value)
            => AddParameter(cmd, in _paramUsername, value);

        /// <summary>
        /// Adds a party UUID list parameter to the command.
        /// </summary>
        public NpgsqlParameter<IList<Guid>> AddPartyUuidListParameter(NpgsqlCommand cmd, IList<Guid> value)
            => AddParameter(cmd, in _paramPartyUuidList, value);

        /// <summary>
        /// Adds a party id list parameter to the command.
        /// </summary>
        public NpgsqlParameter<IList<long>> AddPartyIdListParameter(NpgsqlCommand cmd, IList<long> value)
            => AddParameter(cmd, in _paramPartyIdList, value);

        /// <summary>
        /// Adds a person identifier list parameter to the command.
        /// </summary>
        public NpgsqlParameter<IList<string>> AddPersonIdentifierListParameter(NpgsqlCommand cmd, IList<string> value)
            => AddParameter(cmd, in _paramPersonIdentifierList, value);

        /// <summary>
        /// Adds a organization identifier list parameter to the command.
        /// </summary>
        public NpgsqlParameter<IList<string>> AddOrganizationIdentifierListParameter(NpgsqlCommand cmd, IList<string> value)
            => AddParameter(cmd, in _paramOrganizationIdentifierList, value);

        /// <summary>
        /// Adds a user id list parameter to the command.
        /// </summary>
        public NpgsqlParameter<IList<long>> AddUserIdListParameter(NpgsqlCommand cmd, IList<long> value)
            => AddParameter(cmd, in _paramUserIdList, value);

        /// <summary>
        /// Adds a username list parameter to the command.
        /// </summary>
        public NpgsqlParameter<IList<string>> AddUsernameListParameter(NpgsqlCommand cmd, IList<string> value)
            => AddParameter(cmd, in _paramUsernameList, value);

        /// <summary>
        /// Adds a party type list parameter to the command.
        /// </summary>
        public NpgsqlParameter<IList<PartyRecordType>> AddPartyTypeListParameter(NpgsqlCommand cmd, IList<PartyRecordType> value)
            => AddParameter(cmd, in _paramPartyTypeList, value);

        /// <summary>
        /// Adds stream page parameters to the command.
        /// </summary>
        public (NpgsqlParameter<long> From, NpgsqlParameter<int> Limit) AddStreamPageParameters(NpgsqlCommand cmd, ulong fromExclusive, ushort limit)
        {
            var fromParam = AddParameter(cmd, in _paramStreamFromExclusive, (long)fromExclusive);
            var limitParam = AddParameter(cmd, in _paramStreamLimit, (int)limit);

            return (fromParam, limitParam);
        }

        private NpgsqlParameter<T> AddParameter<T>(NpgsqlCommand cmd, in FilterParameter config, T value)
        {
            Debug.Assert(config.HasValue, "Parameter must be configured");
            Debug.Assert(config.Type == typeof(T), "Parameter type mismatch");

            NpgsqlParameter<T> param;

            param = config.DbType.IsDefault()
                ? cmd.Parameters.Add<T>(config.Name)
                : cmd.Parameters.Add<T>(config.Name, config.DbType);

            param.TypedValue = value;
            return param;
        }

        /// <summary>
        /// Reads the parties from a <see cref="NpgsqlCommand"/> asynchronously.
        /// </summary>
        /// <param name="inCmd">The command.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>An async enumerable of <see cref="PartyRecord"/>.</returns>
        public async IAsyncEnumerable<PartyRecord> PrepareAndReadPartiesAsync(NpgsqlCommand inCmd, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Guard.IsNotNull(inCmd);

            await using var cmd = inCmd;

            await cmd.PrepareAsync(cancellationToken);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                yield break;
            }

            PartyRecord party;
            bool hasMore;
            do
            {
                (party, hasMore) = await ReadParty(reader, cancellationToken);
                yield return party;
            } 
            while (hasMore);
        }

        private async ValueTask<(PartyRecord Party, bool HasMore)> ReadParty(NpgsqlDataReader reader, CancellationToken cancellationToken = default)
        {
            var partyType = await reader.GetConditionalFieldValueAsync<PartyRecordType>(_fields.PartyRecordType, cancellationToken);

            return partyType switch
            {
                { HasValue: false } => await ReadBaseParty(reader, _fields, partyType, cancellationToken),
                { Value: PartyRecordType.Person } => await ReadPersonParty(reader, _fields, cancellationToken),
                { Value: PartyRecordType.Organization } => await ReadOrganizationParty(reader, _fields, cancellationToken),
                { Value: PartyRecordType.SelfIdentifiedUser } => await ReadSelfIdentifiedUserParty(reader, _fields, cancellationToken),
                { Value: PartyRecordType.SystemUser } => await ReadSystemUserParty(reader, _fields, cancellationToken),
                { Value: PartyRecordType.EnterpriseUser } => await ReadEnterpriseUserParty(reader, _fields, cancellationToken),
                _ => Unreachable<(PartyRecord Party, bool HasMore)>(),
            };

            static async ValueTask<PartyRecord> ReadCommonFields(NpgsqlDataReader reader, PartyFields fields, CancellationToken cancellationToken)
            {
                return new PartyRecord(FieldValue.Unset)
                {
                    PartyUuid = await reader.GetConditionalFieldValueAsync<Guid>(fields.PartyUuid, cancellationToken),
                    PartyId = await reader.GetConditionalFieldValueAsync<int>(fields.PartyId, cancellationToken).Select(static id => checked((uint)id)),
                    DisplayName = await reader.GetConditionalFieldValueAsync<string>(fields.PartyDisplayName, cancellationToken),
                    PersonIdentifier = await reader.GetConditionalParsableFieldValueAsync<PersonIdentifier>(fields.PartyPersonIdentifier, cancellationToken),
                    OrganizationIdentifier = await reader.GetConditionalParsableFieldValueAsync<OrganizationIdentifier>(fields.PartyOrganizationIdentifier, cancellationToken),
                    CreatedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>(fields.PartyCreated, cancellationToken),
                    ModifiedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>(fields.PartyUpdated, cancellationToken),
                    IsDeleted = await reader.GetConditionalFieldValueAsync<bool>(fields.PartyIsDeleted, cancellationToken),
                    DeletedAt = await reader.GetConditionalFieldValueAsync<DateTimeOffset>(fields.PartyDeletedAt, cancellationToken),
                    VersionId = await reader.GetConditionalFieldValueAsync<long>(fields.PartyVersionId, cancellationToken).Select(static v => (ulong)v),
                    OwnerUuid = await reader.GetConditionalFieldValueAsync<Guid>(fields.PartyOwnerUuid, cancellationToken),

                    // has to be read last as it can be spread over multiple rows
                    User = FieldValue.Unset,
                };
            }

            static async ValueTask<(PartyRecord Party, bool HasMore)> ReadBaseParty(NpgsqlDataReader reader, PartyFields fields, FieldValue<PartyRecordType> partyType, CancellationToken cancellationToken)
            {
                var common = await ReadCommonFields(reader, fields, cancellationToken);

                // must be the last read-access to the reader
                Debug.Assert(common.PartyUuid.HasValue);
                var partyUuid = common.PartyUuid.Value;
                var (user, hasMore) = await ReadUser(reader, partyUuid, fields, cancellationToken);

                var party = new PartyRecord(partyType)
                {
                    PartyUuid = common.PartyUuid,
                    PartyId = common.PartyId,
                    DisplayName = common.DisplayName,
                    PersonIdentifier = common.PersonIdentifier,
                    OrganizationIdentifier = common.OrganizationIdentifier,
                    CreatedAt = common.CreatedAt,
                    ModifiedAt = common.ModifiedAt,
                    IsDeleted = common.IsDeleted,
                    DeletedAt = common.DeletedAt,
                    VersionId = common.VersionId,
                    OwnerUuid = common.OwnerUuid,
                    User = user,
                };

                return (party, hasMore);
            }

            static async ValueTask<(PartyRecord Party, bool HasMore)> ReadPersonParty(NpgsqlDataReader reader, PartyFields fields, CancellationToken cancellationToken)
            {
                var common = await ReadCommonFields(reader, fields, cancellationToken);
                var firstName = await reader.GetConditionalFieldValueAsync<string>(fields.PersonFirstName, cancellationToken);
                var middleName = await reader.GetConditionalFieldValueAsync<string>(fields.PersonMiddleName, cancellationToken);
                var lastName = await reader.GetConditionalFieldValueAsync<string>(fields.PersonLastName, cancellationToken);
                var shortName = await reader.GetConditionalFieldValueAsync<string>(fields.PersonShortName, cancellationToken);
                var dateOfBirth = await reader.GetConditionalFieldValueAsync<DateOnly>(fields.PersonDateOfBirth, cancellationToken);
                var dateOfDeath = await reader.GetConditionalFieldValueAsync<DateOnly>(fields.PersonDateOfDeath, cancellationToken);
                var address = await reader.GetConditionalFieldValueAsync<StreetAddressRecord>(fields.PersonAddress, cancellationToken);
                var mailingAddress = await reader.GetConditionalFieldValueAsync<MailingAddressRecord>(fields.PersonMailingAddress, cancellationToken);

                // must be the last read-access to the reader
                Debug.Assert(common.PartyUuid.HasValue);
                var partyUuid = common.PartyUuid.Value;
                var (user, hasMore) = await ReadUser(reader, partyUuid, fields, cancellationToken);

                var party = new PersonRecord
                {
                    PartyUuid = common.PartyUuid,
                    PartyId = common.PartyId,
                    DisplayName = common.DisplayName,
                    PersonIdentifier = common.PersonIdentifier,
                    OrganizationIdentifier = common.OrganizationIdentifier,
                    CreatedAt = common.CreatedAt,
                    ModifiedAt = common.ModifiedAt,
                    IsDeleted = common.IsDeleted,
                    DeletedAt = common.DeletedAt,
                    VersionId = common.VersionId,
                    OwnerUuid = common.OwnerUuid,
                    User = user,
                    FirstName = firstName,
                    MiddleName = middleName,
                    LastName = lastName,
                    ShortName = shortName,
                    DateOfBirth = dateOfBirth,
                    DateOfDeath = dateOfDeath,
                    Address = address,
                    MailingAddress = mailingAddress,
                };

                return (party, hasMore);
            }

            static async ValueTask<(PartyRecord Party, bool HasMore)> ReadOrganizationParty(NpgsqlDataReader reader, PartyFields fields, CancellationToken cancellationToken)
            {
                var common = await ReadCommonFields(reader, fields, cancellationToken);
                var parentOrganizationUuid = await reader.GetConditionalFieldValueAsync<Guid>(fields.ParentUuid, cancellationToken);
                var unitStatus = await reader.GetConditionalFieldValueAsync<string>(fields.OrganizationUnitStatus, cancellationToken);
                var unitType = await reader.GetConditionalFieldValueAsync<string>(fields.OrganizationUnitType, cancellationToken);
                var telephoneNumber = await reader.GetConditionalFieldValueAsync<string>(fields.OrganizationTelephoneNumber, cancellationToken);
                var mobileNumber = await reader.GetConditionalFieldValueAsync<string>(fields.OrganizationMobileNumber, cancellationToken);
                var faxNumber = await reader.GetConditionalFieldValueAsync<string>(fields.OrganizationFaxNumber, cancellationToken);
                var emailAddress = await reader.GetConditionalFieldValueAsync<string>(fields.OrganizationEmailAddress, cancellationToken);
                var internetAddress = await reader.GetConditionalFieldValueAsync<string>(fields.OrganizationInternetAddress, cancellationToken);
                var mailingAddress = await reader.GetConditionalFieldValueAsync<MailingAddressRecord>(fields.OrganizationMailingAddress, cancellationToken);
                var businessAddress = await reader.GetConditionalFieldValueAsync<MailingAddressRecord>(fields.OrganizationBusinessAddress, cancellationToken);

                if (parentOrganizationUuid.IsNull)
                {
                    parentOrganizationUuid = FieldValue.Unset;
                }

                // must be the last read-access to the reader
                Debug.Assert(common.PartyUuid.HasValue);
                var partyUuid = common.PartyUuid.Value;
                var (user, hasMore) = await ReadUser(reader, partyUuid, fields, cancellationToken);

                var party = new OrganizationRecord
                {
                    PartyUuid = common.PartyUuid,
                    PartyId = common.PartyId,
                    DisplayName = common.DisplayName,
                    PersonIdentifier = common.PersonIdentifier,
                    OrganizationIdentifier = common.OrganizationIdentifier,
                    CreatedAt = common.CreatedAt,
                    ModifiedAt = common.ModifiedAt,
                    IsDeleted = common.IsDeleted,
                    DeletedAt = common.DeletedAt,
                    VersionId = common.VersionId,
                    OwnerUuid = common.OwnerUuid,
                    User = user,
                    UnitStatus = unitStatus,
                    UnitType = unitType,
                    TelephoneNumber = telephoneNumber,
                    MobileNumber = mobileNumber,
                    FaxNumber = faxNumber,
                    EmailAddress = emailAddress,
                    InternetAddress = internetAddress,
                    MailingAddress = mailingAddress,
                    BusinessAddress = businessAddress,
                    ParentOrganizationUuid = parentOrganizationUuid,
                };

                return (party, hasMore);
            }

            static async ValueTask<(PartyRecord Party, bool HasMore)> ReadSelfIdentifiedUserParty(NpgsqlDataReader reader, PartyFields fields, CancellationToken cancellationToken)
            {
                var common = await ReadCommonFields(reader, fields, cancellationToken);

                // must be the last read-access to the reader
                Debug.Assert(common.PartyUuid.HasValue);
                var partyUuid = common.PartyUuid.Value;
                var (user, hasMore) = await ReadUser(reader, partyUuid, fields, cancellationToken);

                var party = new SelfIdentifiedUserRecord
                {
                    PartyUuid = common.PartyUuid,
                    PartyId = common.PartyId,
                    DisplayName = common.DisplayName,
                    PersonIdentifier = common.PersonIdentifier,
                    OrganizationIdentifier = common.OrganizationIdentifier,
                    CreatedAt = common.CreatedAt,
                    ModifiedAt = common.ModifiedAt,
                    IsDeleted = common.IsDeleted,
                    DeletedAt = common.DeletedAt,
                    VersionId = common.VersionId,
                    OwnerUuid = common.OwnerUuid,
                    User = user,
                };
    
                return (party, hasMore);
            }

            static async ValueTask<(PartyRecord Party, bool HasMore)> ReadSystemUserParty(NpgsqlDataReader reader, PartyFields fields, CancellationToken cancellationToken)
            {
                var common = await ReadCommonFields(reader, fields, cancellationToken);
                var systemUserType = await reader.GetConditionalFieldValueAsync<SystemUserRecordType>(fields.SystemUserType, cancellationToken);

                // must be the last read-access to the reader
                Debug.Assert(common.PartyUuid.HasValue);
                var partyUuid = common.PartyUuid.Value;
                var (user, hasMore) = await ReadUser(reader, partyUuid, fields, cancellationToken);

                var party = new SystemUserRecord
                {
                    PartyUuid = common.PartyUuid,
                    PartyId = common.PartyId,
                    DisplayName = common.DisplayName,
                    PersonIdentifier = common.PersonIdentifier,
                    OrganizationIdentifier = common.OrganizationIdentifier,
                    CreatedAt = common.CreatedAt,
                    ModifiedAt = common.ModifiedAt,
                    IsDeleted = common.IsDeleted,
                    DeletedAt = common.DeletedAt,
                    VersionId = common.VersionId,
                    OwnerUuid = common.OwnerUuid,
                    User = user,
                    SystemUserType = systemUserType,
                };

                return (party, hasMore);
            }

            static async ValueTask<(PartyRecord Party, bool HasMore)> ReadEnterpriseUserParty(NpgsqlDataReader reader, PartyFields fields, CancellationToken cancellationToken)
            {
                var common = await ReadCommonFields(reader, fields, cancellationToken);

                // must be the last read-access to the reader
                Debug.Assert(common.PartyUuid.HasValue);
                var partyUuid = common.PartyUuid.Value;
                var (user, hasMore) = await ReadUser(reader, partyUuid, fields, cancellationToken);

                var party = new EnterpriseUserRecord
                {
                    PartyUuid = common.PartyUuid,
                    PartyId = common.PartyId,
                    DisplayName = common.DisplayName,
                    PersonIdentifier = common.PersonIdentifier,
                    OrganizationIdentifier = common.OrganizationIdentifier,
                    CreatedAt = common.CreatedAt,
                    ModifiedAt = common.ModifiedAt,
                    IsDeleted = common.IsDeleted,
                    DeletedAt = common.DeletedAt,
                    VersionId = common.VersionId,
                    OwnerUuid = common.OwnerUuid,
                    User = user,
                };

                return (party, hasMore);
            }

            static async ValueTask<(FieldValue<PartyUserRecord> User, bool HasMore)> ReadUser(NpgsqlDataReader reader, Guid partyUuid, PartyFields fields, CancellationToken cancellationToken)
            {
                bool hasMore;
                var isActive = await reader.GetConditionalFieldValueAsync<bool>(fields.UserIsActive, cancellationToken);
                if (!isActive.HasValue)
                {
                    // no user-information available for this party, or user-information not requested
                    // this means we don't have to aggregate up user-ids
                    hasMore = await reader.ReadAsync(cancellationToken);
                    return (isActive.IsNull ? FieldValue.Null : FieldValue.Unset, hasMore);
                }

                FieldValue<uint> userId = FieldValue.Unset;
                FieldValue<string> username = FieldValue.Unset;
                var userIdsBuilder = ImmutableArray.CreateBuilder<uint>(1);
                if (isActive.Value)
                {
                    // TODO: read userName too
                    userId = await reader.GetConditionalFieldValueAsync<long>(fields.UserId, cancellationToken).Select(static id => checked((uint)id));
                    username = await reader.GetConditionalFieldValueAsync<string>(fields.Username, cancellationToken);

                    if (userId.HasValue)
                    {
                        userIdsBuilder.Add(userId.Value);
                    }
                }

                // aggregate user-ids
                while (true)
                {
                    hasMore = await reader.ReadAsync(cancellationToken);
                    if (!hasMore)
                    {
                        break;
                    }

                    var currentRowPartyUuid = await reader.GetFieldValueAsync<Guid>(fields.PartyUuid, cancellationToken);
                    if (currentRowPartyUuid != partyUuid)
                    {
                        // we are done with this party, move on to the next one
                        break;
                    }

                    var currentRowUserId = await reader.GetConditionalFieldValueAsync<long>(fields.UserId, cancellationToken).Select(static id => checked((uint)id));
                    if (currentRowUserId.HasValue)
                    {
                        userIdsBuilder.Add(currentRowUserId.Value);
                    }
                }

                FieldValue<PartyUserRecord> user;
                if (userIdsBuilder.Count == 0 && !userId.HasValue && !username.HasValue)
                {
                    user = FieldValue.Null;
                }
                else
                {
                    var userIds = userIdsBuilder.DrainToImmutableValueArray();
                    user = new PartyUserRecord(
                        userId: userId,
                        username: username,
                        userIds: userIds);
                }

                return (user, hasMore);
            }

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            static T Unreachable<T>()
                => throw new UnreachableException();
        }

        private sealed class Builder
        {
            public static PartyQuery Create(PartyFieldIncludes includes, PartyQueryFilters filterBy)
            {
                Builder builder = new();
                builder.Populate(includes, filterBy);

                PartyFields fields = new(
                    parentUuid: builder._parentUuid,
                    partyUuid: builder._partyUuid,
                    partyId: builder._partyId,
                    partyType: builder._partyType,
                    partyDisplayName: builder._partyDisplayName,
                    partyPersonIdentifier: builder._partyPersonIdentifier,
                    partyOrganizationIdentifier: builder._partyOrganizationIdentifier,
                    partyCreated: builder._partyCreated,
                    partyUpdated: builder._partyUpdated,
                    partyIsDeleted: builder._partyIsDeleted,
                    partyDeletedAt: builder._partyDeletedAt,
                    partyVersionId: builder._partyVersionId,
                    partyOwnerUuid: builder._partyOwnerUuid,
                    personFirstName: builder._personFirstName,
                    personMiddleName: builder._personMiddleName,
                    personLastName: builder._personLastName,
                    personShortName: builder._personShortName,
                    personDateOfBirth: builder._personDateOfBirth,
                    personDateOfDeath: builder._personDateOfDeath,
                    personAddress: builder._personAddress,
                    personMailingAddress: builder._personMailingAddress,
                    organizationUnitStatus: builder._organizationUnitStatus,
                    organizationUnitType: builder._organizationUnitType,
                    organizationTelephoneNumber: builder._organizationTelephoneNumber,
                    organizationMobileNumber: builder._organizationMobileNumber,
                    organizationFaxNumber: builder._organizationFaxNumber,
                    organizationEmailAddress: builder._organizationEmailAddress,
                    organizationInternetAddress: builder._organizationInternetAddress,
                    organizationMailingAddress: builder._organizationMailingAddress,
                    organizationBusinessAddress: builder._organizationBusinessAddress,
                    systemUserType: builder._systemUserType,
                    userIsActive: builder._userIsActive,
                    userId: builder._userId,
                    username: builder._username);

                var commandText = builder._builder.ToString();
                return new(
                    commandText,
                    fields,
                    paramPartyUuid: builder._paramPartyUuid,
                    paramPartyId: builder._paramPartyId,
                    paramPersonIdentifier: builder._paramPersonIdentifier,
                    paramOrganizationIdentifier: builder._paramOrganizationIdentifier,
                    paramUserId: builder._paramUserId,
                    paramUsername: builder._paramUsername,
                    paramPartyUuidList: builder._paramPartyUuidList,
                    paramPartyIdList: builder._paramPartyIdList,
                    paramPersonIdentifierList: builder._paramPersonIdentifierList,
                    paramOrganizationIdentifierList: builder._paramOrganizationIdentifierList,
                    paramUserIdList: builder._paramUserIdList,
                    paramUsernameList: builder._paramUsernameList,
                    paramPartyTypeList: builder._paramPartyTypeList,
                    paramStreamFrom: builder._paramStreamFromExclusive,
                    paramStreamLimit: builder._paramStreamLimit);
            }

            private readonly StringBuilder _builder = new();

            // parameters
            private FilterParameter _paramPartyUuid;
            private FilterParameter _paramPartyId;
            private FilterParameter _paramPersonIdentifier;
            private FilterParameter _paramOrganizationIdentifier;
            private FilterParameter _paramUserId;
            private FilterParameter _paramUsername;
            private FilterParameter _paramPartyUuidList;
            private FilterParameter _paramPartyIdList;
            private FilterParameter _paramPersonIdentifierList;
            private FilterParameter _paramOrganizationIdentifierList;
            private FilterParameter _paramUserIdList;
            private FilterParameter _paramUsernameList;
            private FilterParameter _paramPartyTypeList;
            private FilterParameter _paramStreamFromExclusive;
            private FilterParameter _paramStreamLimit;

            // fields
            private sbyte _fieldIndex = 0;

            // meta fields
            private sbyte _parentUuid = -1;

            // register.party
            private sbyte _partyUuid = -1;
            private sbyte _partyId = -1;
            private sbyte _partyType = -1;
            private sbyte _partyDisplayName = -1;
            private sbyte _partyPersonIdentifier = -1;
            private sbyte _partyOrganizationIdentifier = -1;
            private sbyte _partyCreated = -1;
            private sbyte _partyUpdated = -1;
            private sbyte _partyIsDeleted = -1;
            private sbyte _partyDeletedAt = -1;
            private sbyte _partyVersionId = -1;
            private sbyte _partyOwnerUuid = -1;

            // register.person
            private sbyte _personFirstName = -1;
            private sbyte _personMiddleName = -1;
            private sbyte _personLastName = -1;
            private sbyte _personShortName = -1;
            private sbyte _personDateOfBirth = -1;
            private sbyte _personDateOfDeath = -1;
            private sbyte _personAddress = -1;
            private sbyte _personMailingAddress = -1;

            // register.organization
            private sbyte _organizationUnitStatus = -1;
            private sbyte _organizationUnitType = -1;
            private sbyte _organizationTelephoneNumber = -1;
            private sbyte _organizationMobileNumber = -1;
            private sbyte _organizationFaxNumber = -1;
            private sbyte _organizationEmailAddress = -1;
            private sbyte _organizationInternetAddress = -1;
            private sbyte _organizationMailingAddress = -1;
            private sbyte _organizationBusinessAddress = -1;

            // register.system_user
            private sbyte _systemUserType = -1;

            // register.user
            private sbyte _userIsActive = -1;
            private sbyte _userId = -1;
            private sbyte _username = -1;

            public void Populate(PartyFieldIncludes includes, PartyQueryFilters filterBy)
            {
                PopulateCommonTableExpressions(includes, filterBy);

                _builder.Append(/*strpsql*/"SELECT");

                _parentUuid = AddField("uuids.parent_uuid", "p_parent_uuid", include: includes.HasFlag(PartyFieldIncludes.SubUnits));

                _partyUuid = AddField("party.uuid", "p_uuid", includes.HasFlag(PartyFieldIncludes.PartyUuid));
                _partyId = AddField("party.id", "p_id", includes.HasFlag(PartyFieldIncludes.PartyId));
                _partyType = AddField("party.party_type", "p_party_type", includes.HasFlag(PartyFieldIncludes.PartyType));
                _partyDisplayName = AddField("party.display_name", "p_display_name", includes.HasFlag(PartyFieldIncludes.PartyDisplayName));
                _partyPersonIdentifier = AddField("party.person_identifier", "p_person_identifier", includes.HasFlag(PartyFieldIncludes.PartyPersonIdentifier));
                _partyOrganizationIdentifier = AddField("party.organization_identifier", "p_organization_identifier", includes.HasFlag(PartyFieldIncludes.PartyOrganizationIdentifier));
                _partyCreated = AddField("party.created", "p_created", includes.HasFlag(PartyFieldIncludes.PartyCreatedAt));
                _partyUpdated = AddField("party.updated", "p_updated", includes.HasFlag(PartyFieldIncludes.PartyModifiedAt));
                _partyIsDeleted = AddField("party.is_deleted", "p_is_deleted", includes.HasFlag(PartyFieldIncludes.PartyIsDeleted));
                _partyDeletedAt = AddField("party.deleted_at", "p_deleted_at", includes.HasFlag(PartyFieldIncludes.PartyDeletedAt));
                _partyVersionId = AddField("party.version_id", "p_version_id", includes.HasFlag(PartyFieldIncludes.PartyVersionId));
                _partyOwnerUuid = AddField("party.\"owner\"", "p_owner_uuid", includes.HasFlag(PartyFieldIncludes.PartyOwnerUuid));

                _personFirstName = AddField("person.first_name", "p_first_name", includes.HasFlag(PartyFieldIncludes.PersonFirstName));
                _personMiddleName = AddField("person.middle_name", "p_middle_name", includes.HasFlag(PartyFieldIncludes.PersonMiddleName));
                _personLastName = AddField("person.last_name", "p_last_name", includes.HasFlag(PartyFieldIncludes.PersonLastName));
                _personShortName = AddField("person.short_name", "p_short_name", includes.HasFlag(PartyFieldIncludes.PersonShortName));
                _personDateOfBirth = AddField("person.date_of_birth", "p_date_of_birth", includes.HasFlag(PartyFieldIncludes.PersonDateOfBirth));
                _personDateOfDeath = AddField("person.date_of_death", "p_date_of_death", includes.HasFlag(PartyFieldIncludes.PersonDateOfDeath));
                _personAddress = AddField("person.address", "p_address", includes.HasFlag(PartyFieldIncludes.PersonAddress));
                _personMailingAddress = AddField("person.mailing_address", "p_person_mailing_address", includes.HasFlag(PartyFieldIncludes.PersonMailingAddress));

                _organizationUnitStatus = AddField("org.unit_status", "p_unit_status", includes.HasFlag(PartyFieldIncludes.OrganizationUnitStatus));
                _organizationUnitType = AddField("org.unit_type", "p_unit_type", includes.HasFlag(PartyFieldIncludes.OrganizationUnitType));
                _organizationTelephoneNumber = AddField("org.telephone_number", "p_telephone_number", includes.HasFlag(PartyFieldIncludes.OrganizationTelephoneNumber));
                _organizationMobileNumber = AddField("org.mobile_number", "p_mobile_number", includes.HasFlag(PartyFieldIncludes.OrganizationMobileNumber));
                _organizationFaxNumber = AddField("org.fax_number", "p_fax_number", includes.HasFlag(PartyFieldIncludes.OrganizationFaxNumber));
                _organizationEmailAddress = AddField("org.email_address", "p_email_address", includes.HasFlag(PartyFieldIncludes.OrganizationEmailAddress));
                _organizationInternetAddress = AddField("org.internet_address", "p_internet_address", includes.HasFlag(PartyFieldIncludes.OrganizationInternetAddress));
                _organizationMailingAddress = AddField("org.mailing_address", "p_org_mailing_address", includes.HasFlag(PartyFieldIncludes.OrganizationMailingAddress));
                _organizationBusinessAddress = AddField("org.business_address", "p_business_address", includes.HasFlag(PartyFieldIncludes.OrganizationBusinessAddress));

                _systemUserType = AddField("sys_u.\"type\"", "p_system_user_type", includes.HasFlag(PartyFieldIncludes.SystemUserType));

                _userIsActive = AddField("\"user\".is_active", "u_is_active", includes.HasAnyFlags(PartyFieldIncludes.User));
                _userId = AddField("\"user\".user_id", "u_user_id", includes.HasFlag(PartyFieldIncludes.UserId));
                _username = AddField("\"user\".username", "u_username", includes.HasFlag(PartyFieldIncludes.Username));

                _builder.AppendLine().Append(/*strpsql*/"FROM uuids AS uuids");
                _builder.AppendLine().Append(/*strpsql*/"INNER JOIN register.party AS party USING (uuid)");

                if (includes.HasAnyFlags(PartyFieldIncludes.Person))
                {
                    _builder.AppendLine().Append(/*strpsql*/"LEFT JOIN register.person AS person USING (uuid)");
                }

                if (includes.HasAnyFlags(PartyFieldIncludes.Organization))
                {
                    _builder.AppendLine().Append(/*strpsql*/"LEFT JOIN register.organization AS org USING (uuid)");
                }

                if (includes.HasAnyFlags(PartyFieldIncludes.SystemUser))
                {
                    _builder.AppendLine().Append(/*strpsql*/"""LEFT JOIN register.system_user AS sys_u USING (uuid)""");
                }

                if (includes.HasAnyFlags(PartyFieldIncludes.User))
                {
                    _builder.AppendLine().Append(/*strpsql*/"""LEFT JOIN filtered_users AS "user" USING (uuid)""");
                }

                _builder.AppendLine().AppendLine(/*strpsql*/"ORDER BY").Append(/*strpsql*/"    uuids.sort_first");
                _builder.AppendLine(",").Append(/*strpsql*/"    uuids.sort_second NULLS FIRST");
                if (includes.HasAnyFlags(PartyFieldIncludes.User))
                {
                    _builder.AppendLine(",").Append(/*strpsql*/"""    "user".is_active DESC""");
                    _builder.AppendLine(",").Append(/*strpsql*/"""    "user".user_id DESC""");
                }
            }

            private void PopulateCommonTableExpressions(PartyFieldIncludes includes, PartyQueryFilters filterBy)
            {
                const string TOP_LEVEL_UUIDS = "top_level_uuids";
                const string TOP_LEVEL_UNFILTERED = "top_level_uuids_unfiltered";

                var firstExpression = true;
                switch (filterBy.Mode)
                {
                    case PartyQueryFilters.QueryMode.LookupOne:
                        PopulateLookupOneCommonTableExpression(TOP_LEVEL_UUIDS, filterBy.LookupIdentifiers, ref firstExpression);
                        break;

                    case PartyQueryFilters.QueryMode.LookupMultiple:
                        var hasFilters = filterBy.ListFilters is not PartyListFilters.None;
                        var cteName = !hasFilters ? TOP_LEVEL_UUIDS : TOP_LEVEL_UNFILTERED;
                        PopulateLookupMultipleCommonTableExpression(cteName, filterBy.LookupIdentifiers, ref firstExpression);
                        if (hasFilters)
                        {
                            PopulateListFilterCommonTableExpression(TOP_LEVEL_UUIDS, source: TOP_LEVEL_UNFILTERED, filterBy.ListFilters, streamPage: false, ref firstExpression);
                        }

                        break;

                    case PartyQueryFilters.QueryMode.FilteredStream:
                        PopulateListFilterCommonTableExpression(TOP_LEVEL_UUIDS, source: null, filterBy.ListFilters, streamPage: true, ref firstExpression);
                        break;

                    default:
                        ThrowHelper.ThrowArgumentOutOfRangeException(nameof(filterBy), filterBy, "Unsupported filter mode");
                        break;
                }

                if (includes.HasAnyFlags(PartyFieldIncludes.User))
                {
                    switch ((filterBy.LookupIdentifiers.HasFlag(PartyLookupIdentifiers.UserId), filterBy.Mode))
                    {
                        case (false, _):
                            AddCommonTableExpression(
                                ref firstExpression,
                                "filtered_users",
                                /*strpsql*/"""
                                SELECT "user".*
                                FROM register."user" AS "user"
                                WHERE "user".is_active
                                """);
                            break;

                        case (true, PartyQueryFilters.QueryMode.LookupOne):
                            AddCommonTableExpression(
                                ref firstExpression,
                                "filtered_users",
                                /*strpsql*/"""
                                SELECT "user".*
                                FROM register."user" AS "user"
                                WHERE "user".is_active
                                   OR "user".user_id = @userId
                                """);
                            break;

                        default:
                            Debug.Assert(_paramUserIdList.HasValue);
                            AddCommonTableExpression(
                                ref firstExpression,
                                "filtered_users",
                                /*strpsql*/"""
                                SELECT "user".*
                                FROM register."user" AS "user"
                                WHERE "user".is_active
                                   OR "user".user_id = ANY (@userIds)
                                """);
                            break;
                    }
                }

                if (includes.HasFlag(PartyFieldIncludes.SubUnits))
                {
                    AddCommonTableExpression(
                        ref firstExpression,
                        "sub_units",
                        /*strpsql*/"""
                        SELECT
                            parent."uuid" AS parent_uuid,
                            parent.version_id AS parent_version_id,
                            ra."from_party" AS child_uuid
                        FROM top_level_uuids AS parent
                        JOIN register.external_role_assignment ra
                             ON ra.to_party = parent."uuid"
                            AND ra.source = 'ccr'
                            AND (ra.identifier = 'ikke-naeringsdrivende-hovedenhet' OR ra.identifier = 'hovedenhet')
                        """);

                    AddCommonTableExpression(
                        ref firstExpression,
                        "uuids",
                        /*strpsql*/"""
                        SELECT
                            "uuid" AS "uuid",
                            NULL::uuid AS parent_uuid,
                            version_id AS sort_first,
                            NULL::uuid AS sort_second
                        FROM top_level_uuids
                        UNION
                        SELECT 
                            child_uuid AS "uuid",
                            parent_uuid,
                            parent_version_id AS sort_first,
                            child_uuid AS sort_second
                        FROM sub_units
                        """);
                }
                else
                {
                    AddCommonTableExpression(
                        ref firstExpression,
                        "uuids",
                        /*strpsql*/"""
                        SELECT
                            "uuid" AS "uuid",
                            NULL::uuid AS parent_uuid,
                            version_id AS sort_first,
                            NULL::uuid AS sort_second
                        FROM top_level_uuids
                        """);
                }

                _builder.AppendLine();
            }

            private void PopulateLookupOneCommonTableExpression(string name, PartyLookupIdentifiers identifier, ref bool firstExpression)
            {
                // if we are not filtering on multiple values, we only allow a single filter type
                switch (identifier)
                {
                    case PartyLookupIdentifiers.PartyUuid:
                        _paramPartyUuid = new(typeof(Guid), "partyUuid", NpgsqlDbType.Uuid);
                        AddCommonTableExpression(
                            ref firstExpression,
                            name,
                            /*strpsql*/"""
                                SELECT party."uuid", party.version_id
                                FROM register.party AS party
                                WHERE party."uuid" = @partyUuid
                                """);
                        break;

                    case PartyLookupIdentifiers.PartyId:
                        _paramPartyId = new(typeof(long), "partyId", NpgsqlDbType.Bigint);
                        AddCommonTableExpression(
                            ref firstExpression,
                            name,
                            /*strpsql*/"""
                                SELECT party."uuid", party.version_id
                                FROM register.party AS party
                                WHERE party."id" = @partyId
                                """);
                        break;

                    case PartyLookupIdentifiers.PersonIdentifier:
                        _paramPersonIdentifier = new(typeof(string), "personIdentifier", NpgsqlDbType.Text);
                        AddCommonTableExpression(
                            ref firstExpression,
                            name,
                            /*strpsql*/"""
                                SELECT party."uuid", party.version_id
                                FROM register.party AS party
                                WHERE party."person_identifier" = @personIdentifier
                                """);
                        break;

                    case PartyLookupIdentifiers.OrganizationIdentifier:
                        _paramOrganizationIdentifier = new(typeof(string), "organizationIdentifier", NpgsqlDbType.Text);
                        AddCommonTableExpression(
                            ref firstExpression,
                            name,
                            /*strpsql*/"""
                                SELECT party."uuid", party.version_id
                                FROM register.party AS party
                                WHERE party."organization_identifier" = @organizationIdentifier
                                """);
                        break;

                    case PartyLookupIdentifiers.UserId:
                        _paramUserId = new(typeof(long), "userId", NpgsqlDbType.Bigint);
                        AddCommonTableExpression(
                            ref firstExpression,
                            name,
                            /*strpsql*/"""
                                SELECT "user"."uuid", party.version_id
                                FROM register."user" AS "user"
                                INNER JOIN register.party AS party USING (uuid)
                                WHERE "user".user_id = @userId
                                """);
                        break;

                    case PartyLookupIdentifiers.Username:
                        _paramUsername = new(typeof(string), "username", NpgsqlDbType.Text);
                        AddCommonTableExpression(
                            ref firstExpression,
                            name,
                            /*strpsql*/"""
                                SELECT "user"."uuid", party.version_id
                                FROM register."user" AS "user"
                                INNER JOIN register.party AS party USING (uuid)
                                WHERE "user".username = @username
                                """);
                        break;
                }

                Debug.Assert(!firstExpression);
            }

            private void PopulateLookupMultipleCommonTableExpression(string name, PartyLookupIdentifiers identifier, ref bool firstExpression)
            {
                var idSets = new List<string>();

                if (identifier.HasFlag(PartyLookupIdentifiers.PartyUuid))
                {
                    // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
                    _paramPartyUuidList = new(typeof(IList<Guid>), "partyUuids", NpgsqlDbType.Array | NpgsqlDbType.Uuid);
                    idSets.Add("uuids_by_party_uuid");
                    AddCommonTableExpression(
                        ref firstExpression,
                        "uuids_by_party_uuid",
                        /*strpsql*/"""
                            SELECT party."uuid", party.version_id
                            FROM register.party AS party
                            WHERE party."uuid" = ANY (@partyUuids)
                            """);
                }

                if (identifier.HasFlag(PartyLookupIdentifiers.PartyId))
                {
                    // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
                    _paramPartyIdList = new(typeof(IList<long>), "partyIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint);
                    idSets.Add("uuids_by_party_id");
                    AddCommonTableExpression(
                        ref firstExpression,
                        "uuids_by_party_id",
                        /*strpsql*/"""
                            SELECT party."uuid", party.version_id
                            FROM register.party AS party
                            WHERE party."id" = ANY (@partyIds)
                            """);
                }

                if (identifier.HasFlag(PartyLookupIdentifiers.PersonIdentifier))
                {
                    // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
                    _paramPersonIdentifierList = new(typeof(IList<string>), "personIdentifiers", NpgsqlDbType.Array | NpgsqlDbType.Text);
                    idSets.Add("uuids_by_person_identifier");
                    AddCommonTableExpression(
                        ref firstExpression,
                        "uuids_by_person_identifier",
                        /*strpsql*/"""
                            SELECT party."uuid", party.version_id
                            FROM register.party AS party
                            WHERE party."person_identifier" = ANY (@personIdentifiers)
                            """);
                }

                if (identifier.HasFlag(PartyLookupIdentifiers.OrganizationIdentifier))
                {
                    // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
                    _paramOrganizationIdentifierList = new(typeof(IList<string>), "organizationIdentifiers", NpgsqlDbType.Array | NpgsqlDbType.Text);
                    idSets.Add("uuids_by_organization_identifier");
                    AddCommonTableExpression(
                        ref firstExpression,
                        "uuids_by_organization_identifier",
                        /*strpsql*/"""
                            SELECT party."uuid", party.version_id
                            FROM register.party AS party
                            WHERE party."organization_identifier" = ANY (@organizationIdentifiers)
                            """);
                }

                if (identifier.HasFlag(PartyLookupIdentifiers.UserId))
                {
                    // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
                    _paramUserIdList = new(typeof(IList<long>), "userIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint);
                    idSets.Add("uuids_by_user_id");
                    AddCommonTableExpression(
                        ref firstExpression,
                        "uuids_by_user_id",
                        /*strpsql*/"""
                            SELECT "user"."uuid", party.version_id
                            FROM register."user" AS "user"
                            INNER JOIN register.party AS party USING (uuid)
                            WHERE "user".user_id = ANY (@userIds)
                            """);
                }

                if (identifier.HasFlag(PartyLookupIdentifiers.Username))
                {
                    // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
                    _paramUsernameList = new(typeof(IList<string>), "usernames", NpgsqlDbType.Array | NpgsqlDbType.Text);
                    idSets.Add("uuids_by_username");
                    AddCommonTableExpression(
                        ref firstExpression,
                        "uuids_by_username",
                        /*strpsql*/"""
                            SELECT "user"."uuid", party.version_id
                            FROM register."user" AS "user"
                            INNER JOIN register.party AS party USING (uuid)
                            WHERE "user".username = ANY (@usernames)
                            """);
                }

                Debug.Assert(!firstExpression);
                Debug.Assert(idSets.Count > 0, "No filters were added, but multiple filters were requested");

                _builder.AppendLine(",").Append(name).AppendLine(/*strpsql*/" AS (");
                for (int i = 0, l = idSets.Count; i < l; i++)
                {
                    if (i != 0)
                    {
                        _builder.AppendLine(/*strpsql*/"    UNION");
                    }

                    _builder.Append(/*strpsql*/"""    SELECT "uuid", version_id FROM """).AppendLine(idSets[i]);
                }

                _builder.Append(')');
            }

            private void PopulateListFilterCommonTableExpression(string name, string? source, PartyListFilters filters, bool streamPage, ref bool firstExpression)
            {
                Debug.Assert(filters is not PartyListFilters.None || streamPage);
                if (streamPage)
                {
                    _paramStreamFromExclusive = new(typeof(long), "streamFromExclusive", NpgsqlDbType.Bigint);
                    _paramStreamLimit = new(typeof(int), "streamLimit", NpgsqlDbType.Integer);
                    AddCommonTableExpression(
                        ref firstExpression,
                        "maxval",
                        /*strpsql*/"""
                        SELECT register.tx_max_safeval('register.party_version_id_seq') maxval
                        """);
                }

                if (!firstExpression)
                {
                    _builder.AppendLine(",");
                }

                firstExpression = false;
                _builder.Append(name).AppendLine(/*strpsql*/" AS (");
                _builder.AppendLine(/*strpsql*/"    SELECT party.\"uuid\", party.version_id");
                if (source is null)
                {
                    _builder.AppendLine(/*strpsql*/"    FROM register.party AS party");
                }
                else
                {
                    _builder.Append(/*strpsql*/"    FROM ").Append(source).AppendLine(/*strpsql*/" AS source");
                    _builder.AppendLine(/*strpsql*/"    INNER JOIN register.party AS party USING (uuid)");
                }

                var whereAdded = false;
                if (streamPage)
                {
                    _builder.AppendLine(/*strpsql*/"    CROSS JOIN maxval mv");
                    _builder.AppendLine(/*strpsql*/"    WHERE party.version_id > @streamFromExclusive");
                    _builder.AppendLine(/*strpsql*/"      AND party.version_id <= mv.maxval");
                    whereAdded = true;
                }

                if (filters.HasFlag(PartyListFilters.PartyType))
                {
                    _paramPartyTypeList = new(typeof(IList<PartyRecordType>), "partyTypes", default);
                    var kw = whereAdded ? "  AND" : "WHERE";
                    whereAdded = true;

                    _builder.Append("    ").Append(kw).AppendLine(/*strpsql*/" party.party_type = ANY (@partyTypes)");
                }

                if (streamPage)
                {
                    _builder.AppendLine(/*strpsql*/"    ORDER BY party.version_id ASC");
                    _builder.AppendLine(/*strpsql*/"    LIMIT @streamLimit");
                }

                _builder.Append(')');
            }

            private void AddCommonTableExpression(ref bool firstExpression, string name, string query)
            {
                if (firstExpression)
                {
                    _builder.Append(/*strpsql*/"WITH ");
                    firstExpression = false;
                }
                else
                {
                    _builder.AppendLine(/*strpsql*/",");
                }

                _builder.Append(name).AppendLine(/*strpsql*/" AS (");
                foreach (var line in query.AsSpan().EnumerateLines())
                {
                    _builder.Append("    ").Append(line).AppendLine();
                }

                _builder.Append(')');
            }

            private sbyte AddField(string sourceSql, string fieldAlias, bool include)
            {
                if (!include)
                {
                    return -1;
                }

                if (_fieldIndex > 0)
                {
                    _builder.Append(',');
                }

                _builder.AppendLine();
                _builder.Append("    ").Append(sourceSql).Append(' ').Append(fieldAlias);

                return _fieldIndex++;
            }
        }

        private readonly struct FilterParameter(
            Type type,
            string name,
            NpgsqlDbType dbType)
        {
            [MemberNotNullWhen(true, nameof(Type))]
            [MemberNotNullWhen(true, nameof(Name))]
            public bool HasValue => Type is not null;

            public Type? Type => type;

            public string? Name => name;

            public NpgsqlDbType DbType => dbType;
        }

        [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1515:Single-line comment should be preceded by blank line", Justification = "This rule makes no sense here")]
        private class PartyFields(
            // meta fields
            sbyte parentUuid,

            // register.party
            sbyte partyUuid,
            sbyte partyId,
            sbyte partyType,
            sbyte partyDisplayName,
            sbyte partyPersonIdentifier,
            sbyte partyOrganizationIdentifier,
            sbyte partyCreated,
            sbyte partyUpdated,
            sbyte partyIsDeleted,
            sbyte partyDeletedAt,
            sbyte partyVersionId,
            sbyte partyOwnerUuid,

            // register.person
            sbyte personFirstName,
            sbyte personMiddleName,
            sbyte personLastName,
            sbyte personShortName,
            sbyte personDateOfBirth,
            sbyte personDateOfDeath,
            sbyte personAddress,
            sbyte personMailingAddress,

            // register.organization
            sbyte organizationUnitStatus,
            sbyte organizationUnitType,
            sbyte organizationTelephoneNumber,
            sbyte organizationMobileNumber,
            sbyte organizationFaxNumber,
            sbyte organizationEmailAddress,
            sbyte organizationInternetAddress,
            sbyte organizationMailingAddress,
            sbyte organizationBusinessAddress,

            // register.system_user
            sbyte systemUserType,

            // register.user
            sbyte userIsActive,
            sbyte userId,
            sbyte username)
        {
            // meta field
            public int ParentUuid => parentUuid;

            // register.party
            public int PartyUuid => partyUuid;
            public int PartyId => partyId;
            public int PartyRecordType => partyType;
            public int PartyDisplayName => partyDisplayName;
            public int PartyPersonIdentifier => partyPersonIdentifier;
            public int PartyOrganizationIdentifier => partyOrganizationIdentifier;
            public int PartyCreated => partyCreated;
            public int PartyUpdated => partyUpdated;
            public int PartyIsDeleted => partyIsDeleted;
            public int PartyDeletedAt => partyDeletedAt;
            public int PartyVersionId => partyVersionId;
            public int PartyOwnerUuid => partyOwnerUuid;

            // register.person
            public int PersonFirstName => personFirstName;
            public int PersonMiddleName => personMiddleName;
            public int PersonLastName => personLastName;
            public int PersonShortName => personShortName;
            public int PersonDateOfBirth => personDateOfBirth;
            public int PersonDateOfDeath => personDateOfDeath;
            public int PersonAddress => personAddress;
            public int PersonMailingAddress => personMailingAddress;

            // register.organization
            public int OrganizationUnitStatus => organizationUnitStatus;
            public int OrganizationUnitType => organizationUnitType;
            public int OrganizationTelephoneNumber => organizationTelephoneNumber;
            public int OrganizationMobileNumber => organizationMobileNumber;
            public int OrganizationFaxNumber => organizationFaxNumber;
            public int OrganizationEmailAddress => organizationEmailAddress;
            public int OrganizationInternetAddress => organizationInternetAddress;
            public int OrganizationMailingAddress => organizationMailingAddress;
            public int OrganizationBusinessAddress => organizationBusinessAddress;

            // register.system_user
            public int SystemUserType => systemUserType;

            // register.user
            public int UserIsActive => userIsActive;
            public int UserId => userId;
            public int Username => username;
        }
    }
}
