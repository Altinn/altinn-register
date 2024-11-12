namespace Altinn.Register;

/// <summary>
/// Job names for register.
/// </summary>
internal static class Jobs
{
    /// <summary>
    /// Job name for A2 party import-party job.
    /// </summary>
    internal const string A2PartyImportParty = $"{Leases.A2PartyImport}:party";

    /// <summary>
    /// Job name for A2 party import-role job.
    /// </summary>
    internal const string A2PartyImportRole = $"{Leases.A2PartyImport}:role";
}
