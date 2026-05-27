using System.IO.Pipelines;
using Altinn.Register.Integrations.Ccr.FileImport;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.Sftp;

namespace Altinn.Register.IntegrationTests.PartyImport.Ccr;

/// <summary>
/// Integration tests for <see cref="CcrDataTransfer"/> against a real SFTP server running in a
/// test-container. These don't need the web application - only the shared SFTP server.
/// </summary>
public class CcrDataTransferSftpTests
{
    [Fact]
    public async Task GetNextFileAsync_DownloadsUploadedFileFromSftp()
    {
        var ct = TestContext.Current.CancellationToken;
        var sftp = await TestContext.Current.GetRequiredFixture<SftpServerFixture>();

        var expected = await CcrFlatFileTestData.ReadBytesAsync("baj00001.txt", ct);
        var remoteDir = await sftp.UploadToNewDirectoryAsync([("baj00001.txt", expected)], ct);

        var transfer = new CcrDataTransfer(
            user: SftpServerFixture.Username,
            password: SftpServerFixture.Password,
            host: sftp.Host,
            remotePath: remoteDir,
            port: sftp.Port);

        var pipe = new Pipe();

        // lastRunId 0 -> the file with runId 1 ("baj00001.txt") is the next one to fetch.
        var found = await transfer.GetNextFileAsync(pipe.Writer, lastRunId: 0, ct);

        found.ShouldBeTrue();

        var downloaded = await ReadAllBytesAsync(pipe.Reader, ct);
        downloaded.ShouldBe(expected);
    }

    private static async Task<byte[]> ReadAllBytesAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();

        while (true)
        {
            var result = await reader.ReadAsync(cancellationToken);
            foreach (var segment in result.Buffer)
            {
                buffer.Write(segment.Span);
            }

            reader.AdvanceTo(result.Buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        await reader.CompleteAsync();
        return buffer.ToArray();
    }
}
