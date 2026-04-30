namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// Represents the header information for a CCR batch, including sender, receiver, date, and batch number details.
/// </summary>
internal class CcrBatchHeader
{
    /// <summary>
    /// Gets or sets the sender of the message.
    /// </summary>
    public required string Avsender { get; set; }

    /// <summary>
    /// Gets or sets the date value as a string.
    /// </summary>
    public required string Dato { get; set; }

    /// <summary>
    /// Gets or sets the batch number.
    /// </summary>
    public required string Kjoerenr { get; set; }

    /// <summary>
    /// Gets or sets the recipient of the message.
    /// </summary>
    public required string Mottaker { get; set; }

    /// <summary>
    /// The type of the batch
    /// </summary>
    public required string Type { get; set; }
}
