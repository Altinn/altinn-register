#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Headers;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.PartyImport.A2;
using Altinn.Register.Tests.Utils;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.Http;
using FluentAssertions.Execution;
using Nerdbank.Streams;

namespace Altinn.Register.Tests.PartyImport.A2;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:Parameter should not span multiple lines", Justification = "It's more readable")]
public class A2PartyImportServiceTests
    : HostTestBase
{
    [Fact]
    public async Task GetParty_Calls_Correct_Endpoint_AndMapsOrganizationData()
    {
        var partyId = 50004216;
        var party = await TestDataLoader.Load<Altinn.Platform.Register.Models.Party>(partyId.ToString(CultureInfo.InvariantCulture));
        Assert.NotNull(party);

        var partyUuid = party.PartyUuid!.Value;

        using var handler = new FakeHttpMessageHandler();
        handler.Expect(HttpMethod.Get, "/parties")
            .WithQuery("partyuuid", partyUuid.ToString())
            .Respond(() => TestDataParty(partyId));

        var client = new A2PartyImportService(handler.CreateClient(), TimeProvider);

        var partyRecord = await client.GetParty(partyUuid);

        using (new AssertionScope())
        {
            partyRecord.Should().NotBeNull();
            partyRecord.PartyId.Should().Be(partyId);
            partyRecord.PartyUuid.Should().Be(partyUuid);

            var orgRecord = partyRecord.Should().BeOfType<OrganizationRecord>().Which;
            orgRecord.OrganizationIdentifier.Should().HaveValue().Which.Should().Be("311654306");
            orgRecord.Name.Should().HaveValue().Which.Should().Be("TYNSET OG OPPDAL");
            orgRecord.UnitType.Should().HaveValue().Which.Should().Be("ANS");
            orgRecord.UnitStatus.Should().HaveValue().Which.Should().Be("N");
            orgRecord.TelephoneNumber.Should().HaveValue().Which.Should().Be("22077000");
            orgRecord.MobileNumber.Should().HaveValue().Which.Should().Be("99000000");
            orgRecord.FaxNumber.Should().HaveValue().Which.Should().Be("22077108");
            orgRecord.EmailAddress.Should().HaveValue().Which.Should().Be("tynset_og_oppdal@example.com");
            orgRecord.InternetAddress.Should().HaveValue().Which.Should().Be("tynset-og-oppdal.example.com");
            orgRecord.MailingAddress.Should().HaveValue().Which.Should().Be(new MailingAddress
            {
                Address = "Postboks 6662 St. Bergens plass",
                PostalCode = "1666",
                City = "Bergen",
            });
            orgRecord.BusinessAddress.Should().HaveValue().Which.Should().Be(new MailingAddress
            {
                Address = "Postboks 6662 St. Olavs plass",
                PostalCode = "0555",
                City = "Oslo",
            });
        }
    }

    [Fact]
    public async Task GetParty_Calls_Correct_Endpoint_AndMapsPersonData()
    {
        var partyId = 50012345;
        var party = await TestDataLoader.Load<Altinn.Platform.Register.Models.Party>(partyId.ToString(CultureInfo.InvariantCulture));
        Assert.NotNull(party);

        var partyUuid = party.PartyUuid!.Value;

        using var handler = new FakeHttpMessageHandler();
        handler.Expect(HttpMethod.Get, "/parties")
            .WithQuery("partyuuid", partyUuid.ToString())
            .Respond(() => TestDataParty(partyId));

        var client = new A2PartyImportService(handler.CreateClient(), TimeProvider);

        var partyRecord = await client.GetParty(partyUuid);

        using (new AssertionScope())
        {
            partyRecord.Should().NotBeNull();
            partyRecord.PartyId.Should().Be(partyId);
            partyRecord.PartyUuid.Should().Be(partyUuid);

            var persRecord = partyRecord.Should().BeOfType<PersonRecord>().Which;
            persRecord.PersonIdentifier.Should().HaveValue().Which.Should().Be("25871999336");
            persRecord.Name.Should().HaveValue().Which.Should().Be("Ola Bla Nordmann");
            persRecord.FirstName.Should().HaveValue().Which.Should().Be("Ola");
            persRecord.MiddleName.Should().HaveValue().Which.Should().Be("Bla");
            persRecord.LastName.Should().HaveValue().Which.Should().Be("Nordmann");
            persRecord.MailingAddress.Should().HaveValue().Which.Should().Be(new MailingAddress
            {
                Address = "Blåbæreveien 7 8450 Stokmarknes",
                PostalCode = "8450",
                City = "Stokmarknes",
            });
            persRecord.Address.Should().HaveValue().Which.Should().Be(new StreetAddress
            {
                MunicipalNumber = "1866",
                MunicipalName = "Hadsel",
                StreetName = "Blåbærveien",
                HouseNumber = "7",
                HouseLetter = "G",
                PostalCode = "8450",
                City = "Stokarknes",
            });
        }
    }

    [Fact]
    public async Task GetChanges_NoChanges()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Expect(HttpMethod.Get, "/parties/partychanges/0")
            .Respond(
                contentType: "application/json",
                """
                {
                    "PartyChangeList": [],
                    "LastAvailableChange": 0,
                    "LastChangeInSegment": 0
                }
                """);

        var client = new A2PartyImportService(handler.CreateClient(), TimeProvider);
        var changes = await client.GetChanges().ToListAsync();

        var page = changes.Should().ContainSingle().Which;
        page.LastKnownChangeId.Should().Be(0);
        page.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChanges_SinglePage()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Expect(HttpMethod.Get, "/parties/partychanges/0")
            .Respond(
                contentType: "application/json",
                """
                {
                    "PartyChangeList": [
                        {
                            "ChangeId": 1,
                            "PartyId": 1,
                            "PartyUuid": "00000000-0000-0000-0000-000000000001",
                            "LastChangedTime": "2020-01-01T00:00:00Z"
                        },
                        {
                            "ChangeId": 2,
                            "PartyId": 2,
                            "PartyUuid": "00000000-0000-0000-0000-000000000002",
                            "LastChangedTime": "2020-01-02T00:00:00Z"
                        }
                    ],
                    "LastAvailableChange": 2,
                    "LastChangeInSegment": 2
                }
                """);

        var client = new A2PartyImportService(handler.CreateClient(), TimeProvider);
        var changes = await client.GetChanges().ToListAsync();

        var page = changes.Should().ContainSingle().Which;
        page.LastKnownChangeId.Should().Be(2);
        page.Should().HaveCount(2);

        page[0].ChangeId.Should().Be(1);
        page[0].PartyId.Should().Be(1);
        page[0].PartyUuid.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        page[0].ChangeTime.Should().Be(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

        page[1].ChangeId.Should().Be(2);
        page[1].PartyId.Should().Be(2);
        page[1].PartyUuid.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        page[1].ChangeTime.Should().Be(new DateTimeOffset(2020, 1, 2, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task GetChanges_MultiplePages()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Expect(HttpMethod.Get, "/parties/partychanges/0")
            .Respond(
                contentType: "application/json",
                """
                {
                    "PartyChangeList": [
                        {
                            "ChangeId": 1,
                            "PartyId": 1,
                            "PartyUuid": "00000000-0000-0000-0000-000000000001",
                            "LastChangedTime": "2020-01-01T00:00:00Z"
                        },
                        {
                            "ChangeId": 2,
                            "PartyId": 2,
                            "PartyUuid": "00000000-0000-0000-0000-000000000002",
                            "LastChangedTime": "2020-01-02T00:00:00Z"
                        }
                    ],
                    "LastAvailableChange": 3,
                    "LastChangeInSegment": 2
                }
                """);
        
        handler.Expect(HttpMethod.Get, "/parties/partychanges/2")
            .Respond(
                contentType: "application/json",
                """
                {
                    "PartyChangeList": [
                        {
                            "ChangeId": 3,
                            "PartyId": 3,
                            "PartyUuid": "00000000-0000-0000-0000-000000000003",
                            "LastChangedTime": "2020-01-03T00:00:00Z"
                        },
                        {
                            "ChangeId": 4,
                            "PartyId": 4,
                            "PartyUuid": "00000000-0000-0000-0000-000000000004",
                            "LastChangedTime": "2020-01-04T00:00:00Z"
                        }
                    ],
                    "LastAvailableChange": 5,
                    "LastChangeInSegment": 4
                }
                """);

        handler.Expect(HttpMethod.Get, "/parties/partychanges/4")
            .Respond(
                contentType: "application/json",
                """
                {
                    "PartyChangeList": [
                        {
                            "ChangeId": 5,
                            "PartyId": 5,
                            "PartyUuid": "00000000-0000-0000-0000-000000000005",
                            "LastChangedTime": "2020-01-05T00:00:00Z"
                        }
                    ],
                    "LastAvailableChange": 5,
                    "LastChangeInSegment": 5
                }
                """);

        var client = new A2PartyImportService(handler.CreateClient(), TimeProvider);
        var changes = await client.GetChanges().ToListAsync();

        changes.Should().HaveCount(3);
        var firstPage = changes[0];
        firstPage.LastKnownChangeId.Should().Be(3);
        firstPage.Should().HaveCount(2);

        firstPage[0].ChangeId.Should().Be(1);
        firstPage[0].PartyId.Should().Be(1);
        firstPage[0].PartyUuid.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        firstPage[0].ChangeTime.Should().Be(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

        firstPage[1].ChangeId.Should().Be(2);
        firstPage[1].PartyId.Should().Be(2);
        firstPage[1].PartyUuid.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        firstPage[1].ChangeTime.Should().Be(new DateTimeOffset(2020, 1, 2, 0, 0, 0, TimeSpan.Zero));

        var secondPage = changes[1];
        secondPage.LastKnownChangeId.Should().Be(5);
        secondPage.Should().HaveCount(2);

        secondPage[0].ChangeId.Should().Be(3);
        secondPage[0].PartyId.Should().Be(3);
        secondPage[0].PartyUuid.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000003"));
        secondPage[0].ChangeTime.Should().Be(new DateTimeOffset(2020, 1, 3, 0, 0, 0, TimeSpan.Zero));

        secondPage[1].ChangeId.Should().Be(4);
        secondPage[1].PartyId.Should().Be(4);
        secondPage[1].PartyUuid.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000004"));
        secondPage[1].ChangeTime.Should().Be(new DateTimeOffset(2020, 1, 4, 0, 0, 0, TimeSpan.Zero));

        var thirdPage = changes[2];
        thirdPage.LastKnownChangeId.Should().Be(5);
        thirdPage.Should().HaveCount(1);

        thirdPage[0].ChangeId.Should().Be(5);
        thirdPage[0].PartyId.Should().Be(5);
        thirdPage[0].PartyUuid.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000005"));
        thirdPage[0].ChangeTime.Should().Be(new DateTimeOffset(2020, 1, 5, 0, 0, 0, TimeSpan.Zero));
    }

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
}
