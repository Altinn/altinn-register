using System.IO.Pipelines;
using Altinn.Register.Core.Ccr;
using Altinn.Register.Core.ImportJobs.FileProcessing;
using Altinn.Register.Integrations.Ccr.FileImport;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.Sftp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Altinn.Register.IntegrationTests.PartyImport.Ccr;

/// <summary>
/// Integration tests for <see cref="CcrDataTransfer"/> against a real SFTP server running in a
/// test-container. These don't need the web application - only the shared SFTP server.
/// </summary>
public class CcrDataTransferSftpTests
{
    private static readonly TestDataFileProvider _ccrFiles = TestDataFileProvider.For("Ccr/FlatFile");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProcessNextFile_DownloadsUploadedFileFromSftp()
    {
        await using var context = await CreateSftpTestContextAsync();

        var sftp = await TestContext.Current.GetRequiredFixture<SftpServerFixture>();
        var server = await sftp.CreateTestServer();

        var expected = await ReadAllBytesAsync(_ccrFiles.GetFileInfo("baj00001.txt"), CancellationToken);
        await context.UploadFilesAsync([("baj00001.txt", expected)], CancellationToken);

        var processor = new CapturingProcessor();
        var result = await context.Service.ProcessNextFile(processor, lastRunId: 0, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.ShouldBe(CcrFlatFileOperationResult.FileProcessed);

        processor.FileName.ShouldBe("baj00001.txt");
        processor.SequenceNumber.ShouldBe(1U);
        processor.Content.ShouldBe(expected);
    }

    [Fact]
    public async Task ProcessNextFile_ReturnsNoFilesWhenSftpDirectoryIsEmpty()
    {
        await using var context = await CreateSftpTestContextAsync();

        var processor = new CapturingProcessor();
        var result = await context.Service.ProcessNextFile(processor, lastRunId: 0, CancellationToken);

        result.IsProblem.ShouldBeFalse();
        result.Value.ShouldBe(CcrFlatFileOperationResult.NoFileToProcess);
    }

    private static async Task<SftpTestContext> CreateSftpTestContextAsync()
    {
        var sftp = await TestContext.Current.GetRequiredFixture<SftpServerFixture>();
        var server = await sftp.CreateTestServer();

        var provider = new ServiceCollection()
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

        return new SftpTestContext(
            service,
            provider,
            (files, cancellationToken) => SftpServerFixture.UploadFilesAsync(server, files, cancellationToken));
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

    private sealed class SftpTestContext
        : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly Func<(string FileName, byte[] Content)[], CancellationToken, Task> _uploadFiles;

        public SftpTestContext(
            ICcrFlatFileService service,
            ServiceProvider provider,
            Func<(string FileName, byte[] Content)[], CancellationToken, Task> uploadFiles)
        {
            Service = service;
            _provider = provider;
            _uploadFiles = uploadFiles;
        }

        public ICcrFlatFileService Service { get; }

        public Task UploadFilesAsync((string FileName, byte[] Content)[] files, CancellationToken cancellationToken)
            => _uploadFiles(files, cancellationToken);

        public ValueTask DisposeAsync()
            => _provider.DisposeAsync();
    }
}
