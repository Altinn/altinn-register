using Altinn.Authorization.ServiceDefaults.Jobs;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.PartyImport.Ccr;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.MassTransit;
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

    private SftpServerInfo? _server;

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
        if (_server is null)
        {
            throw new InvalidOperationException("SFTP server was not initialized.");
        }

        // Point the production-registered SftpClientSettings at this test's SFTP container dir.
        _server.Configure(configuration, "Altinn:register:PartyImport:Ccr:Sftp");
    }

    [Fact]
    public async Task RunAsync_FetchesAndProcessesCcrFileFromSftp()
    {
        var job = GetRequiredService<CcrImportJob>();
        var tracker = GetRequiredService<IImportJobTracker>();

        var before = await tracker.GetStatus(CcrImportJob.JobName, CancellationToken);
        before.EnqueuedMax.ShouldBe(0UL);

        await ((IJob)job).RunAsync(CancellationToken);

        // The tracker only advances after the file is fetched and the parser has actually
        // produced (and the job has enqueued) at least one organization update. A silent
        // early-exit path (no file / parse incomplete) would leave EnqueuedMax at 0.
        var after = await tracker.GetStatus(CcrImportJob.JobName, CancellationToken);
        after.EnqueuedMax.ShouldBe(1UL);

        // And at least one organization update was published to the bus.
        var anyPublished = await TestHarness.Sent
            .SelectExisting(m => m.MessageObject is ImportCcrPartyCommand)
            .AnyAsync(CancellationToken);
        anyPublished.ShouldBeTrue();
    }
}
