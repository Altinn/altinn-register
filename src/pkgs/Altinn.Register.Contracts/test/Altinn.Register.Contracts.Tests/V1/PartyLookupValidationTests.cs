using Altinn.Register.Contracts.Tests.Shouldly;
using Altinn.Register.Contracts.V1;

namespace Altinn.Register.Contracts.Tests.V1;

public class PartyLookupValidationTests 
{
    [Fact]
    public void NoPropertiesSet_ReturnsIssue()
    {
        PartyLookup target = new();

        var issues = target.ShouldNotBeValidComponentModel();

        var item = issues.ShouldHaveSingleItem();
        item.MemberNames.ShouldContain(nameof(PartyLookup.OrgNo));
        item.MemberNames.ShouldContain(nameof(PartyLookup.Ssn));
        item.ErrorMessage.ShouldBe(PartyLookup.SsnOrOrgNoRequiredMessage);
    }

    [Fact]
    public void TwoPropertiesSet_ReturnsIssue()
    {
        PartyLookup target = new() { Ssn = "09054300139", OrgNo = "910072218" };

        var issues = target.ShouldNotBeValidComponentModel();

        var item = issues.ShouldHaveSingleItem();
        item.MemberNames.ShouldContain(nameof(PartyLookup.OrgNo));
        item.ErrorMessage.ShouldBe(PartyLookup.SsnAndOrgNoExclusiveMessage);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("F2345678901")]
    public void SsnInvalid(string ssn)
    {
        PartyLookup target = new() { Ssn = ssn };

        var issues = target.ShouldNotBeValidComponentModel();

        var item = issues.ShouldHaveSingleItem();
        item.MemberNames.ShouldContain(nameof(PartyLookup.Ssn));
        item.ErrorMessage.ShouldBe("Value needs to be exactly 11 digits.");
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("F23456789")]
    public void OrgNoInvalid(string orgNo)
    {
        PartyLookup target = new() { OrgNo = orgNo };

        var issues = target.ShouldNotBeValidComponentModel();

        var item = issues.ShouldHaveSingleItem();
        item.MemberNames.ShouldContain(nameof(PartyLookup.OrgNo));
        item.ErrorMessage.ShouldBe("Value needs to be exactly 9 digits.");
    }

    [Theory]
    [InlineData("09054300139")]
    [InlineData("27036702163")]
    public void SsnIsValid(string ssn)
    {
        PartyLookup target = new() { Ssn = ssn };

        target.ShouldBeValidComponentModel();
    }

    [Theory]
    [InlineData("910072218")]
    [InlineData("810999012")]
    public void OrgNoIsValid(string orgNo)
    {
        PartyLookup target = new() { OrgNo = orgNo };

        target.ShouldBeValidComponentModel();
    }
}
