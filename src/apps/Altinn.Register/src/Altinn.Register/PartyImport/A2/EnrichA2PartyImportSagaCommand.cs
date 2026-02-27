#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Represents a command that is sent to continue enriching a party in a <see cref="A2PartyImportSaga"/>.
/// </summary>
public sealed record EnrichA2PartyImportSagaCommand
    : CommandBase
{
}
