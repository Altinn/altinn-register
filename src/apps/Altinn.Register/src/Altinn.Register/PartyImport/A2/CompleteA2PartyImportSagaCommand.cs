#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Represents a command that is sent to complete a <see cref="A2PartyImportSaga"/> by upserting a user in the database.
/// </summary>
public sealed record CompleteA2PartyImportSagaCommand
    : CommandBase
{
}
