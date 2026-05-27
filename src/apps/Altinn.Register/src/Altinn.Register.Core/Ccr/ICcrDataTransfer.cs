using System.IO.Pipelines;

namespace Altinn.Register.Core.Ccr;

/// <summary>
/// Defines the contract for an SFTP (SSH File Transfer Protocol) client used to interact with remote file systems over
/// a secure SSH connection.
/// </summary>
/// <remarks>Implementations of this interface provide methods for connecting to a SFTP server, transferring the CCR file.</remarks>
public interface ICcrDataTransfer
{
    /// <summary>
    /// Retrieves the next file from the SFTP server based on the last processed runId.
    /// It checks for files in the specified remote path, and if a file's runId is exactly one greater than the last processed runId,
    /// it downloads the file and marks it as downloaded by renaming it. The method returns a tuple containing the filename and its content as a stream.
    /// Warning: If you give the number 5778 as last file, you will retrieve 5779. Once processing is done, you should then mark file 5779 as downloaded.
    /// </summary>
    /// <param name="writer">PipeWriter to write the file content.</param>
    /// <param name="lastRunId">Last RunId</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing the filename and its content as a stream.</returns>
    Task<bool> GetNextFileAsync(PipeWriter writer, int lastRunId, CancellationToken cancellationToken = default);
}
