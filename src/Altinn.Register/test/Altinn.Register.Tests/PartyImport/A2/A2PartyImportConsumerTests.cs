#nullable enable

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.PartyImport;
using Altinn.Register.PartyImport.A2;
using Altinn.Register.Tests.Utils;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.Http;
using Altinn.Register.TestUtils.MassTransit;
using Altinn.Register.TestUtils.TestData;
using FluentAssertions.Execution;
using Nerdbank.Streams;
using Xunit.Abstractions;

using V1Models = Altinn.Platform.Register.Models;

namespace Altinn.Register.Tests.PartyImport.A2;

public class A2PartyImportConsumerTests(ITestOutputHelper output)
    : BusTestBase(output)
{
    [Fact]
    public async Task ImportA2PartyCommand_FetchesParty_AndSendsUpsertCommand()
    {
        var partyId = 50004216;
        var partyUuid = Guid.Parse("7aa53da8-836c-4812-afcb-76d39f5ebb0e");
        HttpHandlers.For<IA2PartyImportService>().Expect(HttpMethod.Get, "/parties")
            .WithQuery("partyuuid", partyUuid.ToString())
            .Respond(() => TestDataParty(partyId));

        await CommandSender.Send(new ImportA2PartyCommand { PartyUuid = partyUuid, ChangeId = 1, ChangedTime = TimeProvider.GetUtcNow() });

        Assert.True(await Harness.Consumed.Any<ImportA2PartyCommand>());
        var sent = await Harness.Sent.SelectAsync<UpsertPartyCommand>().FirstOrDefaultAsync();
        Assert.NotNull(sent);

        sent.Context.Message.Party.PartyId.Should().Be(partyId);
        sent.Context.Message.Party.PartyUuid.Should().Be(partyUuid);
        sent.Context.DestinationAddress.Should().Be(CommandQueueResolver.GetQueueUriForCommandType<UpsertPartyCommand>());
    }

    [Fact]
    public async Task ImportA2PartyCommand_FetchesParty_AndSendsUpsertCommand_SIUser()
    {
        var partyId = 50006237;
        var partyUuid = Guid.Parse("4fe860c4-bc65-4d2f-a288-f825b460f26b");
        HttpHandlers.For<IA2PartyImportService>().Expect(HttpMethod.Get, "/parties")
            .WithQuery("partyuuid", partyUuid.ToString())
            .Respond(() => TestDataParty(partyId));

        await CommandSender.Send(new ImportA2PartyCommand { PartyUuid = partyUuid, ChangeId = 1, ChangedTime = TimeProvider.GetUtcNow() });

        Assert.True(await Harness.Consumed.Any<ImportA2PartyCommand>());
        var sent = await Harness.Sent.SelectAsync<UpsertPartyCommand>().FirstOrDefaultAsync();
        Assert.NotNull(sent);

        sent.Context.Message.Party.PartyId.Should().Be(partyId);
        sent.Context.Message.Party.PartyUuid.Should().Be(partyUuid);
        sent.Context.Message.Party.PartyType.Should().Be(PartyType.SelfIdentifiedUser);
        sent.Context.DestinationAddress.Should().Be(CommandQueueResolver.GetQueueUriForCommandType<UpsertPartyCommand>());
    }

    [Fact]
    public async Task ImportA2CCRRolesCommand_FetchesRoles_AndSendsUpsertCommand()
    {
        await GetRequiredService<IImportJobTracker>().TrackQueueStatus(JobNames.A2PartyImportCCRRoleAssignments, new()
        {
            SourceMax = 10,
            EnqueuedMax = 1,
        });
        var (org, person1, person2) = await Setup(async uow =>
        {
            var org = await uow.CreateOrg();
            var person1 = await uow.CreatePerson();
            var person2 = await uow.CreatePerson();

            var roles = uow.GetPartyExternalRolePersistence();
            await roles.UpsertExternalRolesFromPartyBySource(
                commandId: Guid.CreateVersion7(),
                partyUuid: org.PartyUuid.Value,
                roleSource: ExternalRoleSource.CentralCoordinatingRegister,
                assignments: [
                    new("styreleder", person2.PartyUuid.Value),
                ]);

            return (org, person1, person2);
        });

        HttpHandlers.For<IA2PartyImportService>().Expect(HttpMethod.Get, "/parties/partyroles/{fromPartyId}")
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

        await CommandSender.Send(cmd);
        var conversation = await Harness.Conversation(cmd);

        var resolveCommand = await conversation.Commands.OfType<ResolveAndUpsertA2CCRRoleAssignmentsCommand>().FirstOrDefaultAsync();
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
        var party = await TestDataLoader.Load<V1Models.Party>("50012345");
        Assert.NotNull(party);
        Assert.NotNull(party.Person);
        
        party.Name = input.Name;
        party.Person.Name = input.Name;
        party.Person.FirstName = input.FirstName;
        party.Person.MiddleName = input.MiddleName;
        party.Person.LastName = input.LastName;

        var partyUuid = party.PartyUuid!.Value;
        HttpHandlers.For<IA2PartyImportService>().Expect(HttpMethod.Get, "/parties")
            .WithQuery("partyuuid", partyUuid.ToString())
            .Respond(() => JsonContent.Create(party));

        await CommandSender.Send(new ImportA2PartyCommand { PartyUuid = partyUuid, ChangeId = 1, ChangedTime = TimeProvider.GetUtcNow() });

        Assert.True(await Harness.Consumed.Any<ImportA2PartyCommand>());
        var sent = await Harness.Sent.SelectAsync<UpsertPartyCommand>().FirstOrDefaultAsync();
        Assert.NotNull(sent);

        var person = sent.Context.Message.Party.Should().BeOfType<PersonRecord>().Which;
        using (new AssertionScope())
        {
            person.PartyId.Should().Be(party.PartyId);
            person.PartyUuid.Should().Be(partyUuid);
            person.DisplayName.Should().Be(input.ExpectedName);
            person.FirstName.Should().Be(input.ExpectedFirstName);
            person.MiddleName.Should().Be(input.ExpectedMiddleName);
            person.LastName.Should().Be(input.ExpectedLastName);
        }
    }

    public static TheoryData<PersonNameInput> PartyNameHandlingData => new()
    {
        new()
        {
            Name = null,
            FirstName = null,
            LastName = null,
            ExpectedName = "Mangler Navn",
            ExpectedFirstName = "Mangler",
            ExpectedLastName = "Navn",
        },

        new()
        {
            Name = "Gul-Brun Sint Tiger",
            FirstName = "Sint Tiger",
            LastName = "Gul-Brun",
            ExpectedName = "Sint Tiger Gul-Brun",
        },

        new()
        {
            Name = "Inserted By ER Import",
            FirstName = null,
            LastName = null,
            ExpectedFirstName = "Mangler",
            ExpectedLastName = "Navn",
            ExpectedName = "Mangler Navn",
        },

        new()
        {
            Name = "Ikke i Altinn register",
            FirstName = null,
            LastName = null,
            ExpectedFirstName = "Mangler",
            ExpectedLastName = "Navn",
            ExpectedName = "Mangler Navn",
        },

        new()
        {
            Name = "Inserted By FReg Import",
            FirstName = null,
            LastName = null,
            ExpectedFirstName = "Mangler",
            ExpectedLastName = "Navn",
            ExpectedName = "Mangler Navn",
        },

        new()
        {
            Name = "Gul-Brun Sint Tiger",
            FirstName = null,
            LastName = null,
            ExpectedFirstName = "Sint Tiger",
            ExpectedLastName = "Gul-Brun",
            ExpectedName = "Sint Tiger Gul-Brun",
        },

        new()
        {
            Name = null,
            FirstName = "Sint Tiger",
            LastName = "Gul-Brun",
            ExpectedName = "Sint Tiger Gul-Brun",
        },

        new()
        {
            Name = null,
            FirstName = "Sint Tiger",
            MiddleName = "Jr.",
            LastName = "Gul-Brun",
            ExpectedName = "Sint Tiger Jr. Gul-Brun",
        },

        // Test that Name is built up from FirstName, MiddleName and LastName
        new()
        {
            Name = "Gul-Brun S. Tiger",
            FirstName = "Sint Tiger",
            LastName = "Gul-Brun",
            ExpectedName = "Sint Tiger Gul-Brun",
        },

        new()
        {
            Name = "Gul-Grønn Sint T. R.",
            FirstName = "Sint Tiger",
            MiddleName = "Rød",
            LastName = "von Gul-Grønn",
            ExpectedName = "Sint Tiger Rød von Gul-Grønn",
        }
    };

    private static async Task<SequenceHttpContent> TestDataParty(int id)
    {
        Sequence<byte>? content = null;

        try
        {
            content = await TestDataLoader.LoadContent(id.ToString(CultureInfo.InvariantCulture));

            var httpContent = new SequenceHttpContent(content);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            content = null;
            return httpContent;
        }
        finally
        {
            content?.Dispose();
        }
    }

    private async Task<T> Setup<T>(Func<IUnitOfWork, Task<T>> setup)
    {
        await using var uow = await GetRequiredService<IUnitOfWorkManager>().CreateAsync(activityName: $"{nameof(PartyImportBatchConsumerTests)}.{nameof(Setup)}");
        var result = await setup(uow);
        await uow.CommitAsync();

        return result;
    }

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
