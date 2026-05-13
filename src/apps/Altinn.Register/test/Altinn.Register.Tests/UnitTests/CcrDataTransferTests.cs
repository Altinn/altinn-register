using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using Altinn.Register.Integrations.Ccr.FileImport;
using Moq;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace Altinn.Register.Tests.UnitTests;

public class CcrDataTransferTests
{
    private const string RemotePath = "/remote/path";
    private const string MockFile1LongName = "/remote/path/baj05778.txtretrieved";
    private const string MockFile1Name = "baj05778.txtretrieved";
    private const string MockFile1DownloadedLongName = "/remote/path/baj05778Downloaded.txtretrieved";
    private const string MockFile2LongName = "/remote/path/baj05779.txtretrieved";
    private const string MockFile2Name = "baj05779.txtretrieved";
    private const string MockFile2DownloadedLongName = "/remote/path/baj05779Downloaded.txtretrieved";
    private const string MockFile3LongName = "/remote/path/baj05780.txt";
    private const string MockFile3Name = "baj05780.txt";
    private const string MockFile3DownloadedLongName = "/remote/path/baj05780Downloaded.txt";
    private const string MockFile4LongName = "/remote/path/baj05781.txt";
    private const string MockFile4DownloadedLongName = "/remote/path/baj05781Downloaded.txt";
    private const string MockFile4Name = "baj05781.txt";

    [Fact]
    public async Task GetNextFileAsync_ReturnsNextFileFromSftp()
    {
        // Arrange
        var pipe = new Pipe();
        var writer = pipe.Writer;
        var reader = pipe.Reader;

        var mockClient = new Mock<ISftpClient>();
        var remotePath = RemotePath;

        var mockFile1 = CreateMockFile(MockFile1LongName, MockFile1Name);
        var mockFile2 = CreateMockFile(MockFile2LongName, MockFile2Name);
        var mockFile3 = CreateMockFile(MockFile3LongName, MockFile3Name);
        var mockFile4 = CreateMockFile(MockFile4LongName, MockFile4Name);
        var mockFile5 = CreateMockFile("/remote/path/baj05777Downloaded.txtretrieved", "baj05777Downloaded.txtretrieved");
        var mockFile6 = CreateMockFile("/remote/path/bns00000.txt", "bns00000.txt");

        var files = new List<ISftpFile> { mockFile1.Object, mockFile2.Object, mockFile3.Object, mockFile4.Object, mockFile5.Object, mockFile6.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var text = "content of file1";
        var content1 = Encoding.ASCII.GetBytes(text);
        
        mockClient.Setup(c => c.DownloadFileAsync("/remote/path/baj05778.txtretrieved", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, CancellationToken>((path, stream, cancellationToken) =>
             {
                stream.Write(content1, 0, content1.Length);
                int a = 0;
             });

        var client = new CcrDataTransfer(remotePath, mockClient.Object);

        // Act
        var result = await client.GetNextFileAsync(writer, -1, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);        
        var readResult = await reader.ReadAsync(TestContext.Current.CancellationToken);
        var buffer = readResult.Buffer;
        string resultContent = Encoding.ASCII.GetString(buffer.ToArray());
        Assert.Equal(text, resultContent);

        // Verify rename was called
        mockClient.Verify(c => c.DownloadFileAsync("/remote/path/baj05777Downloaded.txtretrieved", It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile1LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile2LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile3LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile4LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile1LongName, "/remote/path/baj05778Downloaded.txtretrieved"), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile2LongName, "/remote/path/baj05779Downloaded.txtretrieved"), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile3LongName, "/remote/path/baj05780Downloaded.txt"), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile4LongName, "/remote/path/baj05781Downloaded.txt"), Times.Never);
        mockClient.Verify(c => c.Connect(), Times.Once);
        mockClient.Verify(c => c.Disconnect(), Times.Once);
    }

