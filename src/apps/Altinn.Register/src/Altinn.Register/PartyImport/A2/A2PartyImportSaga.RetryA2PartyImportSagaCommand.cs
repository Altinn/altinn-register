namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Saga for importing parties from A2.
/// </summary>
public partial class A2PartyImportSaga
{
    /// <inheritdoc/>
    public async Task Handle(RetryA2PartyImportSagaCommand message, CancellationToken cancellationToken)
    {
        if (State.PartyUuid == Guid.Empty)
        {
            throw new InvalidOperationException("PartyUuid is not set");
        }

        var now = _timeProvider.GetUtcNow();

        State.Clear();
        if (await FetchParty(cancellationToken) == FlowControl.Break)
        {
            return;
        }

        await Next(now, cancellationToken);
    }
}
