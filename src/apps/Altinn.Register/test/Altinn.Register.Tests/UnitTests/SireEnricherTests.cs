using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.ExternalRoles;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.Sire;
using Altinn.Register.PartyImport;
using Altinn.Register.PartyImport.A2;
using Altinn.Register.PartyImport.A2.Enrichers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Register.Tests.UnitTests;

public class SireEnricherTests
{
    private static readonly OrganizationIdentifier TestOrgId
        = OrganizationIdentifier.Parse("090090003");

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

    private static OrganizationRecord MinimalSireOrg(OrganizationIdentifier orgId)
        => new()
        {
            OwnerUuid = Guid.NewGuid(),
            PartyUuid = Guid.NewGuid(),
            PartyId = FieldValue.Unset,
            ExternalUrn = FieldValue.Unset,
            DisplayName = FieldValue.Unset,
            OrganizationIdentifier = orgId,
            PersonIdentifier = FieldValue.Unset,
            CreatedAt = FieldValue.Unset,
            ModifiedAt = FieldValue.Unset,
            IsDeleted = false,
            DeletedAt = FieldValue.Unset,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            Source = OrganizationSource.RegisteredWithSkatteetaten,
            UnitType = FieldValue.Unset,
            UnitStatus = FieldValue.Unset,
            TelephoneNumber = FieldValue.Unset,
            MobileNumber = FieldValue.Unset,
            FaxNumber = FieldValue.Unset,
            EmailAddress = FieldValue.Unset,
            InternetAddress = FieldValue.Unset,
            MailingAddress = FieldValue.Unset,
            BusinessAddress = FieldValue.Unset,
        };

    [Fact]
    public async Task Run_MapsOrganizationFields()
    {
        var sireOrg = new SireOrganization
        {
            OrganizationIdentifier = TestOrgId,
            Name = "Test AS",
            UnitType = "AS",
            UnitStatus = null,
            IsDeleted = false,
            MailingAddress = new() { Address = "Testgata 1", PostalCode = "0001", City = "OSLO" },
            LastUpdated = DateTimeOffset.UtcNow,
            BusinessRelationships = [],
        };

        var sireClient = new Mock<ISireClient>();
        sireClient
            .Setup(c => c.GetOrganization(TestOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SireOrganization>)sireOrg);

        var enricher = new SireEnricher(
            sireClient.Object,
            new Mock<IPartyPersistence>().Object,
            new Mock<IExternalRoleDefinitionPersistence>().Object,
            NullLogger<SireEnricher>.Instance);

        var party = MinimalSireOrg(TestOrgId);
        var context = new A2PartyImportSagaEnrichmentRunContext
        {
            PartyIdentifier = new ImportPartyIdentifier(TestOrgId),
            Party = party,
            RoleAssignments = [],
        };

        await enricher.Run(context, CancellationToken);

        var result = Assert.IsType<OrganizationRecord>(context.Party);
        Assert.Equal("Test AS", result.DisplayName.Value);
        Assert.Equal("AS", result.UnitType.Value);
        Assert.False(result.IsDeleted.Value);
        Assert.Equal("Testgata 1", result.MailingAddress.Value!.Address);
    }

