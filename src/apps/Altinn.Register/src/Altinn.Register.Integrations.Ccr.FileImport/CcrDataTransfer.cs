using System.IO.Pipelines;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Ccr;
using Altinn.Register.Core.ImportJobs.FileProcessing;

namespace Altinn.Register.Integrations.Ccr.FileImport;

/// <summary>
/// Client for retrieving SFTP files from BRG.
/// </summary>
internal sealed class CcrDataTransfer
    : ICcrFlatFileService
{
    private readonly INetworkFileSystemClientFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CcrDataTransfer"/>.
    /// </summary>
    public CcrDataTransfer(INetworkFileSystemClientFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc/>
    public async Task<Result<CcrFlatFileOperationResult>> ProcessNextFile(
        IFileProcessor<CcrOpenedFileInfo> processor,
        uint lastRunId,
        CancellationToken cancellationToken = default)
    {
        await using var client = await _factory.Connect(nameof(ICcrFlatFileService), cancellationToken);

        try
        {
            var runId = checked(lastRunId + 1);
            var nextFile = FileName(runId);

            {
                await using var fs = await client.OpenReadAsync(nextFile, cancellationToken);
                var reader = PipeReader.Create(fs);
                var fileInfo = new CcrOpenedFileInfo(nextFile, reader, runId);

                await processor.ProcessFileAsync(fileInfo, cancellationToken);
            }

            await client.RenameFileAsync(nextFile, $"{nextFile}retrieved", cancellationToken);

            return CcrFlatFileOperationResult.FileProcessed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            if (await Exists(client, FileName(lastRunId + 2), cancellationToken))
            {
                // If the file with the next runId + 1 does not exist, but the one after that does, it means we have missed a file - that's an error condition
                throw new InvalidOperationException($"The expected CCR file '{FileName(lastRunId + 1)}' was not found, but a file with a higher runId was found. This indicates a missing file and potential data loss.");
            }

            return CcrFlatFileOperationResult.NoFileToProcess;
        }
        catch (Exception)
        {
            // TODO: handle better
            throw;
        }
    }

    private async Task<bool> Exists(INetworkFileSystemClient client, string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await client.OpenReadAsync(path, cancellationToken);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private static string FileName(uint runId) => $"baj{runId:D5}.txt";
}
