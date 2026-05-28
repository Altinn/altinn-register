using Altinn.Authorization.ServiceDefaults.Jobs;
using Altinn.Register.PartyImport.Ccr;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.Sftp;
using Microsoft.Extensions.Configuration;

namespace Altinn.Register.IntegrationTests.PartyImport.Ccr;

/// <summary>
/// End-to-end test for <see cref="CcrImportJob"/>: configured through the production
/// <c>Altinn:register:PartyImport:Ccr:Sftp</c> settings, it should fetch a CCR flat file from a
/// real SFTP server (running in a test-container) and run to completion.
/// </summary>
public class CcrImportJobTests
    : IntegrationTestBase
{
    private static readonly TestDataFileProvider _ccrFiles = TestDataFileProvider.For("Ccr/FlatFile");

    private SftpServerInfo _server = null!;

    protected override async ValueTask InitializeAsync()
    {
        // Allocate a unique SFTP upload directory and stage the CCR file before the host is built,
        // so ConfigureConfiguration can point the SFTP settings at this test's isolated directory.
        var sftp = await TestContext.Current.GetRequiredFixture<SftpServerFixture>();
        _server = await sftp.CreateTestServer();

        byte[] fileContent;
        using (var stream = _ccrFiles.GetFileInfo("baj00001.txt").CreateReadStream())
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, CancellationToken);
            fileContent = buffer.ToArray();
        }

        await SftpServerFixture.UploadFilesAsync(_server, [("baj00001.txt", fileContent)], CancellationToken);

        await base.InitializeAsync();
    }

    protected override void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Point the production-registered SftpClientSettings at this test's SFTP container dir.
        _server.Configure(configuration, "Altinn:register:PartyImport:Ccr:Sftp");
    }

    [Fact]
    public async Task RunAsync_FetchesAndProcessesCcrFileFromSftp()
    {
        var job = GetRequiredService<CcrImportJob>();

        await Should.NotThrowAsync(async () => await ((IJob)job).RunAsync(CancellationToken));
    }
}