    [Fact]
    public async Task Run_OrganizationNotFound_MarksAsDeleted()
    {
        var sireClient = new Mock<ISireClient>();
        sireClient
            .Setup(c => c.GetOrganization(TestOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Problems.OrganizationNotFound.Create());

        var enricher = new SireEnricher(
            sireClient.Object,
            new Mock<IPartyPersistence>().Object,
            new Mock<IExternalRoleDefinitionPersistence>().Object,
            NullLogger<SireEnricher>.Instance);

        var party = MinimalSireOrg(TestOrgId);
        var context = new A2PartyImportSagaEnrichmentRunContext
        {
            PartyIdentifier = new ImportPartyIdentifier(TestOrgId),
            Party = party,
            RoleAssignments = [],
        };

        await enricher.Run(context, CancellationToken);

        var result = Assert.IsType<OrganizationRecord>(context.Party);
        Assert.True(result.IsDeleted.Value);
        Assert.Equal("S", result.UnitStatus.Value);
    }

    [Fact]
    public async Task Run_RoleMapping_PersonIdentifier_MapsToAssignment()
    {
        var personId = PersonIdentifier.Parse("25871999336");
        var relatedPartyUuid = Guid.NewGuid();

        var sireOrg = new SireOrganization
        {
            OrganizationIdentifier = TestOrgId,
            Name = "Test AS",
            UnitType = "AS",
            UnitStatus = null,
            IsDeleted = false,
            MailingAddress = null,
            LastUpdated = null,
            BusinessRelationships =
            [
                new SireBusinessRelationship
                {
                    RoleIdentifier = "styreleder",
                    RelatedPersonIdentifier = personId,
                    RelatedOrganizationIdentifier = null,
                    ValidFrom = null,
                    ValidTo = null,
                }
            ],
        };

        var roleDef = new ExternalRoleDefinition
        {
            Source = ExternalRoleSource.RegisteredWithSkatteetaten,
            Identifier = "styreleder",
            Name = FakeRoleDefinition("styreleder").Name,
            Description = FakeRoleDefinition("styreleder").Description,
            Code = null,
        };

        var relatedParty = new PersonRecord
        {
            OwnerUuid = Guid.NewGuid(),
            PartyUuid = relatedPartyUuid,
            PartyId = FieldValue.Unset,
            ExternalUrn = FieldValue.Unset,
            DisplayName = "Test Person",
            PersonIdentifier = personId,
            OrganizationIdentifier = FieldValue.Unset,
            CreatedAt = FieldValue.Unset,
            ModifiedAt = FieldValue.Unset,
            IsDeleted = false,
            DeletedAt = FieldValue.Unset,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = null,
            LastName = "Person",
            ShortName = "Person Test",
            Address = null,
            MailingAddress = null,
            DateOfBirth = FieldValue.Unset,
            DateOfDeath = FieldValue.Unset,
        };

        var sireClient = new Mock<ISireClient>();
        sireClient
            .Setup(c => c.GetOrganization(TestOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SireOrganization>)sireOrg);

        var parties = new Mock<IPartyPersistence>();
        parties
            .Setup(p => p.LookupParties(
                null,
                null,
                null,
                null,
                It.IsAny<IReadOnlyList<PersonIdentifier>>(),
                null, 
                null, 
                null,
                It.IsAny<PartyFieldIncludes>(),
                It.IsAny<CancellationToken>()))
            .Returns(new[] { (PartyRecord)relatedParty }.ToAsyncEnumerable());

        var roleDefinitions = new Mock<IExternalRoleDefinitionPersistence>();
        roleDefinitions
            .Setup(r => r.TryGetRoleDefinition(
                ExternalRoleSource.RegisteredWithSkatteetaten,
                "styreleder",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleDef);

        var enricher = new SireEnricher(
            sireClient.Object,
            parties.Object,
            roleDefinitions.Object,
            NullLogger<SireEnricher>.Instance);

        var party = MinimalSireOrg(TestOrgId);
        var context = new A2PartyImportSagaEnrichmentRunContext
        {
            PartyIdentifier = new ImportPartyIdentifier(TestOrgId),
            Party = party,
            RoleAssignments = [],
        };

        await enricher.Run(context, CancellationToken);

        Assert.True(context.RoleAssignments.ContainsKey(ExternalRoleSource.RegisteredWithSkatteetaten));
        var update = Assert.IsType<PartyExternalRoleAssignmentsUpdate.Full>(
            context.RoleAssignments[ExternalRoleSource.RegisteredWithSkatteetaten]);
        Assert.Single(update.Assignments);
    }

    [Fact]
    public async Task Run_RoleMapping_RelatedPartyNotInRegister_Skipped()
    {
        var personId = PersonIdentifier.Parse("25871999336");

        var sireOrg = new SireOrganization
        {
            OrganizationIdentifier = TestOrgId,
            Name = "Test AS",
            UnitType = "AS",
            UnitStatus = null,
            IsDeleted = false,
            MailingAddress = null,
            LastUpdated = null,
            BusinessRelationships =
            [
                new SireBusinessRelationship
                {
                    RelatedPersonIdentifier = personId,
                    RelatedOrganizationIdentifier = null,
                    RoleIdentifier = "styreleder",
                    ValidFrom = null,
                    ValidTo = null,
                }
            ],
        };

        var roleDef = new ExternalRoleDefinition
        {
            Source = ExternalRoleSource.RegisteredWithSkatteetaten,
            Identifier = "styreleder",
            Name = FakeRoleDefinition("styreleder").Name,
            Description = FakeRoleDefinition("styreleder").Description,
            Code = null,
        };

        var sireClient = new Mock<ISireClient>();
        sireClient
            .Setup(c => c.GetOrganization(TestOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SireOrganization>)sireOrg);

        // Party not found in register — return empty
        var parties = new Mock<IPartyPersistence>();
        parties
            .Setup(p => p.LookupParties(
                null, 
                null, 
                null, 
                null,
                It.IsAny<IReadOnlyList<PersonIdentifier>>(),
                null, 
                null, 
                null,
                It.IsAny<PartyFieldIncludes>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<PartyRecord>());

        var roleDefinitions = new Mock<IExternalRoleDefinitionPersistence>();
        roleDefinitions
            .Setup(r => r.TryGetRoleDefinition(
                ExternalRoleSource.RegisteredWithSkatteetaten,
                "styreleder",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleDef);

        var enricher = new SireEnricher(
            sireClient.Object,
            parties.Object,
            roleDefinitions.Object,
            NullLogger<SireEnricher>.Instance);

        var party = MinimalSireOrg(TestOrgId);
        var context = new A2PartyImportSagaEnrichmentRunContext
        {
            PartyIdentifier = new ImportPartyIdentifier(TestOrgId),
            Party = party,
            RoleAssignments = [],
        };

        await enricher.Run(context, CancellationToken);

        Assert.True(context.RoleAssignments.ContainsKey(ExternalRoleSource.RegisteredWithSkatteetaten));
        var update = Assert.IsType<PartyExternalRoleAssignmentsUpdate.Full>(
            context.RoleAssignments[ExternalRoleSource.RegisteredWithSkatteetaten]);
        Assert.Single(update.Assignments);
    }

    private static ExternalRoleDefinition FakeRoleDefinition(string identifier)
    {
        var text = TranslatedText.CreateBuilder();
        text[LangCode.En] = identifier;
        text[LangCode.Nb] = identifier;
        text[LangCode.Nn] = identifier;
        var translatedText = text.ToImmutable();

        return new ExternalRoleDefinition
        {
            Source = ExternalRoleSource.RegisteredWithSkatteetaten,
            Identifier = identifier,
            Name = translatedText,
            Description = translatedText,
            Code = null,
        };
    }
}
