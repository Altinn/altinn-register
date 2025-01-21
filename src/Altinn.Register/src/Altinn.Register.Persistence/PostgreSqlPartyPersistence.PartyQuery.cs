using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.Utils;
using CommunityToolkit.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence;

/// <content>
/// Contains the party query builder.
/// </content>
internal partial class PostgreSqlPartyPersistence
{
    [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1516:Elements should be separated by blank line", Justification = "This class is long enough already")]
    private sealed class PartyQuery
    {
        private static ImmutableDictionary<(PartyFieldIncludes Includes, PartyFilters FilterBy), PartyQuery> _queries
            = ImmutableDictionary<(PartyFieldIncludes Includes, PartyFilters FilterBy), PartyQuery>.Empty;

        public static PartyQuery Get(PartyFieldIncludes includes, PartyFilters filterBy)
        {
            includes |= PartyFieldIncludes.PartyUuid | PartyFieldIncludes.PartyType; // always include the UUID and type

            return ImmutableInterlocked.GetOrAdd(ref _queries, (Includes: includes, FilterBy: filterBy), static (key) => Builder.Create(key.Includes, key.FilterBy));
        }

        private PartyQuery(
            string commandText,
            PartyFields parentFields,
            PartyFields? childField,
            FilterParameter paramPartyUuid,
            FilterParameter paramPartyId,
            FilterParameter paramPersonIdentifier,
            FilterParameter paramOrganizationIdentifier,
            FilterParameter paramPartyUuidList,
            FilterParameter paramPartyIdList,
            FilterParameter paramPersonIdentifierList,
            FilterParameter paramOrganizationIdentifierList,
            FilterParameter paramStreamFrom,
            FilterParameter paramStreamLimit)
        {
            CommandText = commandText;
            _parentFields = parentFields;
            _childFields = childField ?? default;
            _paramPartyUuid = paramPartyUuid;
            _paramPartyId = paramPartyId;
            _paramPersonIdentifier = paramPersonIdentifier;
            _paramOrganizationIdentifier = paramOrganizationIdentifier;
            _paramPartyUuidList = paramPartyUuidList;
            _paramPartyIdList = paramPartyIdList;
            _paramPersonIdentifierList = paramPersonIdentifierList;
            _paramOrganizationIdentifierList = paramOrganizationIdentifierList;
            _paramStreamFrom = paramStreamFrom;
            _paramStreamLimit = paramStreamLimit;

            HasSubUnits = childField.HasValue;
        }

        private readonly PartyFields _parentFields;
        private readonly PartyFields _childFields;
        private readonly FilterParameter _paramPartyUuid;
        private readonly FilterParameter _paramPartyId;
        private readonly FilterParameter _paramPersonIdentifier;
        private readonly FilterParameter _paramOrganizationIdentifier;
        private readonly FilterParameter _paramPartyUuidList;
        private readonly FilterParameter _paramPartyIdList;
        private readonly FilterParameter _paramPersonIdentifierList;
        private readonly FilterParameter _paramOrganizationIdentifierList;
        private readonly FilterParameter _paramStreamFrom;
        private readonly FilterParameter _paramStreamLimit;

        public string CommandText { get; }

        public bool HasSubUnits { get; }

        public NpgsqlParameter<Guid> AddPartyUuidParameter(NpgsqlCommand cmd, Guid value)
            => AddParameter(cmd, in _paramPartyUuid, value);

        public NpgsqlParameter<int> AddPartyIdParameter(NpgsqlCommand cmd, int value)
            => AddParameter(cmd, in _paramPartyId, value);

        public NpgsqlParameter<string> AddPersonIdentifierParameter(NpgsqlCommand cmd, string value)
            => AddParameter(cmd, in _paramPersonIdentifier, value);

        public NpgsqlParameter<string> AddOrganizationIdentifierParameter(NpgsqlCommand cmd, string value)
            => AddParameter(cmd, in _paramOrganizationIdentifier, value);

        public NpgsqlParameter<IList<Guid>> AddPartyUuidListParameter(NpgsqlCommand cmd, IList<Guid> value)
            => AddParameter(cmd, in _paramPartyUuidList, value);

        public NpgsqlParameter<IList<int>> AddPartyIdListParameter(NpgsqlCommand cmd, IList<int> value)
            => AddParameter(cmd, in _paramPartyIdList, value);

        public NpgsqlParameter<IList<string>> AddPersonIdentifierListParameter(NpgsqlCommand cmd, IList<string> value)
            => AddParameter(cmd, in _paramPersonIdentifierList, value);

        public NpgsqlParameter<IList<string>> AddOrganizationIdentifierListParameter(NpgsqlCommand cmd, IList<string> value)
            => AddParameter(cmd, in _paramOrganizationIdentifierList, value);

        public (NpgsqlParameter<long> From, NpgsqlParameter<int> Limit) AddStreamPageParameters(NpgsqlCommand cmd, ulong from, ushort limit)
        {
            var fromParam = AddParameter(cmd, in _paramStreamFrom, (long)from);
            var limitParam = AddParameter(cmd, in _paramStreamLimit, (int)limit);

            return (fromParam, limitParam);
        }

        private NpgsqlParameter<T> AddParameter<T>(NpgsqlCommand cmd, in FilterParameter config, T value)
        {
            Debug.Assert(config.HasValue, "Parameter must be configured");
            Debug.Assert(config.Type == typeof(T), "Parameter type mismatch");

            var param = cmd.Parameters.Add<T>(config.Name, config.DbType);
            param.TypedValue = value;

            return param;
        }

        public Guid ReadParentUuid(NpgsqlDataReader reader)
            => reader.GetFieldValue<Guid>(_parentFields.PartyUuid);

        public FieldValue<Guid> ReadChildUuid(NpgsqlDataReader reader)
            => reader.GetConditionalFieldValue<Guid>(_childFields.PartyUuid);

        public PartyRecord ReadParentParty(NpgsqlDataReader reader)
            => ReadParty(reader, in _parentFields);

        public PartyRecord ReadChildParty(NpgsqlDataReader reader, Guid parentPartyUuid)
            => ReadParty(reader, in _childFields, parentPartyUuid);

        private static PartyRecord ReadParty(NpgsqlDataReader reader, in PartyFields fields, FieldValue<Guid> parentPartyUuid = default)
        {
            Guard.IsNotNull(reader);
            Guard.IsNotNull(fields);

            var partyType = reader.GetConditionalFieldValue<PartyType>(fields.PartyType);
            return partyType switch
            {
                { HasValue: false } => ReadBaseParty(reader, in fields, partyType),
                { Value: PartyType.Person } => ReadPersonParty(reader, in fields),
                { Value: PartyType.Organization } => ReadOrganizationParty(reader, in fields, parentPartyUuid),
                _ => Unreachable(),
            };

            static PartyRecord ReadBaseParty(NpgsqlDataReader reader, in PartyFields fields, FieldValue<PartyType> partyType)
            {
                return new PartyRecord(partyType)
                {
                    PartyUuid = reader.GetConditionalFieldValue<Guid>(fields.PartyUuid),
                    PartyId = reader.GetConditionalFieldValue<int>(fields.PartyId),
                    Name = reader.GetConditionalFieldValue<string>(fields.PartyName),
                    PersonIdentifier = reader.GetConditionalParsableFieldValue<PersonIdentifier>(fields.PartyPersonIdentifier),
                    OrganizationIdentifier = reader.GetConditionalParsableFieldValue<OrganizationIdentifier>(fields.PartyOrganizationIdentifier),
                    CreatedAt = reader.GetConditionalFieldValue<DateTimeOffset>(fields.PartyCreated),
                    ModifiedAt = reader.GetConditionalFieldValue<DateTimeOffset>(fields.PartyUpdated),
                    IsDeleted = reader.GetConditionalFieldValue<bool>(fields.PartyIsDeleted),
                    VersionId = reader.GetConditionalFieldValue<long>(fields.PartyVersionId).Select(static v => (ulong)v),
                };
            }

            static PersonRecord ReadPersonParty(NpgsqlDataReader reader, in PartyFields fields)
            {
                return new PersonRecord
                {
                    PartyUuid = reader.GetConditionalFieldValue<Guid>(fields.PartyUuid),
                    PartyId = reader.GetConditionalFieldValue<int>(fields.PartyId),
                    Name = reader.GetConditionalFieldValue<string>(fields.PartyName),
                    PersonIdentifier = reader.GetConditionalParsableFieldValue<PersonIdentifier>(fields.PartyPersonIdentifier),
                    OrganizationIdentifier = reader.GetConditionalParsableFieldValue<OrganizationIdentifier>(fields.PartyOrganizationIdentifier),
                    CreatedAt = reader.GetConditionalFieldValue<DateTimeOffset>(fields.PartyCreated),
                    ModifiedAt = reader.GetConditionalFieldValue<DateTimeOffset>(fields.PartyUpdated),
                    IsDeleted = reader.GetConditionalFieldValue<bool>(fields.PartyIsDeleted),
                    VersionId = reader.GetConditionalFieldValue<long>(fields.PartyVersionId).Select(static v => (ulong)v),
                    FirstName = reader.GetConditionalFieldValue<string>(fields.PersonFirstName),
                    MiddleName = reader.GetConditionalFieldValue<string>(fields.PersonMiddleName),
                    LastName = reader.GetConditionalFieldValue<string>(fields.PersonLastName),
                    DateOfBirth = reader.GetConditionalFieldValue<DateOnly>(fields.PersonDateOfBirth),
                    DateOfDeath = reader.GetConditionalFieldValue<DateOnly>(fields.PersonDateOfDeath),
                    Address = reader.GetConditionalFieldValue<StreetAddress>(fields.PersonAddress),
                    MailingAddress = reader.GetConditionalFieldValue<MailingAddress>(fields.PersonMailingAddress),
                };
            }

            static OrganizationRecord ReadOrganizationParty(NpgsqlDataReader reader, in PartyFields fields, FieldValue<Guid> parentPartyUuid)
            {
                return new OrganizationRecord
                {
                    PartyUuid = reader.GetConditionalFieldValue<Guid>(fields.PartyUuid),
                    PartyId = reader.GetConditionalFieldValue<int>(fields.PartyId),
                    Name = reader.GetConditionalFieldValue<string>(fields.PartyName),
                    PersonIdentifier = reader.GetConditionalParsableFieldValue<PersonIdentifier>(fields.PartyPersonIdentifier),
                    OrganizationIdentifier = reader.GetConditionalParsableFieldValue<OrganizationIdentifier>(fields.PartyOrganizationIdentifier),
                    CreatedAt = reader.GetConditionalFieldValue<DateTimeOffset>(fields.PartyCreated),
                    ModifiedAt = reader.GetConditionalFieldValue<DateTimeOffset>(fields.PartyUpdated),
                    IsDeleted = reader.GetConditionalFieldValue<bool>(fields.PartyIsDeleted),
                    VersionId = reader.GetConditionalFieldValue<long>(fields.PartyVersionId).Select(static v => (ulong)v),
                    UnitStatus = reader.GetConditionalFieldValue<string>(fields.OrganizationUnitStatus),
                    UnitType = reader.GetConditionalFieldValue<string>(fields.OrganizationUnitType),
                    TelephoneNumber = reader.GetConditionalFieldValue<string>(fields.OrganizationTelephoneNumber),
                    MobileNumber = reader.GetConditionalFieldValue<string>(fields.OrganizationMobileNumber),
                    FaxNumber = reader.GetConditionalFieldValue<string>(fields.OrganizationFaxNumber),
                    EmailAddress = reader.GetConditionalFieldValue<string>(fields.OrganizationEmailAddress),
                    InternetAddress = reader.GetConditionalFieldValue<string>(fields.OrganizationInternetAddress),
                    MailingAddress = reader.GetConditionalFieldValue<MailingAddress>(fields.OrganizationMailingAddress),
                    BusinessAddress = reader.GetConditionalFieldValue<MailingAddress>(fields.OrganizationBusinessAddress),
                    ParentOrganizationUuid = parentPartyUuid,
                };
            }

            static PartyRecord Unreachable()
                => throw new UnreachableException();
        }

        private sealed class Builder
        {
            public static PartyQuery Create(PartyFieldIncludes includes, PartyFilters filterBy)
            {
                Builder builder = new();
                builder.Populate(includes, filterBy);

                PartyFields parentFields = new(
                    partyUuid: builder._partyUuid,
                    partyId: builder._partyId,
                    partyType: builder._partyType,
                    partyName: builder._partyName,
                    partyPersonIdentifier: builder._partyPersonIdentifier,
                    partyOrganizationIdentifier: builder._partyOrganizationIdentifier,
                    partyCreated: builder._partyCreated,
                    partyUpdated: builder._partyUpdated,
                    partyIsDeleted: builder._partyIsDeleted,
                    partyVersionId: builder._partyVersionId,
                    personFirstName: builder._personFirstName,
                    personMiddleName: builder._personMiddleName,
                    personLastName: builder._personLastName,
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
                    organizationBusinessAddress: builder._organizationBusinessAddress);

                PartyFields? childFields = !builder._hasSubUnits ? null
                    : new(
                        partyUuid: builder._childPartyUuid,
                        partyId: builder._childPartyId,
                        partyType: builder._childPartyType,
                        partyName: builder._childPartyName,
                        partyPersonIdentifier: builder._childPartyPersonIdentifier,
                        partyOrganizationIdentifier: builder._childPartyOrganizationIdentifier,
                        partyCreated: builder._childPartyCreated,
                        partyUpdated: builder._childPartyUpdated,
                        partyIsDeleted: builder._childPartyIsDeleted,
                        partyVersionId: builder._childPartyVersionId,
                        personFirstName: -1,
                        personMiddleName: -1,
                        personLastName: -1,
                        personDateOfBirth: -1,
                        personDateOfDeath: -1,
                        personAddress: -1,
                        personMailingAddress: -1,
                        organizationUnitStatus: builder._childOrganizationUnitStatus,
                        organizationUnitType: builder._childOrganizationUnitType,
                        organizationTelephoneNumber: builder._childOrganizationTelephoneNumber,
                        organizationMobileNumber: builder._childOrganizationMobileNumber,
                        organizationFaxNumber: builder._childOrganizationFaxNumber,
                        organizationEmailAddress: builder._childOrganizationEmailAddress,
                        organizationInternetAddress: builder._childOrganizationInternetAddress,
                        organizationMailingAddress: builder._childOrganizationMailingAddress,
                        organizationBusinessAddress: builder._childOrganizationBusinessAddress);

                var commandText = builder._builder.ToString();
                return new(
                    commandText,
                    parentFields,
                    childFields,
                    paramPartyUuid: builder._paramPartyUuid,
                    paramPartyId: builder._paramPartyId,
                    paramPersonIdentifier: builder._paramPersonIdentifier,
                    paramOrganizationIdentifier: builder._paramOrganizationIdentifier,
                    paramPartyUuidList: builder._paramPartyUuidList,
                    paramPartyIdList: builder._paramPartyIdList,
                    paramPersonIdentifierList: builder._paramPersonIdentifierList,
                    paramOrganizationIdentifierList: builder._paramOrganizationIdentifierList,
                    paramStreamFrom: builder._paramStreamFrom,
                    paramStreamLimit: builder._paramStreamLimit);
            }

            private readonly StringBuilder _builder = new();

            // parameters
            private FilterParameter _paramPartyUuid;
            private FilterParameter _paramPartyId;
            private FilterParameter _paramPersonIdentifier;
            private FilterParameter _paramOrganizationIdentifier;
            private FilterParameter _paramPartyUuidList;
            private FilterParameter _paramPartyIdList;
            private FilterParameter _paramPersonIdentifierList;
            private FilterParameter _paramOrganizationIdentifierList;
            private FilterParameter _paramStreamFrom;
            private FilterParameter _paramStreamLimit;

            // fields
            private sbyte _fieldIndex = 0;
            private bool _hasSubUnits = false;

            // parent register.party
            private sbyte _partyUuid = -1;
            private sbyte _partyId = -1;
            private sbyte _partyType = -1;
            private sbyte _partyName = -1;
            private sbyte _partyPersonIdentifier = -1;
            private sbyte _partyOrganizationIdentifier = -1;
            private sbyte _partyCreated = -1;
            private sbyte _partyUpdated = -1;
            private sbyte _partyIsDeleted = -1;
            private sbyte _partyVersionId = -1;

            // parent register.person
            private sbyte _personFirstName = -1;
            private sbyte _personMiddleName = -1;
            private sbyte _personLastName = -1;
            private sbyte _personDateOfBirth = -1;
            private sbyte _personDateOfDeath = -1;
            private sbyte _personAddress = -1;
            private sbyte _personMailingAddress = -1;

            // parent register.organization
            private sbyte _organizationUnitStatus = -1;
            private sbyte _organizationUnitType = -1;
            private sbyte _organizationTelephoneNumber = -1;
            private sbyte _organizationMobileNumber = -1;
            private sbyte _organizationFaxNumber = -1;
            private sbyte _organizationEmailAddress = -1;
            private sbyte _organizationInternetAddress = -1;
            private sbyte _organizationMailingAddress = -1;
            private sbyte _organizationBusinessAddress = -1;

            // child register.party
            private sbyte _childPartyUuid = -1;
            private sbyte _childPartyId = -1;
            private sbyte _childPartyType = -1;
            private sbyte _childPartyName = -1;
            private sbyte _childPartyPersonIdentifier = -1;
            private sbyte _childPartyOrganizationIdentifier = -1;
            private sbyte _childPartyCreated = -1;
            private sbyte _childPartyUpdated = -1;
            private sbyte _childPartyIsDeleted = -1;
            private sbyte _childPartyVersionId = -1;

            // child register.organization
            private sbyte _childOrganizationUnitStatus = -1;
            private sbyte _childOrganizationUnitType = -1;
            private sbyte _childOrganizationTelephoneNumber = -1;
            private sbyte _childOrganizationMobileNumber = -1;
            private sbyte _childOrganizationFaxNumber = -1;
            private sbyte _childOrganizationEmailAddress = -1;
            private sbyte _childOrganizationInternetAddress = -1;
            private sbyte _childOrganizationMailingAddress = -1;
            private sbyte _childOrganizationBusinessAddress = -1;

            public void Populate(PartyFieldIncludes includes, PartyFilters filterBy)
            {
                _builder.Append(/*strpsql*/"SELECT");

                _partyUuid = AddField("p.uuid", "p_uuid", includes.HasFlag(PartyFieldIncludes.PartyUuid));
                _partyId = AddField("p.id", "p_id", includes.HasFlag(PartyFieldIncludes.PartyId));
                _partyType = AddField("p.party_type", "p_party_type", includes.HasFlag(PartyFieldIncludes.PartyType));
                _partyName = AddField("p.name", "p_name", includes.HasFlag(PartyFieldIncludes.PartyName));
                _partyPersonIdentifier = AddField("p.person_identifier", "p_person_identifier", includes.HasFlag(PartyFieldIncludes.PartyPersonIdentifier));
                _partyOrganizationIdentifier = AddField("p.organization_identifier", "p_organization_identifier", includes.HasFlag(PartyFieldIncludes.PartyOrganizationIdentifier));
                _partyCreated = AddField("p.created", "p_created", includes.HasFlag(PartyFieldIncludes.PartyCreatedAt));
                _partyUpdated = AddField("p.updated", "p_updated", includes.HasFlag(PartyFieldIncludes.PartyModifiedAt));
                _partyIsDeleted = AddField("p.is_deleted", "p_is_deleted", includes.HasFlag(PartyFieldIncludes.PartyIsDeleted));
                _partyVersionId = AddField("p.version_id", "p_version_id", includes.HasFlag(PartyFieldIncludes.PartyVersionId));

                _personFirstName = AddField("f.first_name", "p_first_name", includes.HasFlag(PartyFieldIncludes.PersonFirstName));
                _personMiddleName = AddField("f.middle_name", "p_middle_name", includes.HasFlag(PartyFieldIncludes.PersonMiddleName));
                _personLastName = AddField("f.last_name", "p_last_name", includes.HasFlag(PartyFieldIncludes.PersonLastName));
                _personDateOfBirth = AddField("f.date_of_birth", "p_date_of_birth", includes.HasFlag(PartyFieldIncludes.PersonDateOfBirth));
                _personDateOfDeath = AddField("f.date_of_death", "p_date_of_death", includes.HasFlag(PartyFieldIncludes.PersonDateOfDeath));
                _personAddress = AddField("f.address", "p_address", includes.HasFlag(PartyFieldIncludes.PersonAddress));
                _personMailingAddress = AddField("f.mailing_address", "p_person_mailing_address", includes.HasFlag(PartyFieldIncludes.PersonMailingAddress));

                _organizationUnitStatus = AddField("o.unit_status", "p_unit_status", includes.HasFlag(PartyFieldIncludes.OrganizationUnitStatus));
                _organizationUnitType = AddField("o.unit_type", "p_unit_type", includes.HasFlag(PartyFieldIncludes.OrganizationUnitType));
                _organizationTelephoneNumber = AddField("o.telephone_number", "p_telephone_number", includes.HasFlag(PartyFieldIncludes.OrganizationTelephoneNumber));
                _organizationMobileNumber = AddField("o.mobile_number", "p_mobile_number", includes.HasFlag(PartyFieldIncludes.OrganizationMobileNumber));
                _organizationFaxNumber = AddField("o.fax_number", "p_fax_number", includes.HasFlag(PartyFieldIncludes.OrganizationFaxNumber));
                _organizationEmailAddress = AddField("o.email_address", "p_email_address", includes.HasFlag(PartyFieldIncludes.OrganizationEmailAddress));
                _organizationInternetAddress = AddField("o.internet_address", "p_internet_address", includes.HasFlag(PartyFieldIncludes.OrganizationInternetAddress));
                _organizationMailingAddress = AddField("o.mailing_address", "p_org_mailing_address", includes.HasFlag(PartyFieldIncludes.OrganizationMailingAddress));
                _organizationBusinessAddress = AddField("o.business_address", "p_business_address", includes.HasFlag(PartyFieldIncludes.OrganizationBusinessAddress));

                if (includes.HasFlag(PartyFieldIncludes.SubUnits))
                {
                    _childPartyUuid = AddField("cp.uuid", "cp_uuid", includes.HasFlag(PartyFieldIncludes.PartyUuid));
                    _childPartyId = AddField("cp.id", "cp_id", includes.HasFlag(PartyFieldIncludes.PartyId));
                    _childPartyType = AddField("cp.party_type", "cp_party_type", includes.HasFlag(PartyFieldIncludes.PartyType));
                    _childPartyName = AddField("cp.name", "cp_name", includes.HasFlag(PartyFieldIncludes.PartyName));
                    _childPartyPersonIdentifier = AddField("cp.person_identifier", "cp_person_identifier", includes.HasFlag(PartyFieldIncludes.PartyPersonIdentifier));
                    _childPartyOrganizationIdentifier = AddField("cp.organization_identifier", "cp_organization_identifier", includes.HasFlag(PartyFieldIncludes.PartyOrganizationIdentifier));
                    _childPartyCreated = AddField("cp.created", "cp_created", includes.HasFlag(PartyFieldIncludes.PartyCreatedAt));
                    _childPartyUpdated = AddField("cp.updated", "cp_updated", includes.HasFlag(PartyFieldIncludes.PartyModifiedAt));
                    _childPartyIsDeleted = AddField("cp.is_deleted", "cp_is_deleted", includes.HasFlag(PartyFieldIncludes.PartyIsDeleted));
                    _childPartyVersionId = AddField("cp.version_id", "cp_version_id", includes.HasFlag(PartyFieldIncludes.PartyVersionId));

                    _childOrganizationUnitStatus = AddField("cp.unit_status", "cp_unit_status", includes.HasFlag(PartyFieldIncludes.OrganizationUnitStatus));
                    _childOrganizationUnitType = AddField("cp.unit_type", "cp_unit_type", includes.HasFlag(PartyFieldIncludes.OrganizationUnitType));
                    _childOrganizationTelephoneNumber = AddField("cp.telephone_number", "cp_telephone_number", includes.HasFlag(PartyFieldIncludes.OrganizationTelephoneNumber));
                    _childOrganizationMobileNumber = AddField("cp.mobile_number", "cp_mobile_number", includes.HasFlag(PartyFieldIncludes.OrganizationMobileNumber));
                    _childOrganizationFaxNumber = AddField("cp.fax_number", "cp_fax_number", includes.HasFlag(PartyFieldIncludes.OrganizationFaxNumber));
                    _childOrganizationEmailAddress = AddField("cp.email_address", "cp_email_address", includes.HasFlag(PartyFieldIncludes.OrganizationEmailAddress));
                    _childOrganizationInternetAddress = AddField("cp.internet_address", "cp_internet_address", includes.HasFlag(PartyFieldIncludes.OrganizationInternetAddress));
                    _childOrganizationMailingAddress = AddField("cp.mailing_address", "cp_org_mailing_address", includes.HasFlag(PartyFieldIncludes.OrganizationMailingAddress));
                    _childOrganizationBusinessAddress = AddField("cp.business_address", "cp_business_address", includes.HasFlag(PartyFieldIncludes.OrganizationBusinessAddress));

                    _hasSubUnits = true;
                }

                _builder.AppendLine().Append(/*strpsql*/"FROM register.party p");

                if (includes.HasAnyFlags(PartyFieldIncludes.Person))
                {
                    _builder.AppendLine().Append(/*strpsql*/"FULL JOIN register.person f USING (uuid)");
                }

                if (includes.HasAnyFlags(PartyFieldIncludes.Organization))
                {
                    _builder.AppendLine().Append(/*strpsql*/"FULL JOIN register.organization o USING (uuid)");
                }

                if (_hasSubUnits)
                {
                    _builder.AppendLine().Append(/*strpsql*/"LEFT JOIN (");
                    _builder.AppendLine().Append(/*strpsql*/"    SELECT");

                    var first = true;
                    AddJoinField("cp.uuid", includes.HasFlag(PartyFieldIncludes.PartyUuid), ref first);
                    AddJoinField("cp.id", includes.HasFlag(PartyFieldIncludes.PartyId), ref first);
                    AddJoinField("cp.party_type", includes.HasFlag(PartyFieldIncludes.PartyType), ref first);
                    AddJoinField("cp.name", includes.HasFlag(PartyFieldIncludes.PartyName), ref first);
                    AddJoinField("cp.person_identifier", includes.HasFlag(PartyFieldIncludes.PartyPersonIdentifier), ref first);
                    AddJoinField("cp.organization_identifier", includes.HasFlag(PartyFieldIncludes.PartyOrganizationIdentifier), ref first);
                    AddJoinField("cp.created", includes.HasFlag(PartyFieldIncludes.PartyCreatedAt), ref first);
                    AddJoinField("cp.updated", includes.HasFlag(PartyFieldIncludes.PartyModifiedAt), ref first);
                    AddJoinField("cp.is_deleted", includes.HasFlag(PartyFieldIncludes.PartyIsDeleted), ref first);
                    AddJoinField("cp.version_id", includes.HasFlag(PartyFieldIncludes.PartyVersionId), ref first);

                    AddJoinField("co.unit_status", includes.HasFlag(PartyFieldIncludes.OrganizationUnitStatus), ref first);
                    AddJoinField("co.unit_type", includes.HasFlag(PartyFieldIncludes.OrganizationUnitType), ref first);
                    AddJoinField("co.telephone_number", includes.HasFlag(PartyFieldIncludes.OrganizationTelephoneNumber), ref first);
                    AddJoinField("co.mobile_number", includes.HasFlag(PartyFieldIncludes.OrganizationMobileNumber), ref first);
                    AddJoinField("co.fax_number", includes.HasFlag(PartyFieldIncludes.OrganizationFaxNumber), ref first);
                    AddJoinField("co.email_address", includes.HasFlag(PartyFieldIncludes.OrganizationEmailAddress), ref first);
                    AddJoinField("co.internet_address", includes.HasFlag(PartyFieldIncludes.OrganizationInternetAddress), ref first);
                    AddJoinField("co.mailing_address", includes.HasFlag(PartyFieldIncludes.OrganizationMailingAddress), ref first);
                    AddJoinField("co.business_address", includes.HasFlag(PartyFieldIncludes.OrganizationBusinessAddress), ref first);

                    AddJoinField("r.to_party parent_uuid", true, ref first);
                    _builder.AppendLine().Append(/*strpsql*/"    FROM register.external_role r");
                    _builder.AppendLine().Append(/*strpsql*/"    FULL JOIN register.party cp ON cp.uuid = r.from_party");

                    if (includes.HasAnyFlags(PartyFieldIncludes.Organization))
                    {
                        _builder.AppendLine().Append(/*strpsql*/"    FULL JOIN register.organization co USING (uuid)");
                    }

                    _builder.AppendLine().Append(/*strpsql*/"    WHERE r.source = 'ccr' AND (r.identifier = 'aafy' OR r.identifier = 'bedr')");
                    _builder.AppendLine().Append(/*strpsql*/") cp ON cp.parent_uuid = p.uuid");
                }

                var firstFilter = true;
                if (!filterBy.HasFlag(PartyFilters.Multiple))
                {
                    // if we are not filtering on multiple values, we only allow a single filter type
                    switch (filterBy)
                    {
                        case PartyFilters.PartyUuid:
                            _paramPartyUuid = AddFilter(typeof(Guid), "partyUuid", /*strpsql*/"p.uuid =", NpgsqlDbType.Uuid, ref firstFilter);
                            break;

                        case PartyFilters.PartyId:
                            _paramPartyId = AddFilter(typeof(int), "partyId", /*strpsql*/"p.id =", NpgsqlDbType.Integer, ref firstFilter);
                            break;

                        case PartyFilters.PersonIdentifier:
                            _paramPersonIdentifier = AddFilter(typeof(string), "personIdentifier", /*strpsql*/"p.person_identifier =", NpgsqlDbType.Text, ref firstFilter);
                            break;

                        case PartyFilters.OrganizationIdentifier:
                            _paramOrganizationIdentifier = AddFilter(typeof(string), "organizationIdentifier", /*strpsql*/"p.organization_identifier =", NpgsqlDbType.Text, ref firstFilter);
                            break;

                        case PartyFilters.StreamPage:
                            // handled later, but legal
                            break;

                        default:
                            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(filterBy), $"Unhandled {nameof(PartyFilters)} value: {filterBy}");
                            break;
                    }
                }
                else
                {
                    if (filterBy.HasFlag(PartyFilters.PartyUuid))
                    {
                        // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
                        _paramPartyUuidList = AddFilter(typeof(IList<Guid>), "partyUuids", /*strpsql*/"p.uuid = ANY", NpgsqlDbType.Array | NpgsqlDbType.Uuid, ref firstFilter);
                    }

                    if (filterBy.HasFlag(PartyFilters.PartyId))
                    {
                        // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
                        _paramPartyIdList = AddFilter(typeof(IList<int>), "partyIds", /*strpsql*/"p.id = ANY", NpgsqlDbType.Array | NpgsqlDbType.Integer, ref firstFilter);
                    }

                    if (filterBy.HasFlag(PartyFilters.PersonIdentifier))
                    {
                        // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
                        _paramPersonIdentifierList = AddFilter(typeof(IList<string>), "personIdentifiers", /*strpsql*/"p.person_identifier = ANY", NpgsqlDbType.Array | NpgsqlDbType.Text, ref firstFilter);
                    }

                    if (filterBy.HasFlag(PartyFilters.OrganizationIdentifier))
                    {
                        // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
                        _paramOrganizationIdentifierList = AddFilter(typeof(IList<string>), "organizationIdentifiers", /*strpsql*/"p.organization_identifier = ANY", NpgsqlDbType.Array | NpgsqlDbType.Text, ref firstFilter);
                    }

                    Debug.Assert(!firstFilter, "No filters were added, but multiple filters were requested");
                }

                if (filterBy.HasFlag(PartyFilters.StreamPage))
                {
                    Debug.Assert(!_hasSubUnits, "A query cannot get both a stream page and subunits");

                    _paramStreamFrom = AddFilter(typeof(long), "streamFrom", /*strpsql*/"p.version_id >=", NpgsqlDbType.Bigint, ref firstFilter);
                    _paramStreamLimit = new(typeof(int), "streamLimit", NpgsqlDbType.Integer);

                    _builder.AppendLine().Append(/*strpsql*/"ORDER BY p.version_id ASC");
                    _builder.AppendLine().Append(/*strpsql*/"LIMIT @streamLimit");
                }
                else
                {
                    _builder.AppendLine().Append(/*strpsql*/"ORDER BY p.uuid");

                    if (_hasSubUnits)
                    {
                        _builder.AppendLine(",").Append(/*strpsql*/"    cp.uuid");
                    }
                }
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

            private void AddJoinField(string sourceSql, bool include, ref bool first)
            {
                if (!include)
                {
                    return;
                }

                if (first)
                {
                    first = false;
                }
                else
                {
                    _builder.Append(',');
                }

                _builder.AppendLine();
                _builder.Append("            ").Append(sourceSql);
            }

            private FilterParameter AddFilter(Type type, string name, string sourceSql, NpgsqlDbType dbType, ref bool first)
            {
                if (first)
                {
                    _builder.AppendLine().Append(/*strpsql*/"WHERE ");
                    first = false;
                }
                else
                {
                    _builder.AppendLine().Append(/*strpsql*/"    OR ");
                }

                _builder.Append(sourceSql);
                
                if (dbType.HasFlag(NpgsqlDbType.Array))
                {
                    _builder.Append('(');
                }
                else
                {
                    _builder.Append(' ');
                }

                _builder.Append('@').Append(name);

                if (dbType.HasFlag(NpgsqlDbType.Array))
                {
                    _builder.Append(')');
                }

                return new(type, name, dbType);
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
        private readonly struct PartyFields(
            // register.party
            sbyte partyUuid,
            sbyte partyId,
            sbyte partyType,
            sbyte partyName,
            sbyte partyPersonIdentifier,
            sbyte partyOrganizationIdentifier,
            sbyte partyCreated,
            sbyte partyUpdated,
            sbyte partyIsDeleted,
            sbyte partyVersionId,

            // register.person
            sbyte personFirstName,
            sbyte personMiddleName,
            sbyte personLastName,
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
            sbyte organizationBusinessAddress)
        {
            // register.party
            public int PartyUuid => partyUuid;
            public int PartyId => partyId;
            public int PartyType => partyType;
            public int PartyName => partyName;
            public int PartyPersonIdentifier => partyPersonIdentifier;
            public int PartyOrganizationIdentifier => partyOrganizationIdentifier;
            public int PartyCreated => partyCreated;
            public int PartyUpdated => partyUpdated;
            public int PartyIsDeleted => partyIsDeleted;
            public int PartyVersionId => partyVersionId;

            // register.person
            public int PersonFirstName => personFirstName;
            public int PersonMiddleName => personMiddleName;
            public int PersonLastName => personLastName;
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
        }
    }
}
