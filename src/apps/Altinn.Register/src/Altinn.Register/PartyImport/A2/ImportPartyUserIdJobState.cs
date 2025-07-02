#nullable enable

using Altinn.Register.Core.ImportJobs;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// State for the import job that imports user-ids from A2 for parties already imported into A3.
/// </summary>
internal class ImportPartyUserIdJobState
    : IImportJobState<ImportPartyUserIdJobState>
{
    /// <inheritdoc/>
    static string IImportJobState<ImportPartyUserIdJobState>.StateType => $"{nameof(ImportPartyUserIdJobState)}@0";
}
