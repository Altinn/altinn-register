using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using Altinn.Platform.Register.Models;

using Xunit;

namespace Altinn.Platform.Models.Tests.Register
{
    public class PartyLookupValidationTests
    {
        [Fact]
        public void NoPropertiesSet_ReturnsIssue()
        {
            // Arrange
            PartyLookup target = new PartyLookup();

            // Act
            IReadOnlyList<ValidationResult> issues = ModelValidator.ValidateModel(target);

            // Assert
            var item = issues.Should().ContainSingle().Which;
            item.MemberNames.Should().Contain([nameof(PartyLookup.OrgNo), nameof(PartyLookup.Ssn)]);
            item.ErrorMessage.Should().Be(PartyLookup.SsnOrOrgNoRequiredMessage);
        }

        [Fact]
        public void TwoPropertiesSet_ReturnsIssue()
        {
            // Arrange
            PartyLookup target = new PartyLookup { Ssn = "09054300139", OrgNo = "910072218" };

            // Act
            IReadOnlyList<ValidationResult> issues = ModelValidator.ValidateModel(target);

            // Assert
            var item = issues.Should().ContainSingle().Which;
            item.MemberNames.Should().Contain(nameof(PartyLookup.OrgNo));
            item.ErrorMessage.Should().Be(PartyLookup.SsnAndOrgNoExclusiveMessage);
        }

        [Theory]
        [InlineData("1234567890")]
        [InlineData("123456789012")]
        [InlineData("F2345678901")]
        public void SsnInvalid(string ssn)
        {
            // Arrange
            PartyLookup target = new PartyLookup { Ssn = ssn };

            // Act
            IReadOnlyList<ValidationResult> issues = ModelValidator.ValidateModel(target);

            // Assert
            var item = issues.Should().ContainSingle().Which;
            item.MemberNames.Should().Contain(nameof(PartyLookup.Ssn));
            item.ErrorMessage.Should().Contain("exactly 11 digits");
        }

        [Theory]
        [InlineData("12345678")]
        [InlineData("1234567890")]
        [InlineData("F23456789")]
        public void OrgNoInvalid(string orgNo)
        {
            // Arrange
            PartyLookup target = new PartyLookup { OrgNo = orgNo };

            // Act
            IReadOnlyList<ValidationResult> issues = ModelValidator.ValidateModel(target);

            // Assert
            var item = issues.Should().ContainSingle().Which;
            item.MemberNames.Should().Contain(nameof(PartyLookup.OrgNo));
            item.ErrorMessage.Should().Contain("exactly 9 digits");
        }

        [Theory]
        [InlineData("09054300139")]
        [InlineData("27036702163")]
        public void SsnIsValid(string ssn)
        {
            // Arrange
            PartyLookup target = new PartyLookup { Ssn = ssn };

            // Act
            IReadOnlyList<ValidationResult> issues = ModelValidator.ValidateModel(target);

            // Assert
            issues.Should().BeEmpty();
        }

        [Theory]
        [InlineData("910072218")]
        [InlineData("810999012")]
        public void OrgNoIsValid(string orgNo)
        {
            // Arrange
            PartyLookup target = new PartyLookup { OrgNo = orgNo };

            // Act
            IReadOnlyList<ValidationResult> issues = ModelValidator.ValidateModel(target);

            // Assert
            issues.Should().BeEmpty();
        }
    }
}
