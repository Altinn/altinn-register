using System.IO.Pipelines;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace Altinn.Register.Integrations.Ccr.FileImport;

/// <summary>
/// Client for retrieving SFTP files from BRG.
/// </summary>
internal sealed class CcrDataTransfer
{
    private readonly string _remotePath;
    private readonly ISftpClient _client;

    /// <summary>
    /// Implementation of <see cref="CcrDataTransfer"/>.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CcrDataTransfer"/> class.
    /// </remarks>
    /// <param name="remotePath">Remote path to access.</param>
    /// <param name="host">Host to connect to.</param>
    /// <param name="password">Password for authentication.</param>
    /// <param name="user">Username for authentication.</param>
    /// <param name="port">Port to connect to, default is 22.</param>
    /// <param name="timeoutSeconds">Timeout in seconds for connection attempts, default is 30 seconds.</param>
    public CcrDataTransfer(string user, string password, string host, string remotePath, int port = 22, int timeoutSeconds = 30)
    {
        _remotePath = remotePath;
        _client = new SftpClient(host, port, user, password);
        _client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(timeoutSeconds); // Set a timeout for connection attempts 
    }

    /// <summary>
    /// Constructor for testing purposes, allows injection of a mock SftpClient.
    /// </summary>
    /// <param name="client">Test Client</param>
    /// <param name="remotePath">Remote path to access.</param>
    public CcrDataTransfer(string remotePath, ISftpClient client)
    {
        _client = client;
        _remotePath = remotePath;
    }

    /// <summary>
    /// Retrieves the next file from the SFTP server based on the last processed runId. It checks for files in the specified remote path, and if a file's runId is exactly one greater than the last processed runId, it downloads the file and marks it as downloaded by renaming it. The method returns a tuple containing the filename and its content as a stream.
    /// </summary>
    /// <param name="writer">PipeWriter to write the file content.</param>
    /// <param name="lastRunId">Last RunId</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing the filename and its content as a stream.</returns>
    public async Task<bool> GetNextFileAsync(PipeWriter writer, int lastRunId, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            await _client.ConnectAsync(cancellationToken);
        }

        try
        {
            IAsyncEnumerable<ISftpFile> files = _client.ListDirectoryAsync(_remotePath, cancellationToken).Where(f => !f.IsDirectory && !f.IsSymbolicLink);
            await foreach (var file in files.OrderBy(f => f.Name))
            {
                if (file.Name.Contains("Downloaded", StringComparison.InvariantCultureIgnoreCase) || !file.Name.StartsWith("baj", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                int runId = GetRunIdFromFileName(file.Name);
                if (lastRunId == -1 || runId == lastRunId + 1)
                {
                    Stream stream = writer.AsStream();
                    await _client.DownloadFileAsync(file.FullName, stream, cancellationToken); // Download file content to the provided stream
                    await writer.FlushAsync(cancellationToken); // Ensure all data is flushed before disconnecting
                    await writer.CompleteAsync(); // Complete the writer to signal no more data will be written
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            await writer.CompleteAsync(ex); // Complete the writer with an exception if an error occurs
            throw;
        }
        finally
        {
            _client.Disconnect();
        }
    }

    /// <summary>
    /// Marks a file as downloaded by renaming it on the SFTP server. It connects to the SFTP server, checks for files in the specified remote path, and if a file's runId is exactly one greater than the last processed runId, it renames the file by replacing ".txt" with "Downloaded.txt". After processing, it disconnects from the SFTP server. The method returns true if a file was marked as downloaded, otherwise false.
    /// </summary>
    /// <param name="lastRun">The last processed runId. Used to identify which file should be renamed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<bool> MarkFileAsDownloadedAsync(int lastRun, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            await _client.ConnectAsync(cancellationToken);
        }

        try
        {
            IAsyncEnumerable<ISftpFile> files = _client.ListDirectoryAsync(_remotePath, cancellationToken).Where(f => !f.IsDirectory && !f.IsSymbolicLink);
            await foreach (var file in files)
            {
                if (file.Name.Contains("Downloaded", StringComparison.InvariantCultureIgnoreCase) || !file.Name.StartsWith("baj", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                int runId = GetRunIdFromFileName(file.Name);
                if (runId == lastRun + 1)
                {
                    await _client.RenameFileAsync(file.FullName, file.FullName.Replace(".txt", "Downloaded.txt"), cancellationToken); // Mark file as downloaded
                    return true;
                }
            }

            return false;
        }
        finally
        {
            _client.Disconnect();
        }
    }

    /// <summary>
    /// Retrieves a specific file from the SFTP server based on the provided filename. It downloads the file content into a stream and returns it. After the download is complete, it disconnects from the SFTP server. The method assumes that the filename provided is the full path to the file on the SFTP server.
    /// </summary>
    /// <param name="filename">Filename to retrieve. Note: not the full path.</param>
    /// <param name="writer">PipeWriter to write the file content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream containing the file content.</returns>
    public async Task<bool> GetSpecificFileAsync(string filename, PipeWriter writer, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            await _client.ConnectAsync(cancellationToken);
        }

        try
        {
            string fullPath = $"{_remotePath}/{filename}";
            await _client.DownloadFileAsync(fullPath, writer.AsStream(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            await writer.CompleteAsync();
            return true;
        }
        finally
        {
            _client.Disconnect();
        }
    }

    /// <summary>
    /// Extracts the runId from the filename. Assumes filename format is always "bajXXXXX.txt" where XXXXX is the runId.
    /// </summary>
    /// <param name="filename">Filename to get runid for.</param>
    /// <returns>Id of the running number of the file.</returns>
    /// <exception cref="FormatException">Unable to retrieve runId from filename.</exception>
    private static int GetRunIdFromFileName(string filename)
    {
        // Extract runId from filename, e.g., "baj05778.txt" -> 5778
        int bajIndex = filename.IndexOf("baj");
        int dotIndex = filename.IndexOf("Downloaded.txt");
        if (dotIndex == -1)
        {
            dotIndex = filename.IndexOf(".txt");
        }

        if (bajIndex < 0 || dotIndex < 0 || dotIndex <= bajIndex)
        {
            throw new FormatException($"Filename {filename} does not contain a valid runId.");
        }

        string runIdPart = filename[(bajIndex + 3)..dotIndex]; // Assuming filename format is always "bajXXXXX.txt"
        if (int.TryParse(runIdPart, out int runId))
        {
            return runId;
        }
        else
        {
            throw new FormatException($"Filename {filename} does not contain a valid runId.");
        }
    }
}
