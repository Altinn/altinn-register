using System.Net;
using Altinn.Register.Core.Errors;
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
            .Expect(HttpMethod.Get, "/parties")
            .WithQuery("partyuuid", partyUuid.ToString())
            .Respond(() => HttpStatusCode.InternalServerError);

        var services = GetRequiredService<IA2PartyImportService>();

        var result = await services.GetParty(partyUuid, TestContext.Current.CancellationToken);
        result.IsProblem.ShouldBeTrue();
        result.Problem.ErrorCode.ShouldBe(Problems.PartyFetchFailed.ErrorCode);
    }
}
