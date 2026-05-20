namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Saga for importing parties from A2.
/// </summary>
public partial class A2PartyImportSaga
{
    /// <inheritdoc/>
    public async Task Handle(RetryA2PartyImportSagaCommand message, CancellationToken cancellationToken)
    {
        if (!State.PartyIdentifier.HasValue)
        {
            throw new InvalidOperationException("PartyIdentifier is not set");
        }

        State.Clear();
        if (await FetchPartyFromA2(cancellationToken) == FlowControl.Break)
        {
            return;
        }

        await Enrich(cancellationToken);
    }
}
