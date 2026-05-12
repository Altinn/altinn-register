using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Contracts;
using Altinn.Register.Contracts.PartyImport.A2;
using Altinn.Register.Core.A2.SblProfile;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Register.Tests.UnitTests;

public class GetOrCreateSelfIdentifiedUserHandlerTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static GetOrCreateSelfIdentifiedUserFromBridgeHandler CreateHandler(
        ISblProfileBridgeClient bridge,
        ICommandSender sender)
        => new(bridge, sender, NullLogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler>.Instance);

    private static Mock<ICommandSender> CreateSenderMock()
    {
        var sender = new Mock<ICommandSender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return sender;
    }

    [Fact]
    public async Task IdPortenEmail_LookupHit_ReturnsExistingUser()
    {
        const string ExternalIdentity = "urn:altinn:person:idporten-email:dXNlckBleGFtcGxlLmNvbQ";

        var existing = new SblUserProfile
        {
            UserId = 42,
            UserUuid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserName = "epost:user@example.com",
            PartyId = 50001,
            ExternalIdentity = ExternalIdentity,
            UserType = 2,
        };

        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)new SblUserLookup(existing));

        var sender = CreateSenderMock();
        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            ExternalIdentity: ExternalIdentity,
            UserName: "epost:user@example.com");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.UserId.ShouldBe(42u);
        result.Value.PartyId.ShouldBe(50001u);
        result.Value.UserName.ShouldBe("epost:user@example.com");
        result.Value.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.IdPortenEmail);
        result.Value.ExternalUrn.ShouldBe(ExternalIdentity);

        bridge.Verify(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IdPortenEmail_LookupMiss_CreatesNewUser_PassesIdentityAndUsernameVerbatim()
    {
        // Matches altinn-authentication's selfregistered-email path: the caller has already built
        // the urn:altinn:person:idporten-email URN and chosen `epost:<email>` as the username.
        const string ExternalIdentity = "urn:altinn:person:idporten-email:bmV3QGV4YW1wbGUuY29t";
        const string UserName = "epost:new@example.com";

        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(ExternalIdentity, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)SblUserLookup.NotFound);

        SblUserProfile? capturedCreate = null;
        bridge.Setup(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<SblUserProfile, CancellationToken>((p, _) => capturedCreate = p)
            .ReturnsAsync((Result<SblUserProfile>)new SblUserProfile
            {
                UserId = 99,
                UserUuid = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                UserName = UserName,
                PartyId = 50002,
                ExternalIdentity = ExternalIdentity,
                UserType = 2,
            });

        ImportA2PartyCommand? capturedImport = null;
        var sender = new Mock<ICommandSender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ImportA2PartyCommand, CancellationToken>((c, _) => capturedImport = c)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            ExternalIdentity: ExternalIdentity,
            UserName: UserName);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.UserId.ShouldBe(99u);

        capturedCreate.ShouldNotBeNull();
        capturedCreate!.UserType.ShouldBe(2);
        capturedCreate.UserName.ShouldBe(UserName);
        capturedCreate.ExternalIdentity.ShouldBe(ExternalIdentity);

        capturedImport.ShouldNotBeNull();
        capturedImport!.PartyUuid.ShouldBe(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        capturedImport.Tracking.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task Legacy_LookupMiss_CreatesNewUser_PassesIdentityAndUsernameVerbatim()
    {
        const string ExternalIdentity = "https://example-idp:sub-abc";
        const string UserName = "altinn-aB1cD2Ef3-x7q9w2";

        SblUserProfile? capturedCreate = null;
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(ExternalIdentity, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)SblUserLookup.NotFound);
        bridge.Setup(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<SblUserProfile, CancellationToken>((p, _) => capturedCreate = p)
            .ReturnsAsync((Result<SblUserProfile>)new SblUserProfile
            {
                UserId = 7,
                UserUuid = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                UserName = UserName,
                PartyId = 50003,
                ExternalIdentity = ExternalIdentity,
                UserType = 2,
            });

        var sender = CreateSenderMock();
        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.Legacy,
            ExternalIdentity: ExternalIdentity,
            UserName: UserName);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.Legacy);
        result.Value.ExternalUrn.ShouldStartWith("urn:altinn:person:legacy-selfidentified:");

        capturedCreate.ShouldNotBeNull();
        capturedCreate!.ExternalIdentity.ShouldBe(ExternalIdentity);
        capturedCreate.UserName.ShouldBe(UserName);

        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Educational_LookupMiss_CreatesNewUser_WithNullExternalUrn()
    {
        // Matches altinn-authentication's uidp-anonym provider: Iss is the provider config key,
        // ExternalSubject is a SHA-256 hex hash from upstream, UserName is `uidp_` + hash segment + random suffix.
        const string ExternalIdentity = "uidp-anonym:66a633c43ef2f656978f957532ce6d0de6f5e13f1e0618b37b4b2a70573e5551";
        const string UidpUserName = "uidp_ej2krar0cl833";

        SblUserProfile? capturedCreate = null;
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(ExternalIdentity, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)SblUserLookup.NotFound);
        bridge.Setup(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<SblUserProfile, CancellationToken>((p, _) => capturedCreate = p)
            .ReturnsAsync((Result<SblUserProfile>)new SblUserProfile
            {
                UserId = 11,
                UserUuid = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                UserName = UidpUserName,
                PartyId = 50004,
                ExternalIdentity = ExternalIdentity,
                UserType = 2,
            });

        var sender = CreateSenderMock();
        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.Educational,
            ExternalIdentity: ExternalIdentity,
            UserName: UidpUserName);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.Educational);
        result.Value.UserId.ShouldBe(11u);
        result.Value.UserName.ShouldBe(UidpUserName);
        result.Value.ExternalUrn.ShouldBeNull();

        capturedCreate.ShouldNotBeNull();
        capturedCreate!.UserName.ShouldBe(UidpUserName);
        capturedCreate.ExternalIdentity.ShouldBe(ExternalIdentity);

        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MissingExternalIdentity_ReturnsValidationProblem()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        var sender = CreateSenderMock();

        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            ExternalIdentity: null,
            UserName: "epost:user@example.com");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);

        bridge.Verify(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingUserName_ReturnsValidationProblem()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        var sender = CreateSenderMock();

        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            ExternalIdentity: "urn:altinn:person:idporten-email:abc",
            UserName: null);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);

        bridge.Verify(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LookupFails_ReturnsProblem()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)Problems.SblProfileBridgeUnavailable.Create());

        var sender = CreateSenderMock();
        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            ExternalIdentity: "urn:altinn:person:idporten-email:abc",
            UserName: "epost:user@example.com");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SblProfileBridgeUnavailable.ErrorCode);

        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateSucceeds_ButEnqueueImportThrows_StillReturnsSuccess()
    {
        const string ExternalIdentity = "urn:altinn:person:idporten-email:Zmxha3lAZXhhbXBsZS5jb20";

        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(ExternalIdentity, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)SblUserLookup.NotFound);
        bridge.Setup(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserProfile>)new SblUserProfile
            {
                UserId = 13,
                UserUuid = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                UserName = "epost:flaky@example.com",
                PartyId = 50005,
                ExternalIdentity = ExternalIdentity,
                UserType = 2,
            });

        var sender = new Mock<ICommandSender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus down"));

        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            ExternalIdentity: ExternalIdentity,
            UserName: "epost:flaky@example.com");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.UserId.ShouldBe(13u);
        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
