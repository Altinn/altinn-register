using Renci.SshNet;
using Renci.SshNet.Common;

namespace Altinn.Register.Integrations.Ccr.FileImport;

/// <summary>
/// Implementation of <see cref="INetworkFileSystemClient"/> that uses an <see cref="ISftpClient"/> to interact with an SFTP server.
/// </summary>
internal sealed class SftpNetworkFileSystemClient
    : INetworkFileSystemClient
{
    private readonly ISftpClient _sftpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="SftpNetworkFileSystemClient"/> class with the specified <see cref="ISftpClient"/>.
    /// </summary>
    /// <param name="sftpClient">The sftp client.</param>
    public SftpNetworkFileSystemClient(ISftpClient sftpClient)
    {
        _sftpClient = sftpClient;
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        // SftpClient.OpenAsync sends the path to the server verbatim - unlike most other methods
        // on SftpClient, it doesn't prepend WorkingDirectory for relative paths. Resolve it here
        // so callers can use relative paths against the working directory set by ChangeDirectory.
        var fullPath = Resolve(path);
        try
        {
            return await _sftpClient.OpenAsync(fullPath, FileMode.Open, FileAccess.Read, cancellationToken);
        }
        catch (SftpPathNotFoundException ex)
        {
            throw new FileNotFoundException("The specified file does not exist on the sftp server.", path, ex);
        }
    }

    private string Resolve(string path)
    {
        if (string.IsNullOrEmpty(path) || path[0] == '/')
        {
            return path;
        }

        var workingDirectory = _sftpClient.WorkingDirectory;
        if (string.IsNullOrEmpty(workingDirectory))
        {
            return path;
        }

        return workingDirectory[^1] == '/'
            ? $"{workingDirectory}{path}"
            : $"{workingDirectory}/{path}";
    }

    /// <inheritdoc/>
    public Task RenameFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        => _sftpClient.RenameFileAsync(sourcePath, destinationPath, cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _sftpClient.Dispose(); // does not support async disposal currently
        return ValueTask.CompletedTask;
    }
}
