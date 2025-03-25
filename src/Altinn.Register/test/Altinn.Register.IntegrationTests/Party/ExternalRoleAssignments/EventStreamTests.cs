﻿using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Controllers.V2;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Models;
using Altinn.Register.TestUtils.TestData;

namespace Altinn.Register.IntegrationTests.Party.ExternalRoleAssignments;

public class EventStreamTests
    : IntegrationTestBase
{
    [Fact]
    public async Task EmptyStream()
    {
        var response = await HttpClient.GetAsync("/register/api/v2/internal/parties/external-roles/assignments/events/stream", TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        var content = await response.ShouldHaveJsonContent<ItemStream<ExternalRoleAssignmentEvent>>();

        content.Items.ShouldBeEmpty();
        content.Links.Next.ShouldBeNull();
        content.Stats.PageStart.ShouldBe(0UL);
        content.Stats.PageEnd.ShouldBe(0UL);
        content.Stats.SequenceMax.ShouldBe(0UL);
    }

    [Fact]
    public async Task SinglePage()
    {
        var evts = await Setup(async (uow, ct) =>
        {
            var orgs = await uow.CreateOrgs(2, ct);
            var roles = await uow.CreateFakeRoleDefinitions(ExternalRoleSource.CentralCoordinatingRegister, ct);

            var persistence = uow.GetPartyExternalRolePersistence();
            return await persistence.UpsertExternalRolesFromPartyBySource(
                commandId: Guid.CreateVersion7(),
                partyUuid: orgs[0].PartyUuid.Value,
                roleSource: ExternalRoleSource.CentralCoordinatingRegister,
                assignments: [
                    new()
                    {
                        RoleIdentifier = roles[0].Identifier,
                        ToParty = orgs[1].PartyUuid.Value,
                    },
                    new()
                    {
                        RoleIdentifier = roles[1].Identifier,
                        ToParty = orgs[1].PartyUuid.Value,
                    },
                ],
                ct)
                .ToListAsync(ct);
        });

        var response = await HttpClient.GetAsync("/register/api/v2/internal/parties/external-roles/assignments/events/stream", TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        var content = await response.ShouldHaveJsonContent<ItemStream<ExternalRoleAssignmentEvent>>();

        var items = content.Items.ToList();
        items.Count.ShouldBe(2);

        items[0].ShouldBe(evts[0]);
        items[1].ShouldBe(evts[1]);

        content.Links.Next.ShouldNotBeNull();
        content.Links.Next.ShouldStartWith(BaseUrl + "register/api/v2/internal/parties/external-roles/assignments/events/stream?");

        response = await HttpClient.GetAsync(content.Links.Next, TestContext.Current.CancellationToken);

        await response.ShouldHaveSuccessStatusCode();
        content = await response.ShouldHaveJsonContent<ItemStream<ExternalRoleAssignmentEvent>>();

        content.Items.ShouldBeEmpty();
        content.Links.Next.ShouldBeNull();
    }

    [Fact]
    public async Task MultiplePages()
    {
        const int MIN_EVENTS = (PartyController.ROLEASSIGNMENTS_STREAM_PAGE_SIZE * 3) + (PartyController.ROLEASSIGNMENTS_STREAM_PAGE_SIZE / 2);

        var evts = await Setup(async (uow, ct) =>
        {
            var orgs = await uow.CreateOrgs(2, ct);
            var allRoles = await uow.CreateFakeRoleDefinitions(ct);

            var evts = new List<ExternalRoleAssignmentEvent>();

            while (evts.Count < MIN_EVENTS)
            {
                foreach (var (source, roles) in allRoles)
                {
                    var assignments = RandomSubset(roles).Select(roles => new IPartyExternalRolePersistence.UpsertExternalRoleAssignment()
                    {
                        RoleIdentifier = roles.Identifier,
                        ToParty = orgs[1].PartyUuid.Value,
                    });

                    evts.AddRange(
                        await uow.GetPartyExternalRolePersistence().UpsertExternalRolesFromPartyBySource(
                            commandId: Guid.CreateVersion7(),
                            partyUuid: orgs[0].PartyUuid.Value,
                            roleSource: source,
                            assignments: assignments,
                            ct)
                            .ToListAsync(ct));
                }
            }

            return evts;
        });

        evts.Count.ShouldBeGreaterThanOrEqualTo(MIN_EVENTS);

        var seen = 0;
        var next = "/register/api/v2/internal/parties/external-roles/assignments/events/stream";

        /*********************************************
         * PAGES                                     *
         *********************************************/
        while (seen < evts.Count)
        {
            var response = await HttpClient.GetAsync(next, TestContext.Current.CancellationToken);

            await response.ShouldHaveSuccessStatusCode();
            var content = await response.ShouldHaveJsonContent<ItemStream<ExternalRoleAssignmentEvent>>();

            var items = content.Items.ToList();
            items.Count.ShouldBe(Math.Min(PartyController.ROLEASSIGNMENTS_STREAM_PAGE_SIZE, evts.Count - seen));

            for (var i = 0; i < items.Count; i++)
            {
                items[i].ShouldBe(evts[seen + i]);
            }

            seen += items.Count;
            next = content.Links.Next.ShouldNotBeNull();
            content.Links.Next.ShouldStartWith(BaseUrl + "register/api/v2/internal/parties/external-roles/assignments/events/stream?");
        }

        /*********************************************
         * LAST PAGE                                 *
         *********************************************/
        var lastResponse = await HttpClient.GetAsync(next, TestContext.Current.CancellationToken);

        await lastResponse.ShouldHaveSuccessStatusCode();
        var lastContent = await lastResponse.ShouldHaveJsonContent<ItemStream<ExternalRoleAssignmentEvent>>();

        lastContent.Items.ShouldBeEmpty();
        lastContent.Links.Next.ShouldBeNull();
    }

    private static IEnumerable<T> RandomSubset<T>(IEnumerable<T> values)
    {
        var random = Random.Shared;

        foreach (var item in values)
        {
            if (random.NextSingle() < 0.2)
            {
                yield return item;
            }
        }
    }
}
