using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Contracts;

namespace Altinn.Register.PartyImport.Ccr;

/// <summary>
/// A command carrying a single organization update parsed from a CCR flat file, to be applied by
/// the <see cref="Altinn.Register.Core.Ccr.CcrService"/>.
/// </summary>
public sealed record ImportCcrPartyCommand
    : CommandBase
{
    /// <summary>
    /// Gets the identifier of the organization this command pertains to.
    /// </summary>
    public required OrganizationIdentifier OrganizationIdentifier { get; init; }

    /// <summary>
    /// Gets the CCR XML document describing the organization update, as raw bytes.
    /// </summary>
    public required byte[] Document { get; init; }
}
