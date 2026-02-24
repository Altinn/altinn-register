using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;

namespace Altinn.Register.PartyImport.A2.Enrichers;

/// <summary>
/// A party enricher that enriches parties from user information.
/// </summary>
internal sealed class A2PartyUserEnricher
    : IA2PartyImportSagaEnrichmentStep
{
    /// <inheritdoc/>
    public static string StepName
        => "a2-user";

    /// <inheritdoc/>
    public static bool CanEnrich(A2PartyImportSagaEnrichmentCheckContext context)
        => context.Party.PartyType.Value is (PartyRecordType.Person or PartyRecordType.SelfIdentifiedUser)
        && context.Party.User.IsUnset;

    private readonly IA2PartyImportService _importService;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyUserEnricher"/> class.
    /// </summary>
    public A2PartyUserEnricher(IA2PartyImportService importService, TimeProvider timeProvider)
    {
        _importService = importService;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task Run(A2PartyImportSagaEnrichmentRunContext context, CancellationToken cancellationToken)
    {
        Result<A2ProfileRecord> userRecordResult;
        if (context.Party.PartyType.Value is PartyRecordType.Person)
        {
            userRecordResult = await _importService.GetOrCreatePersonUser(context.PartyUuid, cancellationToken);
        }
        else
        {
            userRecordResult = await _importService.GetPartyUser(context.PartyUuid, cancellationToken);
        }

        userRecordResult.EnsureSuccess();
        context.Party = A2ProfileHelper.ApplyProfile(context.Party, userRecordResult.Value, _timeProvider.GetUtcNow());
    }
}
