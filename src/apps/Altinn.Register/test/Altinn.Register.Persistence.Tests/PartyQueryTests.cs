using Altinn.Authorization.ModelUtils.EnumUtils;
using Altinn.Register.Core.Parties;
using Xunit.Abstractions;
using static Altinn.Register.Persistence.PartyQueryFilters;
using static Altinn.Register.Persistence.PostgreSqlPartyPersistence;

namespace Altinn.Register.Persistence.Tests;

public class PartyQueryTests 
{
    private static FlagsEnumModel<PartyFieldIncludes> _includesModel = FlagsEnumModel.Create<PartyFieldIncludes>();

    [Theory]
    [MemberData(nameof(QueryVariants))]
    public async Task VerifyQuerySnapshot(PartyFieldIncludes includes, Filters filterBy)
    {
        PartyQueryFilters filter = filterBy;

        var settings = new VerifySettings();
        settings.UseParameters(includes, filter);

        var includesString = _includesModel.Format(includes);
        var filterByString = filter.ToString();
        
        var query = PartyQuery.Get(includes, filter);

        var queryString =
            $"""
            -- include: {includesString}
            -- filter: {filterByString}

            {query.CommandText}

            """;

        await Verify(queryString, extension: "sql", settings);
    }

    [Theory]
    [MemberData(nameof(QueryVariants))]
    public void QueryObjectIsCached(PartyFieldIncludes includes, Filters filterBy)
    {
        PartyQueryFilters filter = filterBy;
        var q1 = PartyQuery.Get(includes, filter);
        var q2 = PartyQuery.Get(includes, filter);

        q1.Should().BeSameAs(q2);
    }

    [Fact]
    public void QueryObjectIsCached_All()
    {
        var queries = new List<PartyQuery>();

        foreach (var variant in QueryVariants)
        {
            PartyFieldIncludes includes = (PartyFieldIncludes)variant[0];
            PartyQueryFilters filter = (Filters)variant[1];
            var newQuery = PartyQuery.Get(includes, filter);
            queries.Should().NotContain(newQuery);
            queries.Add(newQuery);
        }

        var index = 0;
        foreach (var variant in QueryVariants)
        {
            PartyFieldIncludes includes = (PartyFieldIncludes)variant[0];
            PartyQueryFilters filter = (Filters)variant[1];
            var existing = queries[index++];
            var current = PartyQuery.Get(includes, filter);
            current.Should().BeSameAs(existing);
        }
    }

    public static TheoryData<PartyFieldIncludes, Filters> QueryVariants => GetQueryVariants();

    private static TheoryData<PartyFieldIncludes, Filters> GetQueryVariants()
    {
        TheoryData<PartyFieldIncludes, Filters> data = new();

        IEnumerable<PartyLookupIdentifiers> singleIdentifiers = [
            PartyLookupIdentifiers.PartyId,
            PartyLookupIdentifiers.PartyUuid,
            PartyLookupIdentifiers.PersonIdentifier,
            PartyLookupIdentifiers.OrganizationIdentifier,
            PartyLookupIdentifiers.UserId,
            PartyLookupIdentifiers.Username,
            PartyLookupIdentifiers.SelfIdentifiedEmail,
            PartyLookupIdentifiers.ExternalUrn,
        ];

        IEnumerable<PartyLookupIdentifiers> multipleIdentifiers = [
            .. singleIdentifiers,

            // all at once
            PartyLookupIdentifiers.PartyId | PartyLookupIdentifiers.PartyUuid | PartyLookupIdentifiers.PersonIdentifier | PartyLookupIdentifiers.OrganizationIdentifier | PartyLookupIdentifiers.UserId | PartyLookupIdentifiers.Username | PartyLookupIdentifiers.SelfIdentifiedEmail | PartyLookupIdentifiers.ExternalUrn,
        ];

        IEnumerable<PartyListFilters> listFilters = [
            PartyListFilters.None,
            PartyListFilters.PartyType,
        ];

        IEnumerable<PartyQueryFilters> filters = [
            .. singleIdentifiers.Select(LookupOne),
            .. multipleIdentifiers.SelectMany(ids => listFilters.Select(f => Lookup(ids, f))),
            .. listFilters.Select(Stream),
        ];

        // default includes
        foreach (var filter in filters)
        {
            data.Add(PartyFieldIncludes.Identifiers | PartyFieldIncludes.PartyDisplayName, filter);
        }

        // with subunits
        foreach (var filter in filters.Where(static f => !f.IsStream))
        {
            data.Add(PartyFieldIncludes.Identifiers | PartyFieldIncludes.PartyDisplayName | PartyFieldIncludes.SubUnits, filter);
        }

        // full includes
        foreach (var filter in filters)
        {
            var allFields = PartyFieldIncludes.Party | PartyFieldIncludes.User | PartyFieldIncludes.Organization | PartyFieldIncludes.Person | PartyFieldIncludes.SystemUser | PartyFieldIncludes.SelfIdentifiedUser;
            data.Add(allFields, filter);

            if (!filter.IsStream)
            {
                data.Add(allFields | PartyFieldIncludes.SubUnits, filter);
            }
        }

        return data;
    }

    public class Filters
        : IXunitSerializable
    {
        private PartyQueryFilters _value;

        public void Deserialize(IXunitSerializationInfo info)
        {
            var mode = info.GetValue<QueryMode>("mode");
            PartyLookupIdentifiers identifiers;
            PartyListFilters filters;

            switch (mode)
            {
                case QueryMode.LookupOne:
                    identifiers = info.GetValue<PartyLookupIdentifiers>("identifier");
                    _value = LookupOne(identifiers);
                    break;

                case QueryMode.LookupMultiple:
                    identifiers = info.GetValue<PartyLookupIdentifiers>("identifiers");
                    filters = info.GetValue<PartyListFilters>("filters");
                    _value = Lookup(identifiers, filters);
                    break;

                case QueryMode.FilteredStream:
                    filters = info.GetValue<PartyListFilters>("filters");
                    _value = Stream(filters);
                    break;

                default:
                    _value = default;
                    break;
            }
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            var mode = _value.Mode;
            var identifiers = _value.LookupIdentifiers;
            var filters = _value.ListFilters;
            info.AddValue("mode", mode, typeof(QueryMode));

            switch (mode)
            {
                case QueryMode.LookupOne:
                    info.AddValue("identifier", identifiers, typeof(PartyLookupIdentifiers));
                    break;

                case QueryMode.LookupMultiple:
                    info.AddValue("identifiers", identifiers, typeof(PartyLookupIdentifiers));
                    info.AddValue("filters", filters, typeof(PartyListFilters));

                    break;

                case QueryMode.FilteredStream:
                    info.AddValue("filters", filters, typeof(PartyListFilters));

                    break;
            }
        }

        public override string ToString()
            => _value.ToString();

        public override bool Equals(object? obj)
            => obj is Filters other && _value.Equals(other._value);

        public override int GetHashCode()
            => _value.GetHashCode();

        public static implicit operator Filters(PartyQueryFilters value) => new() { _value = value };

        public static implicit operator PartyQueryFilters(Filters wrapper) => wrapper._value;
    }
}
