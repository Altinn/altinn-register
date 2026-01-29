using System.Diagnostics;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Saga for importing parties from A2.
/// </summary>
public partial class A2PartyImportSaga
{
    /// <inheritdoc/>
    public static ValueTask<A2PartyImportSagaData> CreateInitialState(IServiceProvider services, ImportA2PartyCommand command)
        => ValueTask.FromResult(new A2PartyImportSagaData
        {
            PartyUuid = command.PartyUuid,
            UserId = null, // we will only fetch latest user, not any specific one
            Tracking = command.Tracking,
        });

    /// <inheritdoc/>
    public async Task Handle(ImportA2PartyCommand message, CancellationToken cancellationToken)
    {
        Debug.Assert(message.PartyUuid == State.PartyUuid);

        var now = _timeProvider.GetUtcNow();

        if (await FetchParty(cancellationToken) == FlowControl.Break)
        {
            return;
        }

        await Next(now, cancellationToken);
    }
}
