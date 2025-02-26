using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Models;

namespace Altinn.Register.IntegrationTests.Party;

public class StreamTests
    : IntegrationTestBase
{
    [Fact]
    public async Task EmptyStream()
    {
        var response = await HttpClient.GetAsync("/register/api/v2/internal/parties/stream", TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        var content = await response.ShouldHaveJsonContent<ItemStream<PartyRecord>>();

        content.Items.ShouldBeEmpty();
        content.Links.Next.ShouldBeNull();
        content.Stats.PageStart.ShouldBe(0UL);
        content.Stats.PageEnd.ShouldBe(0UL);
        content.Stats.SequenceMax.ShouldBe(0UL);
    }
}
