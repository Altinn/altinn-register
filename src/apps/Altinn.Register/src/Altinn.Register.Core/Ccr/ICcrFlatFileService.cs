using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.ImportJobs.FileProcessing;

namespace Altinn.Register.Core.Ccr;

/// <summary>
/// Defines a service for processing CCR flat files, including retrieving the next file to process and invoking a provided processor to process the file.
/// </summary>
public interface ICcrFlatFileService
{
    /// <summary>
    /// Processes the next CCR flat file using the provided <paramref name="processor"/>.
    /// The <paramref name="lastRunId"/> is used to determine which file
    /// to process next based on the sequence number of the CCR files.
    /// </summary>
    /// <param name="processor">A <see cref="IFileProcessor{T}"/> that will be used to process the CCR flat file.</param>
    /// <param name="lastRunId">The ID of the last run, used to determine which file to process next.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="Result{T}"/> containing the outcome of the operation.</returns>
    public Task<Result<CcrFlatFileOperationResult>> ProcessNextFile(
        IFileProcessor<CcrOpenedFileInfo> processor,
        uint lastRunId,
        CancellationToken cancellationToken = default);
}
