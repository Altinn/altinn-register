using System.Text;
using Altinn.Register.Integrations.ErDataTrans;
using Moq;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace Altinn.Register.Tests.UnitTests;

public class ErDataTransClientTests
{
    [Fact]
    public void GetNewFiles_ReturnsNewFilesFromSftp()
    {
        // Arrange
        var mockClient = new Mock<ISftpClient>();
        var remotePath = "/remote/path";

        var mockFile1 = CreateMockFile("/remote/path/baj05778.txtretrieved", "baj05778.txtretrieved");
        var mockFile2 = CreateMockFile("/remote/path/baj05779.txtretrieved", "baj05779.txtretrieved");
        var mockFile3 = CreateMockFile("/remote/path/baj05780.txt", "baj05780.txt");
        var mockFile4 = CreateMockFile("/remote/path/baj05781.txt", "baj05781.txt");
        var mockFile5 = CreateMockFile("/remote/path/baj05777Downloaded.txtretrieved", "baj05777Downloaded.txtretrieved");
        var mockFile6 = CreateMockFile("/remote/path/bns00000.txt", "bns00000.txt");

        var files = new List<ISftpFile> { mockFile1.Object, mockFile2.Object, mockFile3.Object, mockFile4.Object, mockFile5.Object, mockFile6.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var content1 = "content of file1";
        var content2 = "content of file2";
        var content3 = "content of file3";
        var content4 = "content of file4";

        mockClient.Setup(c => c.DownloadFile("/remote/path/baj05778.txtretrieved", It.IsAny<Stream>()))
        .Callback<string, Stream, Action<ulong>>((path, stream, callback) =>
        {
            var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(content1);
            writer.Flush();
        });

        mockClient.Setup(c => c.DownloadFile("/remote/path/baj05779.txtretrieved", It.IsAny<Stream>()))
            .Callback<string, Stream, Action<ulong>>((path, stream, callback) =>
            {
                var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
                writer.Write(content2);
                writer.Flush();
            });

        mockClient.Setup(c => c.DownloadFile("/remote/path/baj05780.txt", It.IsAny<Stream>()))
            .Callback<string, Stream, Action<ulong>>((path, stream, callback) =>
            {
                var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
                writer.Write(content3);
                writer.Flush();
            });

        mockClient.Setup(c => c.DownloadFile("/remote/path/baj05781.txt", It.IsAny<Stream>()))
            .Callback<string, Stream, Action<ulong>>((path, stream, callback) =>
             {
                 var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
                 writer.Write(content4);
                 writer.Flush();
             });

        var client = new ErDataTransClient(remotePath, mockClient.Object);

        // Act
        var result = client.GetNewFiles(-1);

        // Assert
        Assert.Equal(4, result.Count);

        var file1 = result[0];
        Assert.Equal("baj05778.txtretrieved", file1.Item1);
        file1.Item2.Position = 0;
        var reader1 = new StreamReader(file1.Item2);
        Assert.Equal(content1, reader1.ReadToEnd());

        var file2 = result[1];
        Assert.Equal("baj05779.txtretrieved", file2.Item1);
        file2.Item2.Position = 0;
        var reader2 = new StreamReader(file2.Item2);
        Assert.Equal(content2, reader2.ReadToEnd());

        var file3 = result[2];
        Assert.Equal("baj05780.txt", file3.Item1);
        file3.Item2.Position = 0;
        var reader3 = new StreamReader(file3.Item2);
        Assert.Equal(content3, reader3.ReadToEnd());

        var file4 = result[3];
        Assert.Equal("baj05781.txt", file4.Item1);
        file4.Item2.Position = 0;
        var reader4 = new StreamReader(file4.Item2);
        Assert.Equal(content4, reader4.ReadToEnd());

        // Verify rename was called
        mockClient.Verify(c => c.DownloadFile("/remote/path/baj05777Downloaded.txtretrieved", It.IsAny<Stream>()), Times.Never);
        mockClient.Verify(c => c.DownloadFile("/remote/path/baj05778.txtretrieved", It.IsAny<Stream>()), Times.Once);
        mockClient.Verify(c => c.DownloadFile("/remote/path/baj05779.txtretrieved", It.IsAny<Stream>()), Times.Once);
        mockClient.Verify(c => c.DownloadFile("/remote/path/baj05780.txt", It.IsAny<Stream>()), Times.Once);
        mockClient.Verify(c => c.DownloadFile("/remote/path/baj05781.txt", It.IsAny<Stream>()), Times.Once);
        mockClient.Verify(c => c.RenameFile("/remote/path/baj05778.txtretrieved", "/remote/path/baj05778Downloaded.txtretrieved"), Times.Once);
        mockClient.Verify(c => c.RenameFile("/remote/path/baj05779.txtretrieved", "/remote/path/baj05779Downloaded.txtretrieved"), Times.Once);
        mockClient.Verify(c => c.RenameFile("/remote/path/baj05780.txt", "/remote/path/baj05780Downloaded.txt"), Times.Once);
        mockClient.Verify(c => c.RenameFile("/remote/path/baj05781.txt", "/remote/path/baj05781Downloaded.txt"), Times.Once);
        mockClient.Verify(c => c.Connect(), Times.Once);
        mockClient.Verify(c => c.Disconnect(), Times.Once);
    }

    [Fact]
    public void GetNewFiles_Returns1FileFromSftp()
    {
        // Arrange
        var mockClient = new Mock<ISftpClient>();
        var remotePath = "/remote/path";

        var mockFile1 = CreateMockFile("/remote/path/baj05778.txtretrieved", "baj05778.txtretrieved");
        var mockFile2 = CreateMockFile("/remote/path/baj05779.txtretrieved", "baj05779.txtretrieved");
        var mockFile3 = CreateMockFile("/remote/path/baj05780.txt", "baj05780.txt");
        var mockFile4 = CreateMockFile("/remote/path/baj05781.txt", "baj05781.txt");

        var files = new List<ISftpFile> { mockFile1.Object, mockFile2.Object, mockFile3.Object, mockFile4.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var content1 = "content of file1";
        var content2 = "content of file2";
        var content3 = "content of file3";
        var content4 = "content of file4";

        mockClient.Setup(c => c.DownloadFile("/remote/path/baj05778.txtretrieved", It.IsAny<Stream>()))
        .Callback<string, Stream, Action<ulong>>((path, stream, callback) =>
        {
            var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(content1);
            writer.Flush();
        });

        mockClient.Setup(c => c.DownloadFile("/remote/path/baj05779.txtretrieved", It.IsAny<Stream>()))
            .Callback<string, Stream, Action<ulong>>((path, stream, callback) =>
            {
                var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
                writer.Write(content2);
                writer.Flush();
            });

        mockClient.Setup(c => c.DownloadFile("/remote/path/baj05780.txt", It.IsAny<Stream>()))
            .Callback<string, Stream, Action<ulong>>((path, stream, callback) =>
            {
                var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
                writer.Write(content3);
                writer.Flush();
            });

        mockClient.Setup(c => c.DownloadFile("/remote/path/baj05781.txt", It.IsAny<Stream>()))
            .Callback<string, Stream, Action<ulong>>((path, stream, callback) =>
             {
                 var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
                 writer.Write(content4);
                 writer.Flush();
             });

        var client = new ErDataTransClient(remotePath, mockClient.Object);

        // Act
        var result = client.GetNewFiles(5780);

        // Assert
        Assert.Single(result);

        var file4 = result[0];
        Assert.Equal("baj05781.txt", file4.Item1);
        file4.Item2.Position = 0;
        var reader4 = new StreamReader(file4.Item2);
        Assert.Equal(content4, reader4.ReadToEnd());

        // Verify rename was called
        mockClient.Verify(c => c.DownloadFile("/remote/path/baj05780.txt", It.IsAny<Stream>()), Times.Never);
        mockClient.Verify(c => c.DownloadFile("/remote/path/baj05781.txt", It.IsAny<Stream>()), Times.Once);
        mockClient.Verify(c => c.RenameFile("/remote/path/baj05778.txtretrieved", "/remote/path/baj05778Downloaded.txtretrieved"), Times.Never);
        mockClient.Verify(c => c.RenameFile("/remote/path/baj05781.txt", "/remote/path/baj05781Downloaded.txt"), Times.Once);
        mockClient.Verify(c => c.Connect(), Times.Once);
        mockClient.Verify(c => c.Disconnect(), Times.Once);
    }

    [Fact]
    public void GetNewFiles_InvalidFileRetrieved()
    {
        // Arrange
        var mockClient = new Mock<ISftpClient>();
        var remotePath = "/remote/path";

        var mockFile1 = CreateMockFile("/remote/path/bajaaaaa.txtretrieved", "bajaaaaa.txtretrieved");

        var files = new List<ISftpFile> { mockFile1.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var client = new ErDataTransClient(remotePath, mockClient.Object);

        // Assert
        Assert.Throws<FormatException>(() => client.GetNewFiles(-1));

        // Verify rename was called
        mockClient.Verify(c => c.Connect(), Times.Once);
        mockClient.Verify(c => c.Disconnect(), Times.Once);
    }

    [Fact]
    public void GetNewFiles_InvalidFileRetrieved_V2()
    {
        // Arrange
        var mockClient = new Mock<ISftpClient>();
        var remotePath = "/remote/path";

        var mockFile1 = CreateMockFile("/remote/path/bajaaaaa.a", "bajaaaaa.a");

        var files = new List<ISftpFile> { mockFile1.Object };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var client = new ErDataTransClient(remotePath, mockClient.Object);

        // Assert
        Assert.Throws<FormatException>(() => client.GetNewFiles(-1));

        // Verify rename was called
        mockClient.Verify(c => c.Connect(), Times.Once);
        mockClient.Verify(c => c.Disconnect(), Times.Once);
    }

    [Fact]
    public void GetSpecificFile_RetrieveSpecificFileFromSftp()
    {
        var mockClient = new Mock<ISftpClient>();
        var remotePath = "/remote/path";

        var mockFile1 = CreateMockFile("/remote/path/baj05778.txtretrieved", "baj05778.txtretrieved");

        var files = new List<ISftpFile> { mockFile1.Object, };

        mockClient.Setup(c => c.ListDirectory(remotePath)).Returns(files);

        var content1 = "content of file1";

        mockClient.Setup(c => c.DownloadFile("/remote/path/baj05778.txtretrieved", It.IsAny<Stream>()))
        .Callback<string, Stream, Action<ulong>>((path, stream, callback) =>
        {
            var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(content1);
            writer.Flush();
        });

        var client = new ErDataTransClient(remotePath, mockClient.Object);

        // Act
        var result = client.GetSpecificFile("baj05778.txtretrieved");

        // Assert
        Assert.NotNull(result);
        result.Position = 0;
        var reader1 = new StreamReader(result);
        Assert.Equal(content1, reader1.ReadToEnd());
        mockClient.Verify(c => c.DownloadFile("/remote/path/baj05778.txtretrieved", It.IsAny<Stream>()), Times.Once);
        mockClient.Verify(c => c.Connect(), Times.Once);
        mockClient.Verify(c => c.Disconnect(), Times.Once);
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
