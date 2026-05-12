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

        var sender = CreateSenderMock();
        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            Email: "user@example.com",
            Issuer: null,
            ExternalSubject: null,
            UserName: "altinn-test-user");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.UserId.ShouldBe(42u);
        result.Value.PartyId.ShouldBe(50001u);
        result.Value.UserName.ShouldBe("altinn-existing-1");
        result.Value.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.IdPortenEmail);
        result.Value.ExternalUrn.ShouldStartWith("urn:altinn:person:idporten-email:");

        bridge.Verify(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Never);
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

        ImportA2PartyCommand? capturedImport = null;
        var sender = new Mock<ICommandSender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ImportA2PartyCommand, CancellationToken>((c, _) => capturedImport = c)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            Email: "new@example.com",
            Issuer: null,
            ExternalSubject: null,
            UserName: "epost:new@example.com");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.UserId.ShouldBe(99u);

        capturedCreate.ShouldNotBeNull();
        capturedCreate!.UserType.ShouldBe(2);
        capturedCreate.UserName.ShouldBe("epost:new@example.com");
        capturedCreate.ExternalIdentity.ShouldStartWith("urn:altinn:person:idporten-email:");
        capturedCreate.ExternalIdentity.ShouldContain("new@example.com");

        capturedImport.ShouldNotBeNull();
        capturedImport!.PartyUuid.ShouldBe(created.UserUuid!.Value);
        capturedImport.Tracking.HasValue.ShouldBeFalse();
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

        var sender = CreateSenderMock();
        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.Legacy,
            Email: null,
            Issuer: "https://example-idp",
            ExternalSubject: "sub-abc",
            UserName: "altinn-test-user");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.Legacy);
        result.Value.ExternalUrn.ShouldStartWith("urn:altinn:person:legacy-selfidentified:");

        capturedCreate.ShouldNotBeNull();
        capturedCreate!.ExternalIdentity.ShouldBe("https://example-idp:sub-abc");

        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IdPortenEmail_MissingEmail_ReturnsValidationProblem()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        var sender = CreateSenderMock();

        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            Email: null,
            Issuer: null,
            ExternalSubject: null,
            UserName: "altinn-test-user");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);

        bridge.Verify(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Legacy_MissingIssuer_ReturnsValidationProblem()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        var sender = CreateSenderMock();

        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.Legacy,
            Email: null,
            Issuer: null,
            ExternalSubject: "sub-abc",
            UserName: "altinn-test-user");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);

        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Educational_LookupMiss_CreatesNewUser_WithIssSubExternalIdentity_AndNullUrn()
    {
        // Matches altinn-authentication's uidp-anonym provider: Iss is the provider config key,
        // ExternalSubject is a SHA-256 hex hash from upstream, UserName is `uidp_` + hash segment + random suffix.
        const string Iss = "uidp-anonym";
        const string Sub = "66a633c43ef2f656978f957532ce6d0de6f5e13f1e0618b37b4b2a70573e5551";
        const string UidpUserName = "uidp_ej2krar0cl833";

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
                UserName = UidpUserName,
                PartyId = 50004,
                ExternalIdentity = $"{Iss}:{Sub}",
                UserType = 2,
            });

        var sender = CreateSenderMock();
        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.Educational,
            Email: null,
            Issuer: Iss,
            ExternalSubject: Sub,
            UserName: UidpUserName);

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.SelfIdentifiedUserType.ShouldBe(SelfIdentifiedUserType.Educational);
        result.Value.UserId.ShouldBe(11u);
        result.Value.UserName.ShouldBe(UidpUserName);
        result.Value.ExternalUrn.ShouldBeNull();

        capturedCreate.ShouldNotBeNull();
        capturedCreate!.UserName.ShouldBe(UidpUserName);
        capturedCreate.ExternalIdentity.ShouldBe($"{Iss}:{Sub}");

        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Educational_MissingIssuer_ReturnsValidationProblem()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        var sender = CreateSenderMock();

        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.Educational,
            Email: null,
            Issuer: null,
            ExternalSubject: "66a633c43ef2f656978f957532ce6d0de6f5e13f1e0618b37b4b2a70573e5551",
            UserName: "uidp_ej2krar0cl833");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SelfIdentifiedUserTypeMismatch.ErrorCode);

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
            Email: "user@example.com",
            Issuer: null,
            ExternalSubject: null,
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
            Email: "user@example.com",
            Issuer: null,
            ExternalSubject: null,
            UserName: "altinn-test-user");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeTrue();
        result.Problem!.ErrorCode.ShouldBe(Problems.SblProfileBridgeUnavailable.ErrorCode);

        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateSucceeds_ButEnqueueImportThrows_StillReturnsSuccess()
    {
        var bridge = new Mock<ISblProfileBridgeClient>(MockBehavior.Strict);
        bridge.Setup(b => b.LookupUser(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserLookup>)SblUserLookup.NotFound);
        bridge.Setup(b => b.CreateUser(It.IsAny<SblUserProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SblUserProfile>)new SblUserProfile
            {
                UserId = 13,
                UserUuid = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                UserName = "altinn-flaky-1",
                PartyId = 50005,
                ExternalIdentity = "urn:altinn:person:idporten-email:Zmxha3lAZXhhbXBsZS5jb20",
                UserType = 2,
            });

        var sender = new Mock<ICommandSender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus down"));

        var handler = CreateHandler(bridge.Object, sender.Object);

        var request = new GetOrCreateSelfIdentifiedUserRequest(
            SelfIdentifiedUserType: SelfIdentifiedUserType.IdPortenEmail,
            Email: "flaky@example.com",
            Issuer: null,
            ExternalSubject: null,
            UserName: "altinn-test-user");

        var result = await handler.Handle(request, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.UserId.ShouldBe(13u);
        sender.Verify(s => s.Send(It.IsAny<ImportA2PartyCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
