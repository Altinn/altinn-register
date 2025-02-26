using Altinn.Register.Controllers.V2;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Models;
using Altinn.Register.TestUtils.TestData;

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

    [Fact]
    public async Task SinglePage()
    {
        var orgs = await Setup(async (uow, ct) =>
        {
            return await uow.CreateOrgs(2, ct);
        });

        var response = await HttpClient.GetAsync("/register/api/v2/internal/parties/stream", TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        var content = await response.ShouldHaveJsonContent<ItemStream<PartyRecord>>();

        var items = content.Items.ToList();
        items.Count.ShouldBe(2);

        items[0].PartyUuid.ShouldBe(orgs[0].PartyUuid);
        items[1].PartyUuid.ShouldBe(orgs[1].PartyUuid);

        content.Links.Next.ShouldNotBeNull();
        content.Links.Next.ShouldStartWith(BaseUrl + "register/api/v2/internal/parties/stream?");

        response = await HttpClient.GetAsync(content.Links.Next, TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        content = await response.ShouldHaveJsonContent<ItemStream<PartyRecord>>();

        content.Items.ShouldBeEmpty();
        content.Links.Next.ShouldBeNull();
    }

    [Fact]
    public async Task MultiplePages()
    {
        var orgs = await Setup(async (uow, ct) =>
        {
            return await uow.CreateOrgs((PartyController.PARTY_STREAM_PAGE_SIZE * 3) + (PartyController.PARTY_STREAM_PAGE_SIZE / 2), ct);
        });

        /*********************************************
         * PAGE 1                                    *
         *********************************************/
        var response = await HttpClient.GetAsync("/register/api/v2/internal/parties/stream", TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        var content = await response.ShouldHaveJsonContent<ItemStream<PartyRecord>>();

        var items = content.Items.ToList();
        items.Count.ShouldBe(PartyController.PARTY_STREAM_PAGE_SIZE);

        for (var i = 0; i < items.Count; i++)
        {
            items[i].PartyUuid.ShouldBe(orgs[i].PartyUuid);
        }

        content.Links.Next.ShouldNotBeNull();
        content.Links.Next.ShouldStartWith(BaseUrl + "register/api/v2/internal/parties/stream?");

        /*********************************************
         * PAGE 2                                    *
         *********************************************/
        response = await HttpClient.GetAsync(content.Links.Next, TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        content = await response.ShouldHaveJsonContent<ItemStream<PartyRecord>>();

        items = content.Items.ToList();
        items.Count.ShouldBe(PartyController.PARTY_STREAM_PAGE_SIZE);

        for (var i = 0; i < items.Count; i++)
        {
            items[i].PartyUuid.ShouldBe(orgs[PartyController.PARTY_STREAM_PAGE_SIZE + i].PartyUuid);
        }

        content.Links.Next.ShouldNotBeNull();
        content.Links.Next.ShouldStartWith(BaseUrl + "register/api/v2/internal/parties/stream?");

        /*********************************************
         * PAGE 3                                    *
         *********************************************/
        response = await HttpClient.GetAsync(content.Links.Next, TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        content = await response.ShouldHaveJsonContent<ItemStream<PartyRecord>>();

        items = content.Items.ToList();
        items.Count.ShouldBe(PartyController.PARTY_STREAM_PAGE_SIZE);

        for (var i = 0; i < items.Count; i++)
        {
            items[i].PartyUuid.ShouldBe(orgs[(PartyController.PARTY_STREAM_PAGE_SIZE * 2) + i].PartyUuid);
        }

        content.Links.Next.ShouldNotBeNull();
        content.Links.Next.ShouldStartWith(BaseUrl + "register/api/v2/internal/parties/stream?");

        /*********************************************
         * PAGE 4                                    *
         *********************************************/
        response = await HttpClient.GetAsync(content.Links.Next, TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        content = await response.ShouldHaveJsonContent<ItemStream<PartyRecord>>();

        items = content.Items.ToList();
        items.Count.ShouldBe(PartyController.PARTY_STREAM_PAGE_SIZE / 2);

        for (var i = 0; i < items.Count; i++)
        {
            items[i].PartyUuid.ShouldBe(orgs[(PartyController.PARTY_STREAM_PAGE_SIZE * 3) + i].PartyUuid);
        }

        content.Links.Next.ShouldNotBeNull();
        content.Links.Next.ShouldStartWith(BaseUrl + "register/api/v2/internal/parties/stream?");

        /*********************************************
         * PAGE 5                                    *
         *********************************************/
        response = await HttpClient.GetAsync(content.Links.Next, TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        content = await response.ShouldHaveJsonContent<ItemStream<PartyRecord>>();

        content.Items.ShouldBeEmpty();
        content.Links.Next.ShouldBeNull();
    }
}
