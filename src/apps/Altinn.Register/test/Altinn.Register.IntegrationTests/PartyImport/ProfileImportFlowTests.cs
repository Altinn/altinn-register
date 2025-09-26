using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Authorization.TestUtils.Http;
using Altinn.Register.Contracts.Parties;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.PartyImport;
using Altinn.Register.PartyImport.A2;
using Altinn.Register.TestUtils.MassTransit;
using Altinn.Register.TestUtils.TestData;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.IntegrationTests.PartyImport;

public class ProfileImportFlowTests
    : IntegrationTestBase
{
    [Fact]
    public async Task Person_HistoricalProfile()
    {
        var oldUserUuid = Guid.CreateVersion7();
        var (person, oldId) = await Setup(async (uow, ct) =>
        {
            var testDataGenerator = uow.GetRequiredService<RegisterTestDataGenerator>();
            var tracking = uow.GetRequiredService<IImportJobTracker>();

            await tracking.TrackQueueStatus("test", new() { EnqueuedMax = 100, SourceMax = 100 }, ct);

            var oldIds = await testDataGenerator.GetNextUserIds(cancellationToken: ct);
            var oldId = oldIds[0];
            var person = await uow.CreatePerson(cancellationToken: ct);

            return (person, oldId);
        });

        FakeHttpHandlers.For<IA2PartyImportService>()
            .Expect(HttpMethod.Get, "/profile/api/users/{userId}")
            .WithRouteValue("userId", oldId.ToString())
            .Respond(
                "application/json",
                $$"""
                {
                  "UserId": {{oldId}},
                  "UserUUID": null,
                  "UserType": 1,
                  "UserName": "",
                  "ExternalIdentity": "",
                  "IsReserved": false,
                  "PhoneNumber": null,
                  "Email": null,
                  "PartyId": {{person.PartyId.Value}},
                  "Party": {
                    "PartyTypeName": 1,
                    "SSN": "{{person.PersonIdentifier.Value}}",
                    "OrgNumber": "",
                    "Person": {
                      "SSN": "{{person.PersonIdentifier.Value}}",
                      "Name": "{{person.ShortName.Value}}",
                      "FirstName": "{{person.FirstName.Value}}",
                      "MiddleName": "{{person.MiddleName.Value}}",
                      "LastName": "{{person.LastName.Value}}",
                      "TelephoneNumber": "",
                      "MobileNumber": "",
                      "MailingAddress": "Amalie Jessens vei 26",
                      "MailingPostalCode": "3182",
                      "MailingPostalCity": "HORTEN",
                      "AddressMunicipalNumber": "",
                      "AddressMunicipalName": "",
                      "AddressStreetName": "",
                      "AddressHouseNumber": "",
                      "AddressHouseLetter": "",
                      "AddressPostalCode": "3182",
                      "AddressCity": "HORTEN",
                      "DateOfDeath": null
                    },
                    "Organization": null,
                    "PartyId": {{person.PartyId.Value}},
                    "PartyUUID": "{{person.PartyUuid.Value}}",
                    "UnitType": null,
                    "LastChangedInAltinn": "2009-06-06T15:12:18.787+02:00",
                    "LastChangedInExternalRegister": null,
                    "Name": "{{person.ShortName.Value}}",
                    "IsDeleted": false,
                    "OnlyHierarchyElementWithNoAccess": false,
                    "ChildParties": null
                  },
                  "ProfileSettingPreference": {
                    "Language": "nb",
                    "PreSelectedPartyId": 0,
                    "DoNotPromptForParty": false
                  }
                }
                """);

        var cmd = new ImportA2UserProfileCommand
        {
            UserId = oldId,
            OwnerPartyUuid = person.PartyUuid.Value,
            IsDeleted = true,
            Tracking = new("test", 10),
        };

        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);

        var conversation = await TestHarness.Conversation(cmd, TestContext.Current.CancellationToken);
        var consumed = await conversation.Commands.OfType<UpsertUserRecordCommand>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        var evt = await conversation.Events.Completed.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        consumed.ShouldNotBeNull();
        evt.ShouldBeNull();
    }

    [Fact]
    public async Task Person_ActiveProfile()
    {
        var person = await Setup(async (uow, ct) =>
        {
            var testDataGenerator = uow.GetRequiredService<RegisterTestDataGenerator>();
            var tracking = uow.GetRequiredService<IImportJobTracker>();

            await tracking.TrackQueueStatus("test", new() { EnqueuedMax = 100, SourceMax = 100 }, ct);

            var ids = await testDataGenerator.GetNextUserIds(1, cancellationToken: ct);
            var person = await uow.CreatePerson(
                user: new PartyUserRecord(ids[0], "test-user"),
                cancellationToken: ct);

            return person;
        });

        var userId = person.User.Value!.UserId.Value;

        FakeHttpHandlers.For<IA2PartyImportService>()
            .Expect(HttpMethod.Get, "/profile/api/users/{userId}")
            .WithRouteValue("userId", userId.ToString())
            .Respond(
                "application/json",
                $$"""
                {
                  "UserId": {{userId}},
                  "UserUUID": null,
                  "UserType": 1,
                  "UserName": "",
                  "ExternalIdentity": "",
                  "IsReserved": false,
                  "PhoneNumber": null,
                  "Email": null,
                  "PartyId": {{person.PartyId.Value}},
                  "Party": {
                    "PartyTypeName": 1,
                    "SSN": "{{person.PersonIdentifier.Value}}",
                    "OrgNumber": "",
                    "Person": {
                      "SSN": "{{person.PersonIdentifier.Value}}",
                      "Name": "{{person.ShortName.Value}}",
                      "FirstName": "{{person.FirstName.Value}}",
                      "MiddleName": "{{person.MiddleName.Value}}",
                      "LastName": "{{person.LastName.Value}}",
                      "TelephoneNumber": "",
                      "MobileNumber": "",
                      "MailingAddress": "Amalie Jessens vei 26",
                      "MailingPostalCode": "3182",
                      "MailingPostalCity": "HORTEN",
                      "AddressMunicipalNumber": "",
                      "AddressMunicipalName": "",
                      "AddressStreetName": "",
                      "AddressHouseNumber": "",
                      "AddressHouseLetter": "",
                      "AddressPostalCode": "3182",
                      "AddressCity": "HORTEN",
                      "DateOfDeath": null
                    },
                    "Organization": null,
                    "PartyId": {{person.PartyId.Value}},
                    "PartyUUID": "{{person.PartyUuid.Value}}",
                    "UnitType": null,
                    "LastChangedInAltinn": "2009-06-06T15:12:18.787+02:00",
                    "LastChangedInExternalRegister": null,
                    "Name": "{{person.ShortName.Value}}",
                    "IsDeleted": false,
                    "OnlyHierarchyElementWithNoAccess": false,
                    "ChildParties": null
                  },
                  "ProfileSettingPreference": {
                    "Language": "nb",
                    "PreSelectedPartyId": 0,
                    "DoNotPromptForParty": false
                  }
                }
                """);

        var cmd = new ImportA2UserProfileCommand
        {
            UserId = userId,
            OwnerPartyUuid = person.PartyUuid.Value,
            IsDeleted = false,
            Tracking = new("test", 10),
        };

        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);

        var conversation = await TestHarness.Conversation(cmd, TestContext.Current.CancellationToken);
        var consumed = await conversation.Commands.OfType<UpsertUserRecordCommand>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        var evt = await conversation.Events.Completed.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        consumed.ShouldNotBeNull();
        evt.ShouldNotBeNull();
        evt.Party.PartyUuid.ShouldBe(person.PartyUuid.Value);

        await Check(async (uow, ct) =>
        {
            var persistence = uow.GetPartyPersistence();
            var updated = await persistence.GetPartyById(person.PartyUuid.Value, PartyFieldIncludes.User, ct).FirstOrDefaultAsync(ct);
            updated.ShouldNotBeNull();
            updated.User.ShouldHaveValue();
            updated.User.Value!.Username.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Person_ActiveProfile_Deactivated()
    {
        var person = await Setup(async (uow, ct) =>
        {
            var testDataGenerator = uow.GetRequiredService<RegisterTestDataGenerator>();
            var tracking = uow.GetRequiredService<IImportJobTracker>();

            await tracking.TrackQueueStatus("test", new() { EnqueuedMax = 100, SourceMax = 100 }, ct);

            var person = await uow.CreatePerson(cancellationToken: ct);

            return person;
        });

        var userId = person.User.Value!.UserId.Value;

        FakeHttpHandlers.For<IA2PartyImportService>()
            .Expect(HttpMethod.Get, "/profile/api/users/{userId}")
            .WithRouteValue("userId", userId.ToString())
            .Respond(
                "application/json",
                $$"""
                {
                  "UserId": {{userId}},
                  "UserUUID": null,
                  "UserType": 1,
                  "UserName": "",
                  "ExternalIdentity": "",
                  "IsReserved": false,
                  "PhoneNumber": null,
                  "Email": null,
                  "PartyId": {{person.PartyId.Value}},
                  "Party": {
                    "PartyTypeName": 1,
                    "SSN": "{{person.PersonIdentifier.Value}}",
                    "OrgNumber": "",
                    "Person": {
                      "SSN": "{{person.PersonIdentifier.Value}}",
                      "Name": "{{person.ShortName.Value}}",
                      "FirstName": "{{person.FirstName.Value}}",
                      "MiddleName": "{{person.MiddleName.Value}}",
                      "LastName": "{{person.LastName.Value}}",
                      "TelephoneNumber": "",
                      "MobileNumber": "",
                      "MailingAddress": "Amalie Jessens vei 26",
                      "MailingPostalCode": "3182",
                      "MailingPostalCity": "HORTEN",
                      "AddressMunicipalNumber": "",
                      "AddressMunicipalName": "",
                      "AddressStreetName": "",
                      "AddressHouseNumber": "",
                      "AddressHouseLetter": "",
                      "AddressPostalCode": "3182",
                      "AddressCity": "HORTEN",
                      "DateOfDeath": null
                    },
                    "Organization": null,
                    "PartyId": {{person.PartyId.Value}},
                    "PartyUUID": "{{person.PartyUuid.Value}}",
                    "UnitType": null,
                    "LastChangedInAltinn": "2009-06-06T15:12:18.787+02:00",
                    "LastChangedInExternalRegister": null,
                    "Name": "{{person.ShortName.Value}}",
                    "IsDeleted": false,
                    "OnlyHierarchyElementWithNoAccess": false,
                    "ChildParties": null
                  },
                  "ProfileSettingPreference": {
                    "Language": "nb",
                    "PreSelectedPartyId": 0,
                    "DoNotPromptForParty": false
                  }
                }
                """);

        var cmd = new ImportA2UserProfileCommand
        {
            UserId = userId,
            OwnerPartyUuid = person.PartyUuid.Value,
            IsDeleted = true,
            Tracking = new("test", 10),
        };

        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);

        var conversation = await TestHarness.Conversation(cmd, TestContext.Current.CancellationToken);
        var consumed = await conversation.Commands.OfType<UpsertUserRecordCommand>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        var evt = await conversation.Events.Completed.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        consumed.ShouldNotBeNull();
        evt.ShouldNotBeNull();
        evt.Party.PartyUuid.ShouldBe(person.PartyUuid.Value);

        await Check(async (uow, ct) =>
        {
            var persistence = uow.GetPartyPersistence();
            var updated = await persistence.GetPartyById(person.PartyUuid.Value, PartyFieldIncludes.User, ct).FirstOrDefaultAsync(ct);
            updated.ShouldNotBeNull();
            updated.User.ShouldBeNull();
        });
    }

    [Fact]
    public async Task SelfIdentified_ActiveProfile()
    {
        var siUser = await Setup(async (uow, ct) =>
        {
            var testDataGenerator = uow.GetRequiredService<RegisterTestDataGenerator>();
            var tracking = uow.GetRequiredService<IImportJobTracker>();

            await tracking.TrackQueueStatus("test", new() { EnqueuedMax = 100, SourceMax = 100 }, ct);

            var ids = await testDataGenerator.GetNextUserIds(1, cancellationToken: ct);
            var siUser = await uow.CreateSelfIdentifiedUser(
                user: new PartyUserRecord(ids[0], "TestSelfIdentifiedUser"),
                cancellationToken: ct);

            return siUser;
        });

        var userId = siUser.User.Value!.UserId.Value;

        FakeHttpHandlers.For<IA2PartyImportService>()
            .Expect(HttpMethod.Get, "/profile/api/users/{userId}")
            .WithRouteValue("userId", userId.ToString())
            .Respond(
                "application/json",
                $$"""
                {
                  "UserId": {{userId}},
                  "UserUUID": "{{siUser.PartyUuid}}",
                  "UserType": 2,
                  "UserName": "updated-name",
                  "ExternalIdentity": "",
                  "IsReserved": false,
                  "PhoneNumber": null,
                  "Email": null,
                  "PartyId": {{siUser.PartyId.Value}},
                  "Party": {
                    "PartyTypeName": 3,
                    "SSN": "",
                    "OrgNumber": "",
                    "Person": null,
                    "Organization": null,
                    "PartyId": {{siUser.PartyId.Value}},
                    "PartyUUID": "{{siUser.PartyUuid.Value}}",
                    "UnitType": null,
                    "LastChangedInAltinn": "2010-03-02T01:53:44.87+01:00",
                    "LastChangedInExternalRegister": null,
                    "Name": "updated-name",
                    "IsDeleted": false,
                    "OnlyHierarchyElementWithNoAccess": false,
                    "ChildParties": null
                  },
                  "ProfileSettingPreference": {
                    "Language": "nb",
                    "PreSelectedPartyId": 0,
                    "DoNotPromptForParty": false
                  }
                }
                """);

        var cmd = new ImportA2UserProfileCommand
        {
            UserId = userId,
            OwnerPartyUuid = siUser.PartyUuid.Value,
            IsDeleted = false,
            Tracking = new("test", 10),
        };

        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);

        var conversation = await TestHarness.Conversation(cmd, TestContext.Current.CancellationToken);
        var consumed = await conversation.Commands.OfType<UpsertValidatedPartyCommand>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        var evt = await conversation.Events.Completed.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        consumed.ShouldNotBeNull();
        evt.ShouldNotBeNull();
        evt.Party.PartyUuid.ShouldBe(siUser.PartyUuid.Value);

        await Check(async (uow, ct) =>
        {
            var persistence = uow.GetPartyPersistence();
            var updated = await persistence.GetPartyById(siUser.PartyUuid.Value, PartyFieldIncludes.User | PartyFieldIncludes.Party, ct).FirstOrDefaultAsync(ct);
            updated.ShouldNotBeNull();
            updated.User.ShouldHaveValue();
            updated.User.Value!.Username.ShouldBe("updated-name");
            updated.DisplayName.ShouldBe("updated-name");
            updated.IsDeleted.ShouldBe(false);
        });
    }

    [Fact]
    public async Task SelfIdentified_InactiveProfile()
    {
        var siUser = await Setup(async (uow, ct) =>
        {
            var testDataGenerator = uow.GetRequiredService<RegisterTestDataGenerator>();
            var tracking = uow.GetRequiredService<IImportJobTracker>();

            await tracking.TrackQueueStatus("test", new() { EnqueuedMax = 100, SourceMax = 100 }, ct);

            var ids = await testDataGenerator.GetNextUserIds(1, cancellationToken: ct);
            var siUser = await uow.CreateSelfIdentifiedUser(
                user: new PartyUserRecord(ids[0], "TestSelfIdentifiedUser"),
                cancellationToken: ct);

            return siUser;
        });

        var userId = siUser.User.Value!.UserId.Value;

        FakeHttpHandlers.For<IA2PartyImportService>()
            .Expect(HttpMethod.Get, "/profile/api/users/{userId}")
            .WithRouteValue("userId", userId.ToString())
            .Respond(
                "application/json",
                $$"""
                {
                  "UserId": {{userId}},
                  "UserUUID": "{{siUser.PartyUuid}}",
                  "UserType": 2,
                  "UserName": "updated-name",
                  "ExternalIdentity": "",
                  "IsReserved": false,
                  "PhoneNumber": null,
                  "Email": null,
                  "PartyId": {{siUser.PartyId.Value}},
                  "Party": {
                    "PartyTypeName": 3,
                    "SSN": "",
                    "OrgNumber": "",
                    "Person": null,
                    "Organization": null,
                    "PartyId": {{siUser.PartyId.Value}},
                    "PartyUUID": "{{siUser.PartyUuid.Value}}",
                    "UnitType": null,
                    "LastChangedInAltinn": "2010-03-02T01:53:44.87+01:00",
                    "LastChangedInExternalRegister": null,
                    "Name": "updated-name",
                    "IsDeleted": false,
                    "OnlyHierarchyElementWithNoAccess": false,
                    "ChildParties": null
                  },
                  "ProfileSettingPreference": {
                    "Language": "nb",
                    "PreSelectedPartyId": 0,
                    "DoNotPromptForParty": false
                  }
                }
                """);

        var cmd = new ImportA2UserProfileCommand
        {
            UserId = userId,
            OwnerPartyUuid = siUser.PartyUuid.Value,
            IsDeleted = true,
            Tracking = new("test", 10),
        };

        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);

        var conversation = await TestHarness.Conversation(cmd, TestContext.Current.CancellationToken);
        var consumed = await conversation.Commands.OfType<UpsertValidatedPartyCommand>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        var evt = await conversation.Events.Completed.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        consumed.ShouldNotBeNull();
        evt.ShouldNotBeNull();
        evt.Party.PartyUuid.ShouldBe(siUser.PartyUuid.Value);

        await Check(async (uow, ct) =>
        {
            var persistence = uow.GetPartyPersistence();
            var updated = await persistence.GetPartyById(siUser.PartyUuid.Value, PartyFieldIncludes.User | PartyFieldIncludes.Party, ct).FirstOrDefaultAsync(ct);
            updated.ShouldNotBeNull();
            updated.User.ShouldHaveValue();
            updated.User.Value!.Username.ShouldBeNull();
            updated.DisplayName.ShouldBe("updated-name");
            updated.IsDeleted.ShouldBe(true);
        });
    }

    [Fact]
    public async Task Enterprise_ActiveProfile()
    {
        var uuid = Guid.NewGuid();
        var (org, id) = await Setup(async (uow, ct) =>
        {
            var testDataGenerator = uow.GetRequiredService<RegisterTestDataGenerator>();
            var tracking = uow.GetRequiredService<IImportJobTracker>();

            await tracking.TrackQueueStatus("test", new() { EnqueuedMax = 100, SourceMax = 100 }, ct);

            var ids = await testDataGenerator.GetNextUserIds(1, cancellationToken: ct);
            var org = await uow.CreateOrg(cancellationToken: ct);

            return (org, ids[0]);
        });

        FakeHttpHandlers.For<IA2PartyImportService>()
            .Expect(HttpMethod.Get, "/profile/api/users/{userId}")
            .WithRouteValue("userId", id.ToString())
            .Respond(
                "application/json",
                $$"""
                {
                  "UserId": {{id}},
                  "UserUUID": "{{uuid}}",
                  "UserType": 3,
                  "UserName": "enterprise-user-name",
                  "ExternalIdentity": "",
                  "IsReserved": false,
                  "PhoneNumber": null,
                  "Email": null,
                  "PartyId": {{org.PartyId.Value}},
                  "Party": {
                    "PartyTypeName": 2,
                    "SSN": "",
                    "OrgNumber": "{{org.OrganizationIdentifier.Value}}",
                    "Person": null,
                    "Organization": {
                      "OrgNumber": "{{org.OrganizationIdentifier.Value}}",
                      "Name": "KYSTBASEN ÅGOTNES OG ILSENG",
                      "UnitType": "ASA",
                      "TelephoneNumber": "12345678",
                      "MobileNumber": "99999999",
                      "FaxNumber": "12345679",
                      "EMailAddress": "test@test.test",
                      "InternetAddress": null,
                      "MailingAddress": "Markalléen 19",
                      "MailingPostalCode": "1368",
                      "MailingPostalCity": "STABEKK",
                      "BusinessAddress": "Markalléen 19",
                      "BusinessPostalCode": "1368",
                      "BusinessPostalCity": "STABEKK",
                      "UnitStatus": "N",
                      "Established": "2017-09-11T00:00:00+02:00"
                    },
                    "PartyId": {{org.PartyId.Value}},
                    "PartyUUID": "{{org.PartyUuid.Value}}",
                    "UnitType": "ASA",
                    "LastChangedInAltinn": "2023-02-08T10:22:30.973+01:00",
                    "LastChangedInExternalRegister": "2017-09-11T00:00:00+02:00",
                    "Name": "KYSTBASEN ÅGOTNES OG ILSENG",
                    "IsDeleted": false,
                    "OnlyHierarchyElementWithNoAccess": false,
                    "ChildParties": null
                  },
                  "ProfileSettingPreference": {
                    "Language": "nb",
                    "PreSelectedPartyId": 0,
                    "DoNotPromptForParty": false
                  }
                }
                """);

        var cmd = new ImportA2UserProfileCommand
        {
            UserId = id,
            OwnerPartyUuid = org.PartyUuid.Value,
            IsDeleted = false,
            Tracking = new("test", 10),
        };

        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);

        var conversation = await TestHarness.Conversation(cmd, TestContext.Current.CancellationToken);
        var consumed = await conversation.Commands.OfType<UpsertValidatedPartyCommand>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        var evt = await conversation.Events.Completed.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        consumed.ShouldNotBeNull();
        evt.ShouldNotBeNull();
        evt.Party.PartyUuid.ShouldBe(uuid);

        await Check(async (uow, ct) =>
        {
            var persistence = uow.GetPartyPersistence();
            var updated = await persistence.GetPartyById(uuid, PartyFieldIncludes.User | PartyFieldIncludes.Party, ct).FirstOrDefaultAsync(ct);
            updated.ShouldNotBeNull();
            updated.ShouldBeOfType<EnterpriseUserRecord>();
            updated.OwnerUuid.ShouldBe(org.PartyUuid.Value);
            updated.User.ShouldHaveValue();
            updated.User.Value!.Username.ShouldBe("enterprise-user-name");
            updated.DisplayName.ShouldBe("enterprise-user-name");
            updated.IsDeleted.ShouldBe(false);
        });
    }

    [Fact]
    public async Task Enterprise_InactiveProfile()
    {
        var uuid = Guid.NewGuid();
        var (org, id) = await Setup(async (uow, ct) =>
        {
            var testDataGenerator = uow.GetRequiredService<RegisterTestDataGenerator>();
            var tracking = uow.GetRequiredService<IImportJobTracker>();

            await tracking.TrackQueueStatus("test", new() { EnqueuedMax = 100, SourceMax = 100 }, ct);

            var ids = await testDataGenerator.GetNextUserIds(1, cancellationToken: ct);
            var org = await uow.CreateOrg(cancellationToken: ct);

            return (org, ids[0]);
        });

        FakeHttpHandlers.For<IA2PartyImportService>()
            .Expect(HttpMethod.Get, "/profile/api/users/{userId}")
            .WithRouteValue("userId", id.ToString())
            .Respond(
                "application/json",
                $$"""
                {
                  "UserId": {{id}},
                  "UserUUID": "{{uuid}}",
                  "UserType": 3,
                  "UserName": "enterprise-user-name",
                  "ExternalIdentity": "",
                  "IsReserved": false,
                  "PhoneNumber": null,
                  "Email": null,
                  "PartyId": {{org.PartyId.Value}},
                  "Party": {
                    "PartyTypeName": 2,
                    "SSN": "",
                    "OrgNumber": "{{org.OrganizationIdentifier.Value}}",
                    "Person": null,
                    "Organization": {
                      "OrgNumber": "{{org.OrganizationIdentifier.Value}}",
                      "Name": "KYSTBASEN ÅGOTNES OG ILSENG",
                      "UnitType": "ASA",
                      "TelephoneNumber": "12345678",
                      "MobileNumber": "99999999",
                      "FaxNumber": "12345679",
                      "EMailAddress": "test@test.test",
                      "InternetAddress": null,
                      "MailingAddress": "Markalléen 19",
                      "MailingPostalCode": "1368",
                      "MailingPostalCity": "STABEKK",
                      "BusinessAddress": "Markalléen 19",
                      "BusinessPostalCode": "1368",
                      "BusinessPostalCity": "STABEKK",
                      "UnitStatus": "N",
                      "Established": "2017-09-11T00:00:00+02:00"
                    },
                    "PartyId": {{org.PartyId.Value}},
                    "PartyUUID": "{{org.PartyUuid.Value}}",
                    "UnitType": "ASA",
                    "LastChangedInAltinn": "2023-02-08T10:22:30.973+01:00",
                    "LastChangedInExternalRegister": "2017-09-11T00:00:00+02:00",
                    "Name": "KYSTBASEN ÅGOTNES OG ILSENG",
                    "IsDeleted": false,
                    "OnlyHierarchyElementWithNoAccess": false,
                    "ChildParties": null
                  },
                  "ProfileSettingPreference": {
                    "Language": "nb",
                    "PreSelectedPartyId": 0,
                    "DoNotPromptForParty": false
                  }
                }
                """);

        var cmd = new ImportA2UserProfileCommand
        {
            UserId = id,
            OwnerPartyUuid = org.PartyUuid.Value,
            IsDeleted = true,
            Tracking = new("test", 10),
        };

        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);

        var conversation = await TestHarness.Conversation(cmd, TestContext.Current.CancellationToken);
        var consumed = await conversation.Commands.OfType<UpsertValidatedPartyCommand>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        var evt = await conversation.Events.Completed.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        consumed.ShouldNotBeNull();
        evt.ShouldNotBeNull();
        evt.Party.PartyUuid.ShouldBe(uuid);

        await Check(async (uow, ct) =>
        {
            var persistence = uow.GetPartyPersistence();
            var updated = await persistence.GetPartyById(uuid, PartyFieldIncludes.User | PartyFieldIncludes.Party, ct).FirstOrDefaultAsync(ct);
            updated.ShouldNotBeNull();
            updated.ShouldBeOfType<EnterpriseUserRecord>();
            updated.OwnerUuid.ShouldBe(org.PartyUuid.Value);
            updated.User.ShouldHaveValue();
            updated.User.Value!.Username.ShouldBeNull();
            updated.DisplayName.ShouldBe("enterprise-user-name");
            updated.IsDeleted.ShouldBe(true);
        });
    }

    [Fact]
    public async Task Absorbed_SelfIdentified_User()
    {
        var person = await Setup(async (uow, ct) =>
        {
            await uow.GetRequiredService<IImportJobTracker>().TrackQueueStatus("test", new() { SourceMax = 100, EnqueuedMax = 100 }, ct);
            return await uow.CreatePerson(cancellationToken: ct);
        });

        Debug.Assert(person.User.HasValue);
        Debug.Assert(person.User.Value.UserId.HasValue);
        Debug.Assert(person.User.Value.Username.IsNull);

        var username = "the-user-name";
        var siUuid = Guid.NewGuid();
        var siPartyId = (await GetRequiredService<RegisterTestDataGenerator>().GetNextUserIds(cancellationToken: CancellationToken))[0];
        var siUserId = (await GetRequiredService<RegisterTestDataGenerator>().GetNextUserIds(cancellationToken: CancellationToken))[0];
        var pUserId = person.User.Value.UserId.Value;
        var pUuid = person.PartyUuid.Value;

        var profileJson =
            $$"""
            {
                "UserId": {{siUserId}},
                "UserUUID": "{{siUuid}}",
                "UserType": 2,
                "UserName": "{{username}}",
                "ExternalIdentity": "",
                "IsReserved": false,
                "PhoneNumber": null,
                "Email": null,
                "PartyId": {{siPartyId}},
                "Party": {
                    "PartyTypeName": 3,
                    "SSN": "",
                    "OrgNumber": "",
                    "Person": null,
                    "Organization": null,
                    "PartyId": {{siPartyId}},
                    "PartyUUID": "{{siUuid}}",
                    "UnitType": null,
                    "LastChangedInAltinn": "2010-03-02T01:53:44.87+01:00",
                    "LastChangedInExternalRegister": null,
                    "Name": "{{username}}",
                    "IsDeleted": false,
                    "OnlyHierarchyElementWithNoAccess": false,
                    "ChildParties": null
                },
                "ProfileSettingPreference": {
                    "Language": "nb",
                    "PreSelectedPartyId": 0,
                    "DoNotPromptForParty": false
                }
            }
            """;

        FakeHttpHandlers.For<IA2PartyImportService>()
            .Expect(HttpMethod.Get, "/profile/api/users/{userId}")
            .WithRouteValue("userId", siUserId.ToString())
            .Respond("application/json", profileJson);

        FakeHttpHandlers.For<IA2PartyImportService>()
            .Expect(HttpMethod.Get, "/profile/api/users/{userId}")
            .WithRouteValue("userId", siUserId.ToString())
            .Respond("application/json", profileJson);

        // First: SI user is created
        await UpsertParty(new ImportA2UserProfileCommand
        {
            UserId = siUserId,
            OwnerPartyUuid = siUuid,
            IsDeleted = false,
            Tracking = new("test", 10),
        });

        await Check(async (uow, ct) =>
        {
            var party = await uow.GetPartyPersistence().GetPartyById(siUuid, PartyFieldIncludes.Party | PartyFieldIncludes.User, ct).FirstOrDefaultAsync(ct);
            var si = party.ShouldNotBeNull().ShouldBeOfType<SelfIdentifiedUserRecord>();
            si.User.ShouldHaveValue();
            si.User.Value!.Username.ShouldBe(username);
        });

        // Second: SI user is deleted
        await UpsertParty(new ImportA2UserProfileCommand
        {
            UserId = siUserId,
            OwnerPartyUuid = siUuid,
            IsDeleted = true,
            Tracking = new("test", 11),
        });

        await Check(async (uow, ct) =>
        {
            var party = await uow.GetPartyPersistence().GetPartyById(siUuid, PartyFieldIncludes.Party | PartyFieldIncludes.User, ct).FirstOrDefaultAsync(ct);
            var si = party.ShouldNotBeNull().ShouldBeOfType<SelfIdentifiedUserRecord>();
            si.User.ShouldHaveValue();
            si.User.Value!.Username.ShouldBeNull();
        });

        // Last: Person is updated with username from deleted SI user
        await UpsertParty(new UpsertPartyCommand
        {
            Party = person with 
            {
                User = new PartyUserRecord(userId: pUserId, username: username, userIds: ImmutableValueArray.Create(pUserId)),
            },
            Tracking = new("test", 12),
        });

        await Check(async (uow, ct) =>
        {
            var party = await uow.GetPartyPersistence().GetPartyById(pUuid, PartyFieldIncludes.Party | PartyFieldIncludes.User, ct).FirstOrDefaultAsync(ct);
            var pers = party.ShouldNotBeNull().ShouldBeOfType<PersonRecord>();
            pers.User.ShouldHaveValue();
            pers.User.Value!.Username.ShouldBe(username);
        });
    }

    private async Task<PartyUpdatedEvent> UpsertParty<T>(T cmd)
        where T : CommandBase
    {
        await CommandSender.Send(cmd, cancellationToken: CancellationToken);
        var conversation = await TestHarness.Conversation(cmd, CancellationToken);
        var evt = await conversation.Events.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(CancellationToken);

        evt.ShouldNotBeNull($"{nameof(PartyUpdatedEvent)} was not published");
        return evt;
    }
}
