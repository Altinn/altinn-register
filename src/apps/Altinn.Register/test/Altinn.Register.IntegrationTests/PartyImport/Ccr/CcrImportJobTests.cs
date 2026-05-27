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
    private SftpServerFixture _sftp = null!;
    private string _remoteDir = null!;

    protected override async ValueTask InitializeAsync()
    {
        // Upload the CCR file before the host is built, so the SFTP settings can point at this
        // test's isolated remote directory.
        _sftp = await TestContext.Current.GetRequiredFixture<SftpServerFixture>();

        var fileContent = await CcrFlatFileTestData.ReadBytesAsync("baj00001.txt", CancellationToken);
        _remoteDir = await _sftp.UploadToNewDirectoryAsync([("baj00001.txt", fileContent)], CancellationToken);

        await base.InitializeAsync();
    }

    protected override void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Point the production-registered ICcrDataTransfer at the SFTP test-container, and disable
        // the recurring scheduler for this job. The test drives RunAsync explicitly; letting the
        // background scheduler also run it leaves in-flight SFTP work that doesn't cancel promptly
        // and stalls host shutdown.
        configuration.AddInMemoryCollection([
            new("Altinn:register:PartyImport:Ccr:Sftp:Host", _sftp.Host),
            new("Altinn:register:PartyImport:Ccr:Sftp:Port", _sftp.Port.ToString()),
            new("Altinn:register:PartyImport:Ccr:Sftp:User", SftpServerFixture.Username),
            new("Altinn:register:PartyImport:Ccr:Sftp:Password", SftpServerFixture.Password),
            new("Altinn:register:PartyImport:Ccr:Sftp:RemotePath", _remoteDir),
            new("Altinn:register:PartyImport:Ccr:Enable", "false"),
        ]);
    }

    [Fact]
    public async Task RunAsync_FetchesAndProcessesCcrFileFromSftp()
    {
        var job = GetRequiredService<CcrImportJob>();

        await Should.NotThrowAsync(async () => await ((IJob)job).RunAsync(CancellationToken));
    }
}
