namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// Represents the trailer record for a CCR batch, containing summary information about the batch contents.
/// </summary>
internal class CcrBatchTrailer
{
    /// <summary>
    /// Gets or sets the total number of parties included in the batch.
    /// </summary>
    public int AntallEnheter { get; set; }

    /// <summary>
    /// Gets or sets the total number of updates included in the batch.
    /// </summary>
    public required string Avsender { get; set; }
}
