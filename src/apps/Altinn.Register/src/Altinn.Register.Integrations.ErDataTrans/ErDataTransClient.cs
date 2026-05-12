using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace Altinn.Register.Integrations.ErDataTrans;

/// <summary>
/// Client for retrieving SFTP files from BRG.
/// </summary>
internal sealed class ErDataTransClient
{
    private readonly string _remotePath;
    private readonly ISftpClient _client;

    /// <summary>
    /// Implementation of <see cref="ErDataTransClient"/>.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ErDataTransClient"/> class.
    /// </remarks>
    /// <param name="remotePath">Remote path to access.</param>
    /// <param name="host">Host to connect to.</param>
    /// <param name="password">Password for authentication.</param>
    /// <param name="user">Username for authentication.</param>
    public ErDataTransClient(string user, string password, string host, string remotePath)
    {
        _remotePath = remotePath;
        _client = new SftpClient(host, user, password);
    }

    /// <summary>
    /// Constructor for testing purposes, allows injection of a mock SftpClient.
    /// </summary>
    /// <param name="client">Test Client</param>
    /// <param name="remotePath">Remote path to access.</param>
    public ErDataTransClient(string remotePath, ISftpClient client)
    {
        _client = client;
        _remotePath = remotePath;
    }

    /// <summary>
    /// Retrieves new files from the SFTP server based on the last processed runId. It checks for files in the specified remote path, and if a file's runId is exactly one greater than the last processed runId, it downloads the file and marks it as downloaded by renaming it. The method returns a list of tuples containing the filename and its content as a stream.
    /// </summary>
    /// <param name="lastRunId">Last RunId</param>
    /// <returns>List of tuples containing the filename and its content as a stream.</returns>
    public List<Tuple<string, Stream>> GetNewFiles(int lastRunId = -1)
    {
        if (!_client.IsConnected)
        {
            _client.Connect();
        }

        try
        {
            int lastProcessedRunId = lastRunId;
            List<Tuple<string, Stream>> streams = new();
            IEnumerable<ISftpFile> files = _client.ListDirectory(_remotePath).Where(f => !f.IsDirectory && !f.IsSymbolicLink);
            foreach (var file in files.OrderBy(f => f.Name))
            {
                if (file.Name.Contains("Downloaded", StringComparison.InvariantCultureIgnoreCase) || !file.Name.StartsWith("baj", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                int runId = ErDataTransClient.GetRunIdFromFileName(file.Name);
                if (!file.FullName.Contains("Downloaded")
                    && (lastRunId == -1 || runId == lastProcessedRunId + 1))
                {
                    Stream stream = new MemoryStream();
                    _client.DownloadFile(file.FullName, stream);
                    stream.Position = 0; // Reset stream position after download
                    streams.Add(new Tuple<string, Stream>(file.Name, stream));
                    _client.RenameFile(file.FullName, file.FullName.Replace(".txt", "Downloaded.txt")); // Mark file as downloaded
                    lastProcessedRunId = runId;
                }
            }

            return streams;
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
    /// <returns>Stream containing the file content.</returns>
    public Stream GetSpecificFile(string filename)
    {
        if (!_client.IsConnected)
        {
            _client.Connect();
        }

        try
        {
            string fullPath = $"{_remotePath}/{filename}";
            Stream stream = new MemoryStream();
            _client.DownloadFile(fullPath, stream);
            stream.Position = 0; // Reset stream position after download
            return stream;
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
