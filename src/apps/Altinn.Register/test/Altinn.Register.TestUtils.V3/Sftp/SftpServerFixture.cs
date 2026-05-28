using System.Diagnostics;
using Altinn.Register.TestUtils.Tracing;
using DotNet.Testcontainers.Containers;
using Renci.SshNet;
using Testcontainers.Sftp;

namespace Altinn.Register.TestUtils.Sftp;

/// <summary>
/// An assembly fixture that starts a single SFTP server (the <c>atmoz/sftp</c> image) in a
/// test-container, and provides helpers for uploading files to it.
/// </summary>
public sealed class SftpServerFixture
    : IAsyncLifetime
{
    /// <summary>The username used to authenticate against the SFTP server.</summary>
    public const string Username = "ccr";

    // The atmoz/sftp user is chrooted to its home directory, so the writable upload
    // directory configured below is exposed to clients at "/{UploadDirectory}".
    private const string UploadDirectory = "upload";

    private string password = Guid.NewGuid().ToString("N");

    private readonly SftpContainer _container;

    public SftpServerFixture()
    {
        var builder = new SftpBuilder("atmoz/sftp:alpine")
            .WithUsername(Username)
            .WithPassword(password)
            .WithUploadDirectory(UploadDirectory)
            .WithCleanUp(true);

        if (Debugger.IsAttached)
        {
            builder = builder.WithPortBinding(44182, SftpBuilder.SftpPort);
        }

        _container = builder.Build();
    }

    /// <summary>
    /// Allocates connection details for use by a single test, including a freshly created unique
    /// upload directory under <see cref="UploadRootPath"/> so concurrent tests sharing this server
    /// don't interfere with each other.
    /// </summary>
    /// <returns>An <see cref="SftpServerInfo"/> whose <see cref="SftpServerInfo.UploadDirectory"/>
    /// is the absolute remote path of the directory created for this caller.</returns>
    public async Task<SftpServerInfo> CreateTestServer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        if (_container.State != TestcontainersStates.Running)
        {
            await _container.StartAsync(cancellationToken);
        }

        var uploadDirectory = $"{UploadRootPath}/{Guid.NewGuid():N}";

        using var client = new SftpClient(Host, Port, Username, password);
        await client.ConnectAsync(cancellationToken);
        try
        {
            client.CreateDirectory(uploadDirectory);
        }
        finally
        {
            client.Disconnect();
        }

        return new SftpServerInfo(Host, Port, Username, password, uploadDirectory);
    }

    /// <summary>Gets the host the SFTP server is reachable on.</summary>
    public string Host => _container.Hostname;

    /// <summary>Gets the (randomly assigned) host port the SFTP server is reachable on.</summary>
    public int Port => _container.GetMappedPublicPort(SftpBuilder.SftpPort);

    /// <summary>
    /// Gets the writable root directory as seen by the chrooted SFTP user.
    /// </summary>
    public string UploadRootPath => $"/{UploadDirectory}";

    /// <summary>
    /// Uploads the given files into the upload directory of the supplied <paramref name="server"/>
    /// (typically obtained from <see cref="CreateTestServer"/>).
    /// </summary>
    /// <param name="server">The test-server info pointing at the target upload directory.</param>
    /// <param name="files">The files to upload, as <c>(name, content)</c> pairs.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    public static async Task UploadFilesAsync(
        SftpServerInfo server,
        IEnumerable<(string Name, byte[] Content)> files,
        CancellationToken cancellationToken = default)
    {
        using var client = new SftpClient(server.Host, server.Port, server.Username, server.Password);
        await client.ConnectAsync(cancellationToken);
        try
        {
            foreach (var (name, content) in files)
            {
                using var stream = new MemoryStream(content);
                client.UploadFile(stream, $"{server.UploadDirectory}/{name}");
            }
        }
        finally
        {
            client.Disconnect();
        }
    }

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        TestContext.Current.TestOutputHelper?.WriteLine("Starting SFTP container...");

        using var rootActivity = TestUtilsActivities.Source.StartActivity(ActivityKind.Internal, name: "start sftp server");
        await _container.StartAsync(TestContext.Current.CancellationToken);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        TestContext.Current.TestOutputHelper?.WriteLine("Disposing SFTP container...");

        await _container.DisposeAsync();
    }
}
