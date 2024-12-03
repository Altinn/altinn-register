using Altinn.Authorization.ServiceDefaults.MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// A command used to retry importing a single party from A2.
/// </summary>
public sealed record RetryImportSingleA2PartyCommand
    : Command
{
    /// <summary>
    /// Maps an <see cref="ImportA2PartyBatchCommand.Item"/> to a <see cref="RetryImportSingleA2PartyCommand"/>.
    /// </summary>
    /// <param name="command">The <see cref="ImportA2PartyBatchCommand.Item"/>.</param>
    /// <returns>A mapped <see cref="RetryImportSingleA2PartyCommand"/>.</returns>
    public static RetryImportSingleA2PartyCommand MapFrom(ImportA2PartyBatchCommand.Item command)
    {
        return new RetryImportSingleA2PartyCommand
        {
            PartyUuid = command.PartyUuid,
            ChangeId = command.ChangeId,
            ChangedTime = command.ChangedTime,
        };
    }

    /// <summary>
    /// Gets the party UUID.
    /// </summary>
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// Gets the change ID.
    /// </summary>
    public required uint ChangeId { get; init; }

    /// <summary>
    /// Gets when the change was registered.
    /// </summary>
    public required DateTimeOffset ChangedTime { get; init; }
}
