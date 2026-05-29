using System.IO.Pipelines;
using Altinn.Register.Core.Ccr;
using Altinn.Register.Core.ImportJobs.FileProcessing;
using Altinn.Register.Integrations.Ccr.FileImport;
using Altinn.Register.Tests.Utils;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.Sftp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Tests.PartyImport.Ccr;

/// <summary>
/// Integration tests for <see cref="CcrDataTransfer"/> against a real SFTP server running in a
/// test-container. These don't need the web application - only the shared SFTP server.
/// </summary>
public class CcrDataTransferSftpTests
{
    private static readonly TestDataFileProvider _ccrFiles = TestDataFileProvider.For("Ccr/FlatFile");

    [Fact]
    public async Task ProcessNextFile_DownloadsUploadedFileFromSftp()
    {
        var ct = TestContext.Current.CancellationToken;
        var sftp = await TestContext.Current.GetRequiredFixture<SftpServerFixture>();
        var server = await sftp.CreateTestServer();

        // Local test data is "test1.txt" (the canonical fixture the parser tests share);
        // the SFTP server expects production's filename convention "baj{runId:D5}.txt".
        var expected = await ReadAllBytesAsync(_ccrFiles.GetFileInfo("test1.txt"), ct);
        await SftpServerFixture.UploadFilesAsync(server, [("baj00001.txt", expected)], ct);

        // Wire the production DefaultSftpClientFactory + CcrDataTransfer via real named-options
        // binding, so this also exercises the production DI/options path.
        await using var provider = new ServiceCollection()
            .AddOptions<SftpClientSettings>(nameof(ICcrFlatFileService))
                .Configure(s =>
                {
                    s.Host = server.Host;
                    s.Port = checked((ushort)server.Port);
                    s.User = server.Username;
                    s.Password = server.Password;
                    s.RemotePath = server.UploadDirectory;
                })
                .Services
            .BuildServiceProvider();

        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<SftpClientSettings>>();
        ICcrFlatFileService service = new CcrDataTransfer(new DefaultSftpClientFactory(optionsMonitor));

        var processor = new CapturingProcessor();
        var result = await service.ProcessNextFile(processor, lastRunId: 0, ct);

        result.IsProblem.ShouldBeFalse();
        result.Value.ShouldBe(CcrFlatFileOperationResult.FileProcessed);

        processor.FileName.ShouldBe("baj00001.txt");
        processor.SequenceNumber.ShouldBe(1U);
        processor.Content.ShouldBe(expected);
    }

    private static async Task<byte[]> ReadAllBytesAsync(IFileInfo file, CancellationToken cancellationToken)
    {
        using var stream = file.CreateReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
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

    /// <summary>
    /// Captures the file name, sequence number and full byte content from a single
    /// <see cref="ICcrFlatFileService.ProcessNextFile"/> invocation.
    /// </summary>
    private sealed class CapturingProcessor
        : IFileProcessor<CcrOpenedFileInfo>
    {
        public string? FileName { get; private set; }

        public uint SequenceNumber { get; private set; }

        public byte[]? Content { get; private set; }

        public async Task ProcessFileAsync(CcrOpenedFileInfo fileInfo, CancellationToken cancellationToken)
        {
            FileName = fileInfo.Name;
            SequenceNumber = fileInfo.SequenceNumber;
            Content = await ReadAllBytesAsync(fileInfo.Reader, cancellationToken);
        }
    }
}
