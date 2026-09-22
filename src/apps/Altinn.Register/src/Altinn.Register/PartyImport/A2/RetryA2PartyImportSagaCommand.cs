using Altinn.Authorization.ServiceDefaults.MassTransit;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Represents a command that is sent to retry a <see cref="A2PartyImportSaga"/>. Only issued by dev-tooling.
/// </summary>
public sealed record RetryA2PartyImportSagaCommand
    : CommandBase
{
    /// <summary>
    /// Gets the unique identifier for the party.
    /// </summary>
    public required ImportPartyIdentifier PartyIdentifier { get; init; }
}
