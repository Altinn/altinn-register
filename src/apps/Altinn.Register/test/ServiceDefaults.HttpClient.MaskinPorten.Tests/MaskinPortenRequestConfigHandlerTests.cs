using Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Handlers;
using Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Utils;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests;

public class MaskinPortenRequestConfigHandlerTests
{
    private readonly NullHttpClient _sut
        = new([
            new MaskinPortenRequestConfigHandler("test-client"),
        ]);

    [Fact]
    public async Task Sets_ClientName_IfNotConfigured()
    {
        using var request = CreateRequest();

        using var response = await _sut.SendAsync(request, TestContext.Current.CancellationToken);
        request.Options.MaskinPortenClientName.ShouldBe("test-client");
    }

    [Fact]
    public async Task Keeps_ClientName_IfConfigured()
    {
        using var request = CreateRequest();
        request.Options.MaskinPortenClientName = "existing-value";

        using var response = await _sut.SendAsync(request, TestContext.Current.CancellationToken);
        request.Options.MaskinPortenClientName.ShouldBe("existing-value");
    }

    private HttpRequestMessage CreateRequest()
        => new HttpRequestMessage(HttpMethod.Get, "/");
}
