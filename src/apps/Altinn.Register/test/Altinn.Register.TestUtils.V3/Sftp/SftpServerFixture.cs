using System.Diagnostics;
using Altinn.Register.TestUtils.Tracing;
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

    /// <summary>The password used to authenticate against the SFTP server.</summary>
    public const string Password = "ccr-password";

    // The atmoz/sftp user is chrooted to its home directory, so the writable upload
    // directory configured below is exposed to clients at "/{UploadDirectory}".
    private const string UploadDirectory = "upload";

    private readonly SftpContainer _container;

    public SftpServerFixture()
    {
        var builder = new SftpBuilder("atmoz/sftp:alpine")
            .WithUsername(Username)
            .WithPassword(Password)
            .WithUploadDirectory(UploadDirectory)
            .WithCleanUp(true);

        if (Debugger.IsAttached)
        {
            builder = builder.WithPortBinding(44182, SftpBuilder.SftpPort);
        }

        _container = builder.Build();
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
    /// Uploads the given files to a fresh, uniquely named directory under <see cref="UploadRootPath"/>,
    /// so that tests sharing this server don't interfere with each other.
    /// </summary>
    /// <param name="files">The files to upload, as <c>(name, content)</c> pairs.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The absolute remote path of the directory the files were uploaded to.</returns>
    public async Task<string> UploadToNewDirectoryAsync(
        IEnumerable<(string Name, byte[] Content)> files,
        CancellationToken cancellationToken = default)
    {
        var remoteDir = $"{UploadRootPath}/{Guid.NewGuid():N}";

        using var client = new SftpClient(Host, Port, Username, Password);
        await client.ConnectAsync(cancellationToken);
        try
        {
            client.CreateDirectory(remoteDir);
            foreach (var (name, content) in files)
            {
                using var stream = new MemoryStream(content);
                client.UploadFile(stream, $"{remoteDir}/{name}");
            }
        }
        finally
        {
            client.Disconnect();
        }

        return remoteDir;
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
