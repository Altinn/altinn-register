#nullable enable

using System.Globalization;
using System.Net.Http.Headers;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.PartyImport;
using Altinn.Register.PartyImport.A2;
using Altinn.Register.Tests.Utils;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.Http;
using Nerdbank.Streams;

namespace Altinn.Register.Tests.PartyImport;

public class A2PartyImportConsumerTests
    : BusTestBase
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
        Assert.True(await Harness.Consumed.Any<ImportA2PartyCommand>());
        var sent = await Harness.Sent.SelectAsync<UpsertPartyCommand>().FirstOrDefaultAsync();
        Assert.NotNull(sent);

        sent.Context.Message.Party.PartyId.Should().Be(partyId);
        sent.Context.Message.Party.PartyUuid.Should().Be(partyUuid);
        sent.Context.DestinationAddress.Should().Be(CommandQueueResolver.GetQueueUriForCommandType<UpsertPartyCommand>());
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
