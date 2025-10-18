#nullable enable

using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Models;

namespace Altinn.Register.Tests.UnitTests;

public class PartyTypesSetTests 
{
    [Fact]
    public void Contains_AllTypes()
    {
        PartyTypes allBitsSet = ~PartyTypes.None;
        var partyTypesSet = new PartyTypesSet(allBitsSet);

        var allPartyTypes = Enum.GetValues<PartyRecordType>();
        partyTypesSet.Should().HaveCount(allPartyTypes.Length);

        foreach (var partyType in allPartyTypes)
        {
            partyTypesSet.Contains(partyType).Should().BeTrue("Expected PartyTypesSet to contain party type {0}", partyType);
        }

        var enumerated = partyTypesSet.ToList();
        enumerated.Should().HaveCount(allPartyTypes.Length, "Expected enumeration to yield all party types");

        foreach (var partyType in allPartyTypes)
        {
            enumerated.Should().Contain(partyType, "Expected enumeration to contain party type {0}", partyType);
        }
    }
}
