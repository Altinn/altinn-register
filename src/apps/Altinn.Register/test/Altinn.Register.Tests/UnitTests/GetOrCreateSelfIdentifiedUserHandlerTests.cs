using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts;
using Altinn.Register.Core.A2.SblProfile;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Register.Tests.UnitTests;

public class GetOrCreateSelfIdentifiedUserHandlerTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task IdPortenEmail_LookupHit_ReturnsExistingUser()
    {
        var existing = new SblUserProfile
        {
            UserId = 42,
            UserUuid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserName = "altinn-existing-1",
            PartyId = 50001,
            ExternalIdentity = "urn:altinn:person:idporten-email:dXNlckBleGFtcGxlLmNvbQ",
            UserType = 2,
        };

        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)new SblUserLookup(existing));

        var handler = new GetOrCreateSelfIdentifiedUserFromBridgeHandler(
            bridge.Object,
            NullLogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler>.Instance);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            Email: "user@example.com",
            Issuer: null,
            ExternalSubject: null,
            UserNamePrefix: null);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.UserId.ShouldBe(42u);
        result.Value.PartyId.ShouldBe(50001u);
        result.Value.UserName.ShouldBe("altinn-existing-1");
        result.Value.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.IdPortenEmail);
        result.Value.ExternalUrn.ShouldStartWith("urn:altinn:person:idporten-email:");

        bridge.Verify(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IdPortenEmail_LookupMiss_CreatesNewUser()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)SblUserLookup.NotFound);

        SblUserProfile? capturedCreate = null;
        var created = new SblUserProfile
        {
            UserId = 99,
            UserUuid = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UserName = "altinn-newuser-aB1cD2",
            PartyId = 50002,
            ExternalIdentity = "urn:altinn:person:idporten-email:bmV3QGV4YW1wbGUuY29t",
            UserType = 2,
        };
        bridge.Setup(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<SblUserProfile, CancellationToken>((p, _) => capturedCreate = p)
            .ReturnsAsync((Result<SblUserProfile>)created);

        var handler = new GetOrCreateSelfIdentifiedUserFromBridgeHandler(
            bridge.Object,
            NullLogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler>.Instance);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            Email: "new@example.com",
            Issuer: null,
            ExternalSubject: null,
            UserNamePrefix: "altinn-");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.UserId.ShouldBe(99u);

        capturedCreate.ShouldNotBeNull();
        capturedCreate!.UserType.ShouldBe(2);
        capturedCreate.UserName.ShouldStartWith("altinn-");
        capturedCreate.ExternalIdentity.ShouldStartWith("urn:altinn:person:idporten-email:");
        capturedCreate.ExternalIdentity.ShouldContain("new@example.com");
    }

    [Fact]
    public async Task Legacy_LookupMiss_CreatesNewUser_WithIssSubExternalIdentity()
    {
        SblUserProfile? capturedCreate = null;
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)SblUserLookup.NotFound);
        bridge.Setup(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<SblUserProfile, CancellationToken>((p, _) => capturedCreate = p)
            .ReturnsAsync((Result<SblUserProfile>)new SblUserProfile
            {
                UserId = 7,
                UserUuid = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                UserName = "altinn-aB1cD2Ef3-x7q9w2",
                PartyId = 50003,
                ExternalIdentity = "https://example-idp:sub-abc",
                UserType = 2,
            });

        var handler = new GetOrCreateSelfIdentifiedUserFromBridgeHandler(
            bridge.Object,
            NullLogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler>.Instance);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.Legacy,
            Email: null,
            Issuer: "https://example-idp",
            ExternalSubject: "sub-abc",
            UserNamePrefix: null);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.Legacy);
        result.Value.ExternalUrn.ShouldStartWith("urn:altinn:person:legacy-selfidentified:");

        capturedCreate.ShouldNotBeNull();
        capturedCreate!.ExternalIdentity.ShouldBe("https://example-idp:sub-abc");
    }

    [Fact]
    public async Task IdPortenEmail_MissingEmail_ReturnsValidationProblem()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);

        var handler = new GetOrCreateSelfIdentifiedUserFromBridgeHandler(
            bridge.Object,
            NullLogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler>.Instance);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            Email: null,
            Issuer: null,
            ExternalSubject: null,
            UserNamePrefix: null);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);

        bridge.Verify(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Legacy_MissingIssuer_ReturnsValidationProblem()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);

        var handler = new GetOrCreateSelfIdentifiedUserFromBridgeHandler(
            bridge.Object,
            NullLogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler>.Instance);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.Legacy,
            Email: null,
            Issuer: null,
            ExternalSubject: "sub-abc",
            UserNamePrefix: null);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);
    }

    [Fact]
    public async Task Educational_LookupMiss_CreatesNewUser_WithIssSubExternalIdentity_AndNullUrn()
    {
        SblUserProfile? capturedCreate = null;
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)SblUserLookup.NotFound);
        bridge.Setup(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<SblUserProfile, CancellationToken>((p, _) => capturedCreate = p)
            .ReturnsAsync((Result<SblUserProfile>)new SblUserProfile
            {
                UserId = 11,
                UserUuid = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                UserName = "altinn-edu99-x7q9w2",
                PartyId = 50004,
                ExternalIdentity = "https://feide.no:edu-sub-abc",
                UserType = 2,
            });

        var handler = new GetOrCreateSelfIdentifiedUserFromBridgeHandler(
            bridge.Object,
            NullLogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler>.Instance);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.Educational,
            Email: null,
            Issuer: "https://feide.no",
            ExternalSubject: "edu-sub-abc",
            UserNamePrefix: null);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.Educational);
        result.Value.UserId.ShouldBe(11u);
        result.Value.ExternalUrn.ShouldBeNull();

        capturedCreate.ShouldNotBeNull();
        capturedCreate!.ExternalIdentity.ShouldBe("https://feide.no:edu-sub-abc");
    }

    [Fact]
    public async Task Educational_MissingIssuer_ReturnsValidationProblem()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);

        var handler = new GetOrCreateSelfIdentifiedUserFromBridgeHandler(
            bridge.Object,
            NullLogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler>.Instance);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.Educational,
            Email: null,
            Issuer: null,
            ExternalSubject: "edu-sub-abc",
            UserNamePrefix: null);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);
    }

    [Fact]
    public void GenerateUserName_UsesCustomPrefix_AndProducesShortStableSegment()
    {
        var name1 = GetOrCreateSelfIdentifiedUserFromBridgeHandler.GenerateUserName("urn:altinn:person:idporten-email:abc", "test-");
        var name2 = GetOrCreateSelfIdentifiedUserFromBridgeHandler.GenerateUserName("urn:altinn:person:idporten-email:abc", "test-");

        name1.ShouldStartWith("test-");
        name2.ShouldStartWith("test-");

        // Same hashed segment for the same external identity, but suffix randomized
        var hashed1 = name1.Substring("test-".Length, 10);
        var hashed2 = name2.Substring("test-".Length, 10);
        hashed1.ShouldBe(hashed2);
        name1.ShouldNotBe(name2);
    }

    [Fact]
    public async Task LookupFails_ReturnsProblem()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)Problems.SblProfileBridgeUnavailable.Create());

        var handler = new GetOrCreateSelfIdentifiedUserFromBridgeHandler(
            bridge.Object,
            NullLogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler>.Instance);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            Email: "user@example.com",
            Issuer: null,
            ExternalSubject: null,
            UserNamePrefix: null);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SblProfileBridgeUnavailable.ErrorCode);
    }
}
