using System.Net;
using System.Net.Http.Json;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.TestUtils.Http;
using Altinn.Register.Contracts;
using Altinn.Register.Core.A2.SblProfile;
using Altinn.Register.Core.Errors;
using Altinn.Register.Models;

namespace Altinn.Register.IntegrationTests.Controllers;

public class UsersControllerTests
    : IntegrationTestBase
{
    private const string EndpointUrl = "register/api/v2/internal/users/self-identified";

    [Fact]
    public async Task GetOrCreate_IdPortenEmail_LookupHit_ReturnsExisting()
    {
        var existing = new SblUserProfile
        {
            UserId = 4242,
            UserUuid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserName = "altinn-existing-1",
            PartyId = 50001,
            ExternalIdentity = "urn:altinn:person:idporten-email:dXNlckBleGFtcGxlLmNvbQ",
            UserType = 2,
        };

        FakeHttpHandlers.For<ISblProfileBridgeClient>()
            .Expect(HttpMethod.Post, "/profile/api/users/")
            .Respond(() => JsonContent.Create(existing, options: JsonOptions));

        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
            .WithPlatformToken("unittest");
        request.Content = JsonContent.Create(new SelfIdentifiedUserCreateRequest
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.IdPortenEmail,
            Email = "user@example.com",
            UserName = "epost:user@example.com",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var body = await response.ShouldHaveJsonContent<SelfIdentifiedUserResponse>();

        body.ShouldNotBeNull();
        body.UserId.ShouldBe(4242u);
        body.PartyId.ShouldBe(50001u);
        body.PartyUuid.ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        body.UserName.ShouldBe("altinn-existing-1");
        body.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.IdPortenEmail);
        body.ExternalUrn.ShouldStartWith("urn:altinn:person:idporten-email:");
    }

    [Fact]
    public async Task GetOrCreate_IdPortenEmail_LookupMiss_CreatesUser()
    {
        FakeHttpHandlers.For<ISblProfileBridgeClient>()
            .Expect(HttpMethod.Post, "/profile/api/users/")
            .Respond(HttpStatusCode.NotFound);

        var created = new SblUserProfile
        {
            UserId = 9001,
            UserUuid = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UserName = "altinn-newhash99-x7q9w2",
            PartyId = 50002,
            ExternalIdentity = "urn:altinn:person:idporten-email:bmV3QGV4YW1wbGUuY29t",
            UserType = 2,
        };

        FakeHttpHandlers.For<ISblProfileBridgeClient>()
            .Expect(HttpMethod.Post, "/profile/api/users/create/")
            .Respond(() => JsonContent.Create(created, options: JsonOptions));

        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
            .WithPlatformToken("unittest");
        request.Content = JsonContent.Create(new SelfIdentifiedUserCreateRequest
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.IdPortenEmail,
            Email = "new@example.com",
            UserName = "epost:new@example.com",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var body = await response.ShouldHaveJsonContent<SelfIdentifiedUserResponse>();

        body.ShouldNotBeNull();
        body.UserId.ShouldBe(9001u);
    }

    [Fact]
    public async Task GetOrCreate_Educational_LookupMiss_CreatesUser_WithNullExternalUrn()
    {
        FakeHttpHandlers.For<ISblProfileBridgeClient>()
            .Expect(HttpMethod.Post, "/profile/api/users/")
            .Respond(HttpStatusCode.NotFound);

        var created = new SblUserProfile
        {
            UserId = 11011,
            UserUuid = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            UserName = "altinn-eduhash99-x7q9w2",
            PartyId = 50004,
            ExternalIdentity = "https://feide.no:edu-sub-abc",
            UserType = 2,
        };

        FakeHttpHandlers.For<ISblProfileBridgeClient>()
            .Expect(HttpMethod.Post, "/profile/api/users/create/")
            .Respond(() => JsonContent.Create(created, options: JsonOptions));

        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
            .WithPlatformToken("unittest");
        request.Content = JsonContent.Create(new SelfIdentifiedUserCreateRequest
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.Educational,
            Issuer = "https://feide.no",
            ExternalSubject = "edu-sub-abc",
            UserName = "altinn-eduhash99-x7q9w2",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var body = await response.ShouldHaveJsonContent<SelfIdentifiedUserResponse>();

        body.ShouldNotBeNull();
        body.UserId.ShouldBe(11011u);
        body.PartyId.ShouldBe(50004u);
        body.PartyUuid.ShouldBe(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        body.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.Educational);
        body.ExternalUrn.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrCreate_Educational_LookupHit_ReturnsExisting_WithNullExternalUrn()
    {
        var existing = new SblUserProfile
        {
            UserId = 11012,
            UserUuid = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            UserName = "altinn-eduexisting-1",
            PartyId = 50005,
            ExternalIdentity = "https://feide.no:edu-sub-existing",
            UserType = 2,
        };

        FakeHttpHandlers.For<ISblProfileBridgeClient>()
            .Expect(HttpMethod.Post, "/profile/api/users/")
            .Respond(() => JsonContent.Create(existing, options: JsonOptions));

        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
            .WithPlatformToken("unittest");
        request.Content = JsonContent.Create(new SelfIdentifiedUserCreateRequest
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.Educational,
            Issuer = "https://feide.no",
            ExternalSubject = "edu-sub-existing",
            UserName = "altinn-eduexisting-1",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var body = await response.ShouldHaveJsonContent<SelfIdentifiedUserResponse>();

        body.ShouldNotBeNull();
        body.UserId.ShouldBe(11012u);
        body.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.Educational);
        body.ExternalUrn.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrCreate_Educational_MissingIssuer_ReturnsBadRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
            .WithPlatformToken("unittest");
        request.Content = JsonContent.Create(new SelfIdentifiedUserCreateRequest
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.Educational,
            ExternalSubject = "edu-sub-abc",
            UserName = "altinn-test",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        var problem = await response.ShouldHaveJsonContent<AltinnProblemDetails>();
        problem.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);
    }

    [Fact]
    public async Task GetOrCreate_MissingEmail_ReturnsBadRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
            .WithPlatformToken("unittest");
        request.Content = JsonContent.Create(new SelfIdentifiedUserCreateRequest
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.IdPortenEmail,
            UserName = "altinn-test",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        var problem = await response.ShouldHaveJsonContent<AltinnProblemDetails>();
        problem.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);
    }

    [Fact]
    public async Task GetOrCreate_BridgeUnavailable_ReturnsBadGateway()
    {
        FakeHttpHandlers.For<ISblProfileBridgeClient>()
            .Expect(HttpMethod.Post, "/profile/api/users/")
            .Respond(HttpStatusCode.InternalServerError);

        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
            .WithPlatformToken("unittest");
        request.Content = JsonContent.Create(new SelfIdentifiedUserCreateRequest
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.IdPortenEmail,
            Email = "x@example.com",
            UserName = "epost:x@example.com",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadGateway);
        var problem = await response.ShouldHaveJsonContent<AltinnProblemDetails>();
        problem.ErrorCode.ShouldBe(Problems.SblProfileBridgeUnavailable.ErrorCode);
    }

    [Fact]
    public async Task GetOrCreate_Unauthorized_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl);
        request.Headers.Authorization = new("Bearer", "bogus");
        request.Content = JsonContent.Create(new SelfIdentifiedUserCreateRequest
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.IdPortenEmail,
            Email = "x@example.com",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.Unauthorized);
    }
}
