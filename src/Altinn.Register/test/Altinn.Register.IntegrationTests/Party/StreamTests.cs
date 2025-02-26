using Altinn.Register.Controllers.V2;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Models;
using Altinn.Register.TestUtils.TestData;
using Xunit.Sdk;

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

    [Fact]
    public async Task DefaultFields()
    {
        var (dbOrg, dbPers) = await Setup(async (uow, ct) =>
        {
            var org = await uow.CreateOrg(cancellationToken: ct);
            var pers = await uow.CreatePerson(cancellationToken: ct);

            return (org, pers);
        });

        var response = await HttpClient.GetAsync("/register/api/v2/internal/parties/stream", TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        var content = await response.ShouldHaveJsonContent<ItemStream<PartyRecord>>();

        var items = content.Items.ToList();
        items.Count.ShouldBe(2);

        var apiOrg = items[0];
        var apiPers = items[1];

        apiOrg.ShouldBeOfType<OrganizationRecord>().ShouldSatisfyAllConditions(
            o => o.PartyUuid.ShouldBe(dbOrg.PartyUuid),
            o => o.PartyId.ShouldBe(dbOrg.PartyId),
            o => o.DisplayName.ShouldBe(dbOrg.DisplayName),
            o => o.PersonIdentifier.ShouldBeNull(),
            o => o.OrganizationIdentifier.ShouldBe(dbOrg.OrganizationIdentifier),
            o => o.CreatedAt.ShouldBeUnset(),
            o => o.ModifiedAt.ShouldBeUnset(),
            o => o.IsDeleted.ShouldBeUnset(),
            o => o.VersionId.ShouldBe(dbOrg.VersionId),
            o => o.UnitStatus.ShouldBeUnset(),
            o => o.UnitType.ShouldBeUnset(),
            o => o.TelephoneNumber.ShouldBeUnset(),
            o => o.MobileNumber.ShouldBeUnset(),
            o => o.FaxNumber.ShouldBeUnset(),
            o => o.EmailAddress.ShouldBeUnset(),
            o => o.InternetAddress.ShouldBeUnset(),
            o => o.MailingAddress.ShouldBeUnset(),
            o => o.BusinessAddress.ShouldBeUnset());

        apiPers.ShouldBeOfType<PersonRecord>().ShouldSatisfyAllConditions(
            p => p.PartyUuid.ShouldBe(dbPers.PartyUuid),
            p => p.PartyId.ShouldBe(dbPers.PartyId),
            p => p.DisplayName.ShouldBe(dbPers.DisplayName),
            p => p.PersonIdentifier.ShouldBe(dbPers.PersonIdentifier),
            p => p.OrganizationIdentifier.ShouldBeNull(),
            p => p.CreatedAt.ShouldBeUnset(),
            p => p.ModifiedAt.ShouldBeUnset(),
            p => p.IsDeleted.ShouldBeUnset(),
            p => p.VersionId.ShouldBe(dbPers.VersionId),
            p => p.FirstName.ShouldBeUnset(),
            p => p.MiddleName.ShouldBeUnset(),
            p => p.LastName.ShouldBeUnset(),
            p => p.ShortName.ShouldBeUnset(),
            p => p.Address.ShouldBeUnset(),
            p => p.MailingAddress.ShouldBeUnset(),
            p => p.DateOfBirth.ShouldBeUnset(),
            p => p.DateOfDeath.ShouldBeUnset());
    }

    [Fact]
    public async Task AllFields()
    {
        var (dbOrg, dbPers) = await Setup(async (uow, ct) =>
        {
            var org = await uow.CreateOrg(cancellationToken: ct);
            var pers = await uow.CreatePerson(cancellationToken: ct);

            return (org, pers);
        });

        var response = await HttpClient.GetAsync("/register/api/v2/internal/parties/stream?fields=person,party,org", TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        var content = await response.ShouldHaveJsonContent<ItemStream<PartyRecord>>();

        var nextLink = content.Links.Next.ShouldNotBeNull();
        nextLink.ShouldContain("fields=party,person,org");

        var items = content.Items.ToList();
        items.Count.ShouldBe(2);

        var apiOrg = items[0];
        var apiPers = items[1];

        apiOrg.ShouldBeOfType<OrganizationRecord>().ShouldSatisfyAllConditions(
            o => o.PartyUuid.ShouldBe(dbOrg.PartyUuid),
            o => o.PartyId.ShouldBe(dbOrg.PartyId),
            o => o.DisplayName.ShouldBe(dbOrg.DisplayName),
            o => o.PersonIdentifier.ShouldBeNull(),
            o => o.OrganizationIdentifier.ShouldBe(dbOrg.OrganizationIdentifier),
            o => o.CreatedAt.ShouldBe(dbOrg.CreatedAt),
            o => o.ModifiedAt.ShouldBe(dbOrg.ModifiedAt),
            o => o.IsDeleted.ShouldBe(dbOrg.IsDeleted),
            o => o.VersionId.ShouldBe(dbOrg.VersionId),
            o => o.UnitStatus.ShouldBe(dbOrg.UnitStatus),
            o => o.UnitType.ShouldBe(dbOrg.UnitType),
            o => o.TelephoneNumber.ShouldBe(dbOrg.TelephoneNumber),
            o => o.MobileNumber.ShouldBe(dbOrg.MobileNumber),
            o => o.FaxNumber.ShouldBe(dbOrg.FaxNumber),
            o => o.EmailAddress.ShouldBe(dbOrg.EmailAddress),
            o => o.InternetAddress.ShouldBe(dbOrg.InternetAddress),
            o => o.MailingAddress.ShouldBe(dbOrg.MailingAddress),
            o => o.BusinessAddress.ShouldBe(dbOrg.BusinessAddress));

        apiPers.ShouldBeOfType<PersonRecord>().ShouldSatisfyAllConditions(
            p => p.PartyUuid.ShouldBe(dbPers.PartyUuid),
            p => p.PartyId.ShouldBe(dbPers.PartyId),
            p => p.DisplayName.ShouldBe(dbPers.DisplayName),
            p => p.PersonIdentifier.ShouldBe(dbPers.PersonIdentifier),
            p => p.OrganizationIdentifier.ShouldBeNull(),
            p => p.CreatedAt.ShouldBe(dbPers.CreatedAt),
            p => p.ModifiedAt.ShouldBe(dbPers.ModifiedAt),
            p => p.IsDeleted.ShouldBe(dbPers.IsDeleted),
            p => p.VersionId.ShouldBe(dbPers.VersionId),
            p => p.FirstName.ShouldBe(dbPers.FirstName),
            p => p.MiddleName.ShouldBe(dbPers.MiddleName),
            p => p.LastName.ShouldBe(dbPers.LastName),
            p => p.ShortName.ShouldBe(dbPers.ShortName),
            p => p.Address.ShouldBe(dbPers.Address),
            p => p.MailingAddress.ShouldBe(dbPers.MailingAddress),
            p => p.DateOfBirth.ShouldBe(dbPers.DateOfBirth),
            p => p.DateOfDeath.ShouldBe(dbPers.DateOfDeath));
    }
}
