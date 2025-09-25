﻿#nullable enable

using Altinn.Register.Core.PartyImport.A2;

namespace Altinn.Register.Tests.UnitTests;

public class A2PartyExternalRoleAssignmentTests
{
    [Theory]
    [InlineData("BEDR", "bedr")]
    [InlineData("BEDR", "BEDR")]
    [InlineData("bedr", "BEDR")]
    [InlineData("bedr", "bedr")]
    [InlineData("Bedr", "bEdR")]
    public void Equals_IsCaseInsensitive(string role1, string role2)
    {
        var uuid = Guid.NewGuid();
        var left = new A2PartyExternalRoleAssignment
        {
            ToPartyUuid = uuid,
            RoleCode = role1,
        };
        var right = new A2PartyExternalRoleAssignment
        {
            ToPartyUuid = uuid,
            RoleCode = role2,
        };

        left.Equals(right).Should().BeTrue();
        right.Equals(left).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentUuid_NotEqual()
    {
        var left = new A2PartyExternalRoleAssignment
        {
            ToPartyUuid = Guid.NewGuid(),
            RoleCode = "BEDR",
        };
        var right = new A2PartyExternalRoleAssignment
        {
            ToPartyUuid = Guid.NewGuid(),
            RoleCode = "BEDR",
        };
        left.Equals(right).Should().BeFalse();
        right.Equals(left).Should().BeFalse();
        left.GetHashCode().Should().NotBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentRole_NotEqual()
    {
        var uuid = Guid.NewGuid();
        var left = new A2PartyExternalRoleAssignment
        {
            ToPartyUuid = uuid,
            RoleCode = "BEDR",
        };
        var right = new A2PartyExternalRoleAssignment
        {
            ToPartyUuid = uuid,
            RoleCode = "DAGL",
        };
        left.Equals(right).Should().BeFalse();
        right.Equals(left).Should().BeFalse();
        left.GetHashCode().Should().NotBe(right.GetHashCode());
    }
}
