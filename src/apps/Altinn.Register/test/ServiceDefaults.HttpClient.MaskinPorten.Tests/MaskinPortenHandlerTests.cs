using Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Handlers;
using Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Fakes;
using Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Utils;
using Microsoft.Extensions.Time.Testing;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests;

public class MaskinPortenHandlerTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly FakeMaskinPortenClient _client;
    private readonly NullHttpClient _sut;

    public MaskinPortenHandlerTests()
    {
        _timeProvider = new();
        _client = new(_timeProvider);
        _sut = new([
            new MaskinPortenHandler(_timeProvider, _client),
        ]);
    }

    [Fact]
    public async Task No_ClientName_DoesNot_SetToken()
    {
        using var request = CreateRequest(configureClientName: false);

        using var response = await _sut.SendAsync(request, TestContext.Current.CancellationToken);
        request.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task Existing_NonBearer_Token_IsNot_Modified()
    {
        using var request = CreateRequest();
        var authorization = request.Headers.Authorization = new("CustomScheme", "foo-bar-bat");

        using var response = await _sut.SendAsync(request, TestContext.Current.CancellationToken);
        request.Headers.Authorization.ShouldBeSameAs(authorization);
    }

    [Fact]
    public async Task Existing_Different_Bearer_IsNot_Modified()
    {
        using var request = CreateRequest();
        var authorization = request.Headers.Authorization = new("Bearer", "foo-bar-bat");

        using var response = await _sut.SendAsync(request, TestContext.Current.CancellationToken);
        request.Headers.Authorization.ShouldBeSameAs(authorization);
    }

    [Fact]
    public async Task Adds_Token()
    {
        using var request = CreateRequest();

        using var response = await _sut.SendAsync(request, TestContext.Current.CancellationToken);
        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("1");
    }

    [Fact]
    public async Task DifferentClientName_Throws()
    {
        using var request = CreateRequest();
        using var response1 = await _sut.SendAsync(request, TestContext.Current.CancellationToken);

        request.Options.MaskinPortenClientName = "other-fake-client";
        await Should.ThrowAsync<InvalidOperationException>(() => _sut.SendAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistingToken_Kept()
    {
        using var request = CreateRequest();
        using var response1 = await _sut.SendAsync(request, TestContext.Current.CancellationToken);

        var authorization = request.Headers.Authorization;
        authorization.ShouldNotBeNull();
        authorization.Scheme.ShouldBe("Bearer");
        authorization.Parameter.ShouldBe("1");

        // typical in a retry scenario
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        using var response2 = await _sut.SendAsync(request, TestContext.Current.CancellationToken);

        request.Headers.Authorization.ShouldBeSameAs(authorization);
        authorization.Scheme.ShouldBe("Bearer");
        authorization.Parameter.ShouldBe("1");
    }

    [Fact]
    public async Task Expired_ExistingToken_Replaced()
    {
        using var request = CreateRequest();
        using var response1 = await _sut.SendAsync(request, TestContext.Current.CancellationToken);

        var authorization = request.Headers.Authorization;
        authorization.ShouldNotBeNull();
        authorization.Scheme.ShouldBe("Bearer");
        authorization.Parameter.ShouldBe("1");

        // retry happens after token is expired
        _timeProvider.Advance(TimeSpan.FromMinutes(15));

        using var response2 = await _sut.SendAsync(request, TestContext.Current.CancellationToken);

        request.Headers.Authorization.ShouldNotBeSameAs(authorization);
        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("2");
    }

    private HttpRequestMessage CreateRequest(bool configureClientName = true)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/");

        if (configureClientName)
        {
            req.Options.MaskinPortenClientName = "fake-client";
        }

        return req;
    }
}
