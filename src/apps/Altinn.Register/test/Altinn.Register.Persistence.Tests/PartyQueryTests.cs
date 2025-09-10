using Altinn.Authorization.ModelUtils.EnumUtils;
using Altinn.Register.Core.Parties;
using static Altinn.Register.Persistence.PostgreSqlPartyPersistence;

namespace Altinn.Register.Persistence.Tests;

public class PartyQueryTests 
{
    private static FlagsEnumModel<PartyFieldIncludes> _includesModel = FlagsEnumModel.Create<PartyFieldIncludes>();
    private static FlagsEnumModel<PartyQueryFilters> _filtersModel = FlagsEnumModel.Create<PartyQueryFilters>();

    [Theory]
    [MemberData(nameof(QueryVariants))]
    public async Task VerifyQuerySnapshot(PartyFieldIncludes includes, PartyQueryFilters filterBy)
    {
        var settings = new VerifySettings();
        settings.UseParameters(includes, filterBy);

        var includesString = _includesModel.Format(includes);
        var filterByString = _filtersModel.Format(filterBy);
        
        var query = PartyQuery.Get(includes, filterBy);

        var queryString =
            $"""
            -- include: {includesString}
            -- filter: {filterByString}

            {query.CommandText}
            """;

        await Verify(queryString, extension: "sql", settings);
    }

    public static TheoryData<PartyFieldIncludes, PartyQueryFilters> QueryVariants => GetQueryVariants();

    private static TheoryData<PartyFieldIncludes, PartyQueryFilters> GetQueryVariants()
    {
        TheoryData<PartyFieldIncludes, PartyQueryFilters> data = new();
        
        IEnumerable<PartyQueryFilters> singleFilters = [
            PartyQueryFilters.PartyId,
            PartyQueryFilters.PartyUuid,
            PartyQueryFilters.PersonIdentifier,
            PartyQueryFilters.OrganizationIdentifier,
            PartyQueryFilters.UserId,
            PartyQueryFilters.StreamPage,
        ];

        IEnumerable<PartyQueryFilters> multiFilters = [
            PartyQueryFilters.PartyId | PartyQueryFilters.Multiple,
            PartyQueryFilters.PartyUuid | PartyQueryFilters.Multiple,
            PartyQueryFilters.PersonIdentifier | PartyQueryFilters.Multiple,
            PartyQueryFilters.OrganizationIdentifier | PartyQueryFilters.Multiple,
            PartyQueryFilters.UserId | PartyQueryFilters.Multiple,
            
            // all at once
            PartyQueryFilters.PartyId | PartyQueryFilters.PartyUuid | PartyQueryFilters.PersonIdentifier | PartyQueryFilters.OrganizationIdentifier | PartyQueryFilters.UserId | PartyQueryFilters.Multiple,
        ];

        IEnumerable<PartyQueryFilters> filters = [
            .. singleFilters,
            .. multiFilters,
        ];

        // default includes
        foreach (var filter in filters)
        {
            data.Add(PartyFieldIncludes.Identifiers | PartyFieldIncludes.PartyDisplayName, filter);
        }

        // with subunits
        foreach (var filter in filters.Where(static f => !f.HasFlag(PartyQueryFilters.StreamPage)))
        {
            data.Add(PartyFieldIncludes.Identifiers | PartyFieldIncludes.PartyDisplayName | PartyFieldIncludes.SubUnits, filter);
        }

        // full includes
        foreach (var filter in filters)
        {
            var allFields = PartyFieldIncludes.Party | PartyFieldIncludes.User | PartyFieldIncludes.Organization | PartyFieldIncludes.Person | PartyFieldIncludes.SystemUser;
            data.Add(allFields, filter);

            if (!filter.HasFlag(PartyQueryFilters.StreamPage))
            {
                data.Add(allFields | PartyFieldIncludes.SubUnits, filter);
            }
        }

        return data;
    }
}
