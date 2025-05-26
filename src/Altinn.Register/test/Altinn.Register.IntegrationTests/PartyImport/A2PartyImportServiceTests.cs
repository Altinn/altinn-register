using System.Net;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.TestUtils.Http;

namespace Altinn.Register.IntegrationTests.PartyImport;

public class A2PartyImportServiceTests
    : IntegrationTestBase
{
    [Fact]
    public async Task Does_Not_Retry()
    {
        var partyUuid = Guid.CreateVersion7();

        FakeHttpHandlers.For<IA2PartyImportService>()
            .Expect(HttpMethod.Get, "/register/api/parties")
            .WithQuery("partyuuid", partyUuid.ToString())
            .Respond(() => HttpStatusCode.InternalServerError);

        var service = GetRequiredService<IA2PartyImportService>();

        var result = await service.GetParty(partyUuid, TestContext.Current.CancellationToken);
        result.IsProblem.ShouldBeTrue();
        result.Problem.ErrorCode.ShouldBe(Problems.PartyFetchFailed.ErrorCode);
    }

    public class GetUserPartyTests
        : IntegrationTestBase
    {
        protected virtual IFakeRequestBuilder GetRequestBuilder(FakeHttpMessageHandler handler, Guid partyUuid)
            => handler.Expect(HttpMethod.Get, "/profile/api/users")
                .WithQuery("userUUID", partyUuid.ToString());

        protected virtual Task<Result<PartyUserRecord>> GetUserParty(IA2PartyImportService service, Guid partyUuid)
            => service.GetPartyUser(partyUuid, TestContext.Current.CancellationToken);

        [Fact]
        public async Task Gone_Returns_PartyGoneProblem()
        {
            var partyUuid = Guid.CreateVersion7();

            GetRequestBuilder(FakeHttpHandlers.For<IA2PartyImportService>(), partyUuid)
                .Respond(() => HttpStatusCode.Gone);

            var service = GetRequiredService<IA2PartyImportService>();

            var result = await GetUserParty(service, partyUuid);
            
            result.IsProblem.ShouldBeTrue();
            result.Problem.ErrorCode.ShouldBe(Problems.PartyGone.ErrorCode);
        }

        [Fact]
        public async Task NotFound_Returns_PartyNotFound()
        {
            var partyUuid = Guid.CreateVersion7();

            GetRequestBuilder(FakeHttpHandlers.For<IA2PartyImportService>(), partyUuid)
                .Respond(() => HttpStatusCode.NotFound);

            var service = GetRequiredService<IA2PartyImportService>();

            var result = await GetUserParty(service, partyUuid);

            result.IsProblem.ShouldBeTrue();
            result.Problem.ErrorCode.ShouldBe(Problems.PartyNotFound.ErrorCode);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task UnsuccessfulStatus_Returns_PartyFetchFailed(HttpStatusCode statusCode)
        {
            var partyUuid = Guid.CreateVersion7();

            GetRequestBuilder(FakeHttpHandlers.For<IA2PartyImportService>(), partyUuid)
                .Respond(() => statusCode);

            var service = GetRequiredService<IA2PartyImportService>();

            var result = await GetUserParty(service, partyUuid);

            result.IsProblem.ShouldBeTrue();
            result.Problem.ErrorCode.ShouldBe(Problems.PartyFetchFailed.ErrorCode);
        }

        [Fact]
        public async Task ValidResponse_ReturnsPartyUserRecord()
        {
            var partyUuid = Guid.Parse("b049a483-dfea-437e-bb89-261a5390f207");

            GetRequestBuilder(FakeHttpHandlers.For<IA2PartyImportService>(), partyUuid)
                .Respond(
                "application/json",
                """
                {
                  "UserId": 20002571,
                  "UserUUID": "b049a483-dfea-437e-bb89-261a5390f207",
                  "UserType": 2,
                  "UserName": "AdvancedSettingsTest",
                  "ExternalIdentity": "",
                  "IsReserved": false,
                  "PhoneNumber": null,
                  "Email": "AdvancedSettingsTest@AdvancedSettingsTest.no",
                  "PartyId": 50068492,
                  "Party": {
                    "PartyTypeName": 3,
                    "SSN": "",
                    "OrgNumber": "",
                    "Person": null,
                    "Organization": null,
                    "PartyId": 50068492,
                    "PartyUUID": "b049a483-dfea-437e-bb89-261a5390f207",
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

            var service = GetRequiredService<IA2PartyImportService>();

            var result = await GetUserParty(service, partyUuid);

            result.IsProblem.ShouldBeFalse();
            result.Value.ShouldNotBeNull();
            result.Value.ShouldBe(new PartyUserRecord(userId: 20002571U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(20002571U)));
        }
    }

    public class GetOrCreatePersonUserTests
        : GetUserPartyTests
    {
        protected override IFakeRequestBuilder GetRequestBuilder(FakeHttpMessageHandler handler, Guid partyUuid)
            => handler.Expect(HttpMethod.Get, "profile/api/users/getorcreate/{userUuid:guid}")
                .WithRouteValue("userUuid", partyUuid.ToString());

        protected override Task<Result<PartyUserRecord>> GetUserParty(IA2PartyImportService service, Guid partyUuid)
            => service.GetOrCreatePersonUser(partyUuid, TestContext.Current.CancellationToken);
    }
}
