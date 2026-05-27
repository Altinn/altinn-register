namespace Altinn.Register.IntegrationTests.PartyImport.Ccr;

/// <summary>
/// Helpers for reading CCR flat-file test data shipped in <c>Testdata/Ccr/FlatFile</c>.
/// </summary>
internal static class CcrFlatFileTestData
{
    /// <summary>
    /// Reads the raw bytes of a CCR flat-file test data file. The bytes are returned exactly as
    /// stored on disk (the files are fixed-width and Latin-9 encoded), so they're suitable for
    /// uploading verbatim to the SFTP test server.
    /// </summary>
    /// <param name="fileName">The file name, e.g. <c>baj00001.txt</c>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    public static Task<byte[]> ReadBytesAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Testdata", "Ccr", "FlatFile", fileName);
        return File.ReadAllBytesAsync(path, cancellationToken);
    }
}
