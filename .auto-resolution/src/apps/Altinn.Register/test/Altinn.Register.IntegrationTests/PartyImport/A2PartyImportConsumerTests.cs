using System.Net.Http.Json;
using Altinn.Authorization.ServiceDefaults.MassTransit.Commands;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.PartyImport;
using Altinn.Register.PartyImport.A2;
using Altinn.Register.TestUtils.Http;
using Altinn.Register.TestUtils.MassTransit;
using Altinn.Register.TestUtils.TestData;
using Xunit.Sdk;
using V1Models = Altinn.Register.Contracts.V1;

namespace Altinn.Register.IntegrationTests.PartyImport;

public class A2PartyImportConsumerTests
    : IntegrationTestBase
{
    private ICommandQueueResolver CommandQueueResolver
        => GetRequiredService<ICommandQueueResolver>();

    [Fact]
    public async Task ImportA2PartyCommand_FetchesParty_AndSendsUpsertCommand()
    {
        var partyId = 50004216U;
        var partyUuid = Guid.Parse("7aa53da8-836c-4812-afcb-76d39f5ebb0e");
        FakeHttpHandlers.For<IA2PartyImportService>().Expect(HttpMethod.Get, "/register/api/parties")
            .WithQuery("partyuuid", partyUuid.ToString())
            .Respond(
                "application/json",
                """
                {
                  "PartyTypeName": 2,
                  "SSN": "",
                  "OrgNumber": "311654306",
                  "Person": null,
                  "Organization": {
                    "OrgNumber": "311654306",
                    "Name": "TYNSET OG OPPDAL",
                    "UnitType": "ANS",
                    "UnitStatus": "N",
                    "TelephoneNumber": "22077000",
                    "MobileNumber": "99000000",
                    "FaxNumber": "22077108",
                    "EMailAddress": "tynset_og_oppdal@example.com",
                    "InternetAddress": "tynset-og-oppdal.example.com",
                    "MailingAddress": "Postboks 6662 St. Bergens plass",
                    "MailingPostalCode": "1666",
                    "MailingPostalCity": "Bergen",
                    "BusinessAddress": "Postboks 6662 St. Olavs plass",
                    "BusinessPostalCode": "0555",
                    "BusinessPostalCity": "Oslo"
                  },
                  "PartyId": 50004216,
                  "PartyUuid": "7aa53da8-836c-4812-afcb-76d39f5ebb0e",
                  "UnitType": "ANS",
                  "Name": "TYNSET OG OPPDAL",
                  "IsDeleted": false,
                  "OnlyHierarchyElementWithNoAccess": false,
                  "ChildParties": null
                }
                """);

        await CommandSender.Send(new ImportA2PartyCommand { PartyUuid = partyUuid, ChangeId = 1, ChangedTime = TimeProvider.GetUtcNow() }, TestContext.Current.CancellationToken);

        Assert.True(await TestHarness.Consumed.Any<ImportA2PartyCommand>(TestContext.Current.CancellationToken));
        var sent = await TestHarness.Sent.SelectAsync<UpsertPartyCommand>(TestContext.Current.CancellationToken).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(sent);

        sent.Context.Message.Party.PartyId.ShouldBe(partyId);
        sent.Context.Message.Party.PartyUuid.ShouldBe(partyUuid);
        sent.Context.DestinationAddress.ShouldBe(CommandQueueResolver.GetQueueUriForCommandType<UpsertPartyCommand>());
    }

    [Fact]
    public async Task ImportA2PartyCommand_FetchesParty_AndSendsUpsertCommand_SIUser()
    {
        var partyId = 50006237U;
        var partyUuid = Guid.Parse("4fe860c4-bc65-4d2f-a288-f825b460f26b");
        FakeHttpHandlers.For<IA2PartyImportService>().Expect(HttpMethod.Get, "/register/api/parties")
            .WithQuery("partyuuid", partyUuid.ToString())
            .Respond(
                "application/json",
                """
                {
                  "PartyTypeName": 3,
                  "SSN": "",
                  "OrgNumber": "",
                  "Person": null,
                  "Organization": null,
                  "PartyId": 50006237,
                  "PartyUUID": "4fe860c4-bc65-4d2f-a288-f825b460f26b",
                  "UnitType": null,
                  "LastChangedInAltinn": "2010-03-02T01:53:44.87+01:00",
                  "LastChangedInExternalRegister": null,
                  "Name": "TestSelfIdentifiedUser",
                  "IsDeleted": false,
                  "OnlyHierarchyElementWithNoAccess": false,
                  "ChildParties": null
                }
                """);

        await CommandSender.Send(new ImportA2PartyCommand { PartyUuid = partyUuid, ChangeId = 1, ChangedTime = TimeProvider.GetUtcNow() }, TestContext.Current.CancellationToken);

        Assert.True(await TestHarness.Consumed.Any<ImportA2PartyCommand>(TestContext.Current.CancellationToken));
        var sent = await TestHarness.Sent.SelectAsync<UpsertPartyCommand>(TestContext.Current.CancellationToken).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(sent);

        sent.Context.Message.Party.PartyId.ShouldBe(partyId);
        sent.Context.Message.Party.PartyUuid.ShouldBe(partyUuid);
        sent.Context.Message.Party.PartyType.ShouldBe(PartyRecordType.SelfIdentifiedUser);
        sent.Context.DestinationAddress.ShouldBe(CommandQueueResolver.GetQueueUriForCommandType<UpsertPartyCommand>());
    }

    [Fact]
    public async Task ImportA2UserIdForPartyCommand_ForPerson_FetchesUserId_AndSendsUpsertCommand()
    {
        var partyUuid = Guid.CreateVersion7();
        var partyType = PartyRecordType.Person;

        FakeHttpHandlers.For<IA2PartyImportService>().Expect(HttpMethod.Get, "/profile/api/users/getorcreate/{partyUuid:guid}")
            .WithRouteValue("partyUuid", partyUuid.ToString())
            .Respond(
                "application/json",
                $$"""
                {
                  "UserId": 20002571,
                  "UserUUID": "{{partyUuid}}",
                  "UserType": 2,
                  "UserName": "AdvancedSettingsTest",
                  "ExternalIdentity": "",
                  "IsReserved": false,
                  "PhoneNumber": null,
                  "Email": "AdvancedSettingsTest@AdvancedSettingsTest.no",
                  "PartyId": 50068492,
                  "Party": {
                    "PartyTypeName": 1,
                    "SSN": "",
                    "OrgNumber": "",
                    "Person": null,
                    "Organization": null,
                    "PartyId": 50068492,
                    "PartyUUID": "{{partyUuid}}",
                    "UnitType": null,
                    "LastChangedInAltinn": "2021-02-08T05:07:09.677+01:00",
                    "LastChangedInExternalRegister": null,
                    "Name": "AdvancedSettingsTest",
                    "IsDeleted": false,
                    "OnlyHierarchyElementWithNoAccess": false,
                    "ChildParties": null
                  },
                  "ProfileSettingPreference": {
                    "Language": "nb",
                    "PreSelectedPartyId": 0,
                    "DoNotPromptForParty": true
                  }
                }
                """);

        var cmd = new ImportA2UserIdForPartyCommand
        {
            PartyUuid = partyUuid,
            PartyType = partyType,
            Tracking = new("test", 101),
        };

        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);
        var conversation = await TestHarness.Conversation(cmd, TestContext.Current.CancellationToken);

        var upsertCommand = await conversation.Commands.OfType<UpsertPartyUserCommand>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        upsertCommand.ShouldNotBeNull();
        upsertCommand.PartyUuid.ShouldBe(partyUuid);
        upsertCommand.User.UserId.ShouldBe(20002571U);
    }

    [Fact]
    public async Task ImportA2CCRRolesCommand_FetchesRoles_AndSendsUpsertCommand()
    {
        await GetRequiredService<IImportJobTracker>().TrackQueueStatus(
            JobNames.A2PartyImportCCRRoleAssignments, 
            new()
            {
                SourceMax = 10,
                EnqueuedMax = 1,
            }, 
            TestContext.Current.CancellationToken);

        var (org, person1, person2) = await Setup(async (uow, ct) =>
        {
            var org = await uow.CreateOrg(cancellationToken: ct);
            var person1 = await uow.CreatePerson(cancellationToken: ct);
            var person2 = await uow.CreatePerson(cancellationToken: ct);

            var roles = uow.GetPartyExternalRolePersistence();
            await roles.UpsertExternalRolesFromPartyBySource(
                commandId: Guid.CreateVersion7(),
                partyUuid: org.PartyUuid.Value,
                roleSource: ExternalRoleSource.CentralCoordinatingRegister,
                assignments: [
                    new("styreleder", person2.PartyUuid.Value),
                ],
                cancellationToken: ct);

            return (org, person1, person2);
        });

        FakeHttpHandlers.For<IA2PartyImportService>().Expect(HttpMethod.Get, "/register/api/parties/partyroles/{fromPartyId}")
            .WithRouteValue("fromPartyId", org.PartyId.Value.ToString())
            .Respond(
                "application/json",
                $$"""
                [
                    {"PartyId": "{{person1.PartyId.Value}}", "PartyUuid": "{{person1.PartyUuid.Value}}", "PartyRelation": "Role", "RoleCode": "DAGL"},
                    {"PartyId": "{{person2.PartyId.Value}}", "PartyUuid": "{{person2.PartyUuid.Value}}", "PartyRelation": "Role", "RoleCode": "MEDL"}
                ]
                """);

        var cmd = new ImportA2CCRRolesCommand
        {
            ChangeId = 1,
            ChangedTime = TimeProvider.GetUtcNow(),
            PartyId = org.PartyId.Value,
            PartyUuid = org.PartyUuid.Value,
        };

        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);
        var conversation = await TestHarness.Conversation(cmd, TestContext.Current.CancellationToken);

        var resolveCommand = await conversation.Commands.OfType<ResolveAndUpsertA2CCRRoleAssignmentsCommand>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(resolveCommand);

        Assert.Equal(resolveCommand.FromPartyUuid, cmd.PartyUuid);
        Assert.Equal(resolveCommand.Tracking, new(JobNames.A2PartyImportCCRRoleAssignments, 1));
        Assert.Collection(
            resolveCommand.RoleAssignments,
            a =>
            {
                Assert.Equal(person1.PartyUuid.Value, a.ToPartyUuid);
                Assert.Equal("DAGL", a.RoleCode);
            },
            a =>
            {
                Assert.Equal(person2.PartyUuid.Value, a.ToPartyUuid);
                Assert.Equal("MEDL", a.RoleCode);
            });
    }

    [Theory]
    [MemberData(nameof(PartyNameHandlingData))]
    public async Task ImportA2PartyCommand_NameHandling(PersonNameInput input)
    {
        var party = new V1Models.Party
        {
            PartyId = 50012345,
            PartyTypeName = V1Models.PartyType.Person,
            OrgNumber = null,
            SSN = "25871999336",
            UnitType = null,
            Name = "Ola Bla Nordmann",
            IsDeleted = false,
            PartyUuid = Guid.Parse("195BD1DC-F136-48FE-9BA4-959949A1B067"),
            OnlyHierarchyElementWithNoAccess = false,
            Person = new V1Models.Person
            {
                SSN = "25871999336",
                Name = "Ola Bla Nordmann",
                FirstName = "Ola",
                MiddleName = "Bla",
                LastName = "Nordmann",
                TelephoneNumber = "12345678",
                MobileNumber = "87654321",
                MailingAddress = "Blåbæreveien 7 8450 Stokmarknes",
                MailingPostalCode = "8450",
                MailingPostalCity = "Stokmarknes",
                AddressMunicipalNumber = "1866",
                AddressMunicipalName = "Hadsel",
                AddressStreetName = "Blåbærveien",
                AddressHouseNumber = "7",
                AddressHouseLetter = "G",
                AddressPostalCode = "8450",
                AddressCity = "Stokarknes"
            },
            Organization = null,
            ChildParties = null,
        };

        party.Name = input.Name;
        party.Person.Name = input.Name;
        party.Person.FirstName = input.FirstName;
        party.Person.MiddleName = input.MiddleName;
        party.Person.LastName = input.LastName;

        var partyUuid = party.PartyUuid!.Value;
        FakeHttpHandlers.For<IA2PartyImportService>().Expect(HttpMethod.Get, "/register/api/parties")
            .WithQuery("partyuuid", partyUuid.ToString())
            .Respond(() => JsonContent.Create(party));

        await CommandSender.Send(new ImportA2PartyCommand { PartyUuid = partyUuid, ChangeId = 1, ChangedTime = TimeProvider.GetUtcNow() }, TestContext.Current.CancellationToken);

        Assert.True(await TestHarness.Consumed.Any<ImportA2PartyCommand>(TestContext.Current.CancellationToken));
        var sent = await TestHarness.Sent.SelectAsync<UpsertPartyCommand>(TestContext.Current.CancellationToken).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(sent);

        var person = sent.Context.Message.Party.ShouldBeOfType<PersonRecord>();
        person.ShouldSatisfyAllConditions([
            (p) => p.PartyId.ShouldBe((uint)party.PartyId),
            (p) => p.PartyUuid.ShouldBe(partyUuid),
            (p) => p.DisplayName.ShouldBe(input.ExpectedName),
            (p) => p.FirstName.ShouldBe(input.ExpectedFirstName),
            (p) => p.MiddleName.ShouldBe(input.ExpectedMiddleName),
            (p) => p.LastName.ShouldBe(input.ExpectedLastName),
        ]);
    }

    public static TheoryData<PersonNameInput> PartyNameHandlingData =>
    [
        new PersonNameInput()
        {
            Name = null,
            FirstName = null,
            LastName = null,
            ExpectedName = "Mangler Navn",
            ExpectedFirstName = "Mangler",
            ExpectedLastName = "Navn",
        },

        new PersonNameInput()
        {
            Name = "Gul-Brun Sint Tiger",
            FirstName = "Sint Tiger",
            LastName = "Gul-Brun",
            ExpectedName = "Sint Tiger Gul-Brun",
        },

        new PersonNameInput()
        {
            Name = "Inserted By ER Import",
            FirstName = null,
            LastName = null,
            ExpectedFirstName = "Mangler",
            ExpectedLastName = "Navn",
            ExpectedName = "Mangler Navn",
        },

        new PersonNameInput()
        {
            Name = "Ikke i Altinn register",
            FirstName = null,
            LastName = null,
            ExpectedFirstName = "Mangler",
            ExpectedLastName = "Navn",
            ExpectedName = "Mangler Navn",
        },

        new PersonNameInput()
        {
            Name = "Inserted By FReg Import",
            FirstName = null,
            LastName = null,
            ExpectedFirstName = "Mangler",
            ExpectedLastName = "Navn",
            ExpectedName = "Mangler Navn",
        },

        new PersonNameInput()
        {
            Name = "Gul-Brun Sint Tiger",
            FirstName = null,
            LastName = null,
            ExpectedFirstName = "Sint Tiger",
            ExpectedLastName = "Gul-Brun",
            ExpectedName = "Sint Tiger Gul-Brun",
        },

        new PersonNameInput()
        {
            Name = null,
            FirstName = "Sint Tiger",
            LastName = "Gul-Brun",
            ExpectedName = "Sint Tiger Gul-Brun",
        },

        new PersonNameInput()
        {
            Name = null,
            FirstName = "Sint Tiger",
            MiddleName = "Jr.",
            LastName = "Gul-Brun",
            ExpectedName = "Sint Tiger Jr. Gul-Brun",
        },

        // Test that Name is built up from FirstName, MiddleName and LastName
        new PersonNameInput()
        {
            Name = "Gul-Brun S. Tiger",
            FirstName = "Sint Tiger",
            LastName = "Gul-Brun",
            ExpectedName = "Sint Tiger Gul-Brun",
        },

        new PersonNameInput()
        {
            Name = "Gul-Grønn Sint T. R.",
            FirstName = "Sint Tiger",
            MiddleName = "Rød",
            LastName = "von Gul-Grønn",
            ExpectedName = "Sint Tiger Rød von Gul-Grønn",
        }
    ];

    public sealed record PersonNameInput
        : IXunitSerializable
    {
        private string? _name;
        private string? _firstName;
        private string? _lastName;
        private string? _middleName;
        private string? _expectedName;
        private string? _expectedFirstName;
        private string? _expectedMiddleName;
        private string? _expectedLastName;

        public required string? Name
        {
            get => _name;
            init => _name = value;
        }

        public required string? FirstName
        {
            get => _firstName;
            init => _firstName = value;
        }

        public string? MiddleName
        {
            get => _middleName;
            init => _middleName = value;
        }

        public required string? LastName
        {
            get => _lastName;
            init => _lastName = value;
        }

        public string? ExpectedName
        {
            get => _expectedName ?? Name;
            init => _expectedName = value;
        }

        public string? ExpectedFirstName
        {
            get => _expectedFirstName ?? FirstName;
            init => _expectedFirstName = value;
        }

        public string? ExpectedMiddleName
        {
            get => _expectedMiddleName ?? MiddleName;
            init => _expectedMiddleName = value;
        }

        public string? ExpectedLastName
        {
            get => _expectedLastName ?? LastName;
            init => _expectedLastName = value;
        }

        public PersonNameInput()
        {
        }

        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            _name = info.GetValue<string>(nameof(Name));
            _firstName = info.GetValue<string>(nameof(FirstName));
            _middleName = info.GetValue<string>(nameof(MiddleName));
            _lastName = info.GetValue<string>(nameof(LastName));
            _expectedName = info.GetValue<string>(nameof(ExpectedName));
            _expectedFirstName = info.GetValue<string>(nameof(ExpectedFirstName));
            _expectedMiddleName = info.GetValue<string>(nameof(ExpectedMiddleName));
            _expectedLastName = info.GetValue<string>(nameof(ExpectedLastName));
        }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Name), Name, typeof(string));
            info.AddValue(nameof(FirstName), FirstName, typeof(string));
            info.AddValue(nameof(MiddleName), MiddleName, typeof(string));
            info.AddValue(nameof(LastName), LastName, typeof(string));
            info.AddValue(nameof(ExpectedName), ExpectedName, typeof(string));
            info.AddValue(nameof(ExpectedFirstName), ExpectedFirstName, typeof(string));
            info.AddValue(nameof(ExpectedMiddleName), ExpectedMiddleName, typeof(string));
            info.AddValue(nameof(ExpectedLastName), ExpectedLastName, typeof(string));
        }
    }
}
