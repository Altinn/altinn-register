using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Contracts;

namespace Altinn.Register.PartyImport.Ccr;

/// <summary>
/// A command carrying a single organization update parsed from a CCR flat file, to be applied by
/// the <see cref="Altinn.Register.Core.Ccr.CcrService"/>.
/// </summary>
public sealed record ImportCcrXmlCommand
    : CommandBase
{
    /// <summary>
    /// Gets the unique identifier of the batch, if available.
    /// Is null for sync SOAP requests, but will be set for messages produced by the <see cref="CcrImportJob"/>.
    /// </summary>
    public required uint? BatchId { get; init; }

    /// <summary>
    /// Gets the identifier of the organization this command pertains to.
    /// </summary>
    public required OrganizationIdentifier OrganizationIdentifier { get; init; }

    /// <summary>
    /// Gets the CCR XML document as raw bytes.
    /// </summary>
    public required byte[] Document { get; init; }
}