    [Fact]
    public async Task GetNextFileAsync_ReturnsNextFileFromSftp_ValueGiven()
    {
        // Arrange
        var pipe = new Pipe();
        var writer = pipe.Writer;
        var reader = pipe.Reader;
        var mockClient = new Mock<ISftpClient>();
        var remotePath = RemotePath;

        var mockFile1 = CreateMockFile(MockFile1LongName, "baj05778.txtretrieved");
        var mockFile2 = CreateMockFile(MockFile2LongName, "baj05779.txtretrieved");
        var mockFile3 = CreateMockFile(MockFile3LongName, "baj05780.txt");
        var mockFile4 = CreateMockFile(MockFile4LongName, "baj05781.txt");

        var files = new List<ISftpFile> { mockFile1.Object, mockFile2.Object, mockFile3.Object, mockFile4.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var text = "content of file4";
        var content4 = Encoding.ASCII.GetBytes(text);

        mockClient.Setup(c => c.DownloadFileAsync(MockFile4LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, CancellationToken>((path, stream, cancellationToken) =>
             {
                stream.Write(content4, 0, content4.Length);
                int a = 0;
             });

        var client = new CcrDataTransfer(remotePath, mockClient.Object);

        // Act
        var result = await client.GetNextFileAsync(writer, 5780, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);        
        var readResult = await reader.ReadAsync(TestContext.Current.CancellationToken);
        var buffer = readResult.Buffer;
        string resultContent = Encoding.ASCII.GetString(buffer.ToArray());
        Assert.Equal(text, resultContent);

        // Verify rename was called
        mockClient.Verify(c => c.DownloadFileAsync("/remote/path/baj05777Downloaded.txtretrieved", It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile1LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile2LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile3LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile4LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        mockClient.Verify(c => c.RenameFile(MockFile1LongName, "/remote/path/baj05778Downloaded.txtretrieved"), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile2LongName, "/remote/path/baj05779Downloaded.txtretrieved"), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile3LongName, "/remote/path/baj05780Downloaded.txt"), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile4LongName, "/remote/path/baj05781Downloaded.txt"), Times.Never);
        mockClient.Verify(c => c.Connect(), Times.Once);
        mockClient.Verify(c => c.Disconnect(), Times.Once);
    }
    
    [Fact]
    public async Task GetNewFiles_ReturnsNextFileFromSftp_ValueGiven_FileIsRenamed()
    {
        // Arrange
        var pipe = new Pipe();
        var writer = pipe.Writer;
        var reader = pipe.Reader;
        var mockClient = new Mock<ISftpClient>();
        var remotePath = RemotePath;

        var mockFile1 = CreateMockFile(MockFile1LongName, "baj05778.txtretrieved");
        var mockFile2 = CreateMockFile(MockFile2LongName, "baj05779.txtretrieved");
        var mockFile3 = CreateMockFile(MockFile3LongName, "baj05780.txt");
        var mockFile4 = CreateMockFile(MockFile4LongName, "baj05781.txt");

        var files = new List<ISftpFile> { mockFile1.Object, mockFile2.Object, mockFile3.Object, mockFile4.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var text = "content of file4";
        var content4 = Encoding.ASCII.GetBytes(text);

        mockClient.Setup(c => c.DownloadFileAsync(MockFile4LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, CancellationToken>((path, stream, cancellationToken) =>
             {
                stream.Write(content4, 0, content4.Length);
                int a = 0;
             });

        var client = new CcrDataTransfer(remotePath, mockClient.Object);

        // Act
        var result = await client.GetNextFileAsync(writer, 5780, TestContext.Current.CancellationToken);

        Assert.True(result);
        await client.MarkFileAsDownloadedAsync(5780, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);        
        var readResult = await reader.ReadAsync(TestContext.Current.CancellationToken);
        var buffer = readResult.Buffer;
        string resultContent = Encoding.ASCII.GetString(buffer.ToArray());
        Assert.Equal(text, resultContent);
        
        mockClient.Verify(c => c.DownloadFileAsync("/remote/path/baj05777Downloaded.txtretrieved", It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile1LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile2LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile3LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile4LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        mockClient.Verify(c => c.RenameFile(MockFile1LongName, "/remote/path/baj05778Downloaded.txtretrieved"), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile2LongName, "/remote/path/baj05779Downloaded.txtretrieved"), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile3LongName, "/remote/path/baj05780Downloaded.txt"), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile4LongName, "/remote/path/baj05781Downloaded.txt"), Times.Once);
        mockClient.Verify(c => c.Connect(), Times.Exactly(2)); // One for GetNextFileAsync and one for MarkFileAsDownloadedAsync
        mockClient.Verify(c => c.Disconnect(), Times.Exactly(2)); // One for GetNextFileAsync and one for MarkFileAsDownloadedAsync
    }

    [Fact]
    public async Task GetNewFiles_InvalidFileRetrieved()
    {
        // Arrange
        PipeWriter writer = new Pipe().Writer;
        var mockClient = new Mock<ISftpClient>();
        var remotePath = RemotePath;

        var mockFile1 = CreateMockFile("/remote/path/bajaaaaa.txtretrieved", "bajaaaaa.txtretrieved");

        var files = new List<ISftpFile> { mockFile1.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var client = new CcrDataTransfer(remotePath, mockClient.Object);

        // Assert
        await Assert.ThrowsAsync<FormatException>(() => client.GetNextFileAsync(writer, -1, TestContext.Current.CancellationToken));

        // Verify rename was called
        mockClient.Verify(c => c.Connect(), Times.Once);
        mockClient.Verify(c => c.Disconnect(), Times.Once);
    }

    [Fact]
    public async Task GetNewFiles_InvalidFileRetrieved_V2()
    {
        // Arrange
        PipeWriter writer = new Pipe().Writer;
        var mockClient = new Mock<ISftpClient>();
        var remotePath = RemotePath;

        var mockFile1 = CreateMockFile("/remote/path/bajaaaaa.a", "bajaaaaa.a");

        var files = new List<ISftpFile> { mockFile1.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var client = new CcrDataTransfer(remotePath, mockClient.Object);

        // Assert
        await Assert.ThrowsAsync<FormatException>(() => client.GetNextFileAsync(writer, -1, TestContext.Current.CancellationToken));

        // Verify rename was called
        mockClient.Verify(c => c.Connect(), Times.Once);
        mockClient.Verify(c => c.Disconnect(), Times.Once);
    }

    [Fact]
    public async Task GetSpecificFile_RetrieveSpecificFileFromSftp()
    {
        // Arrange
        var pipe = new Pipe();
        var writer = pipe.Writer;
        var reader = pipe.Reader;
        var mockClient = new Mock<ISftpClient>();
        var remotePath = RemotePath;

        var mockFile1 = CreateMockFile(MockFile1LongName, MockFile1Name);

        var files = new List<ISftpFile> { mockFile1.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var text = "content of file4";
        var content4 = Encoding.ASCII.GetBytes(text);

        mockClient.Setup(c => c.DownloadFileAsync(MockFile1LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, CancellationToken>((path, stream, cancellationToken) =>
             {
                stream.Write(content4, 0, content4.Length);
                int a = 0;
             });

        var client = new CcrDataTransfer(remotePath, mockClient.Object);

        // Act
        var result = await client.GetSpecificFileAsync(MockFile1Name, writer, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);        
        var readResult = await reader.ReadAsync(TestContext.Current.CancellationToken);
        var buffer = readResult.Buffer;
        string resultContent = Encoding.ASCII.GetString(buffer.ToArray());
        Assert.Equal(text, resultContent);
        mockClient.Verify(c => c.DownloadFileAsync(MockFile1LongName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        mockClient.Verify(c => c.Connect(), Times.Once);
        mockClient.Verify(c => c.Disconnect(), Times.Once);
    }
    
    [Fact]
    public async Task RenameFiles_RenameFilesOnSftp()
    {
        var mockClient = new Mock<ISftpClient>();
        var remotePath = RemotePath;
        
        var mockFile1 = CreateMockFile(MockFile1LongName, MockFile1Name);
        var mockFile2 = CreateMockFile(MockFile2LongName, MockFile2Name);
        var mockFile3 = CreateMockFile(MockFile3LongName, MockFile3Name);
        var mockFile4 = CreateMockFile(MockFile4LongName, MockFile4Name);

        var files = new List<ISftpFile> { mockFile1.Object, mockFile2.Object, mockFile3.Object, mockFile4.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var client = new CcrDataTransfer(remotePath, mockClient.Object);

        // Act
        var result = await client.MarkFileAsDownloadedAsync(5779, TestContext.Current.CancellationToken);
        var result2 = await client.MarkFileAsDownloadedAsync(5780, TestContext.Current.CancellationToken);
        var result3 = await client.MarkFileAsDownloadedAsync(5781, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.True(result2);
        Assert.False(result3);
        mockClient.Verify(c => c.RenameFile(MockFile1LongName, MockFile1DownloadedLongName), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile2LongName, MockFile2DownloadedLongName), Times.Never);
        mockClient.Verify(c => c.RenameFile(MockFile3LongName, MockFile3DownloadedLongName), Times.Once);
        mockClient.Verify(c => c.RenameFile(MockFile4LongName, MockFile4DownloadedLongName), Times.Once);
        mockClient.Verify(c => c.Connect(), Times.Exactly(3)); // One for each call to MarkFileAsDownloadedAsync
        mockClient.Verify(c => c.Disconnect(), Times.Exactly(3));
    }

    private static Mock<ISftpFile> CreateMockFile(string fullName, string name)
    {
        var mockFile1 = new Mock<ISftpFile>();
        mockFile1.Setup(f => f.IsDirectory).Returns(false);
        mockFile1.Setup(f => f.IsSymbolicLink).Returns(false);
        mockFile1.Setup(f => f.FullName).Returns(fullName);
        mockFile1.Setup(f => f.Name).Returns(name);
        return mockFile1;
    }
}
