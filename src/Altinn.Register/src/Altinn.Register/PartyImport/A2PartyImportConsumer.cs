using MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Consumer for importing parties from A2.
/// </summary>
public sealed class A2PartyImportConsumer
    : IConsumer<ImportA2PartyCommand>
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<ImportA2PartyCommand> context)
    {
        var urn = MessageUrn.ForType<ImportA2PartyCommand>();
        return Task.CompletedTask;
    }
}
