#nullable enable

using Altinn.Register.Contracts.Events;
using Altinn.Register.Core.UnitOfWork;
using MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Consumer for upserting parties from different sources.
/// </summary>
public sealed class PartyImportConsumer
    : IConsumer<UpsertPartyCommand>
{
    private readonly IUnitOfWorkManager _uow;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyImportConsumer"/> class.
    /// </summary>
    public PartyImportConsumer(IUnitOfWorkManager uow)
    {
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<UpsertPartyCommand> context)
    {
        await using var uow = await _uow.CreateAsync(cancellationToken: context.CancellationToken);
        var persistence = uow.GetPartyPersistence();
        var result = await persistence.UpsertParty(context.Message.Party, context.CancellationToken);
        result.EnsureSuccess();

        await uow.CommitAsync(context.CancellationToken);

        var partyUpdatedEvt = new PartyUpdatedEvent
        {
            PartyUuid = result.Value.PartyUuid.Value,
        };

        await context.Publish(partyUpdatedEvt, context.CancellationToken);
    }
}
