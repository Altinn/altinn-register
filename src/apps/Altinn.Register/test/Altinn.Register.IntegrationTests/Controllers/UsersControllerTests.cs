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
        const string ExternalIdentity = "urn:altinn:person:idporten-email:dXNlckBleGFtcGxlLmNvbQ";

        var existing = new SblUserProfile
        {
            UserId = 4242,
            UserUuid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserName = "epost:user@example.com",
            PartyId = 50001,
            ExternalIdentity = ExternalIdentity,
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
            ExternalIdentity = ExternalIdentity,
            UserName = "epost:user@example.com",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var body = await response.ShouldHaveJsonContent<SelfIdentifiedUserResponse>();

        body.ShouldNotBeNull();
        body.UserId.ShouldBe(4242u);
        body.PartyId.ShouldBe(50001u);
        body.PartyUuid.ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        body.UserName.ShouldBe("epost:user@example.com");
        body.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.IdPortenEmail);
        body.ExternalUrn.ShouldBe(ExternalIdentity);
    }

    [Fact]
    public async Task GetOrCreate_IdPortenEmail_LookupMiss_CreatesUser()
    {
        // Caller builds the bridge-shape URN and chooses the username (see altinn-authentication's
        // selfregistered-email path: `epost:<email>` is the convention).
        const string ExternalIdentity = "urn:altinn:person:idporten-email:bmV3QGV4YW1wbGUuY29t";
        const string UserName = "epost:new@example.com";

        FakeHttpHandlers.For<ISblProfileBridgeClient>()
            .Expect(HttpMethod.Post, "/profile/api/users/")
            .Respond(HttpStatusCode.NotFound);

        var created = new SblUserProfile
        {
            UserId = 9001,
            UserUuid = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UserName = UserName,
            PartyId = 50002,
            ExternalIdentity = ExternalIdentity,
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
            ExternalIdentity = ExternalIdentity,
            UserName = UserName,
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
        // Matches altinn-authentication's uidp-anonym provider (see appsettings.test.json):
        // Iss is the provider config key, ExternalSubject is a SHA-256 hex hash, UserName is
        // `uidp_` + hash segment + random suffix (see existing_uidpuser.json). The caller
        // concatenates these into a single ExternalIdentity before calling register.
        const string ExternalIdentity = "uidp-anonym:66a633c43ef2f656978f957532ce6d0de6f5e13f1e0618b37b4b2a70573e5551";
        const string UidpUserName = "uidp_newhash99x7";

        FakeHttpHandlers.For<ISblProfileBridgeClient>()
            .Expect(HttpMethod.Post, "/profile/api/users/")
            .Respond(HttpStatusCode.NotFound);

        var created = new SblUserProfile
        {
            UserId = 11011,
            UserUuid = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            UserName = UidpUserName,
            PartyId = 50004,
            ExternalIdentity = ExternalIdentity,
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
            ExternalIdentity = ExternalIdentity,
            UserName = UidpUserName,
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
        // Existing uidp/edu user — same shape as altinn-authentication's existing_uidpuser.json scenario.
        const string ExternalIdentity = "uidp-anonym:33a633c47ef2f656978f957532ce6d0de6f5e13f1e0618b37b4b2a70573e5551";
        const string UidpUserName = "uidp_ej2krar0cl833";

        var existing = new SblUserProfile
        {
            UserId = 11012,
            UserUuid = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            UserName = UidpUserName,
            PartyId = 50005,
            ExternalIdentity = ExternalIdentity,
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
            ExternalIdentity = ExternalIdentity,
            UserName = UidpUserName,
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
    public async Task GetOrCreate_MissingExternalIdentity_ReturnsBadRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
            .WithPlatformToken("unittest");
        request.Content = JsonContent.Create(new SelfIdentifiedUserCreateRequest
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.IdPortenEmail,
            UserName = "epost:user@example.com",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        var problem = await response.ShouldHaveJsonContent<AltinnProblemDetails>();
        problem.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);
    }

    [Fact]
    public async Task GetOrCreate_MissingUserName_ReturnsBadRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
            .WithPlatformToken("unittest");
        request.Content = JsonContent.Create(new SelfIdentifiedUserCreateRequest
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.IdPortenEmail,
            ExternalIdentity = "urn:altinn:person:idporten-email:abc",
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
            ExternalIdentity = "urn:altinn:person:idporten-email:eEBleGFtcGxlLmNvbQ",
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
            ExternalIdentity = "urn:altinn:person:idporten-email:eEBleGFtcGxlLmNvbQ",
            UserName = "epost:x@example.com",
        });

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.Unauthorized);
    }
}
