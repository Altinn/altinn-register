#nullable enable

namespace Altinn.Register;

/// <summary>
/// Job names for register.
/// </summary>
internal static class JobNames
{
    /// <summary>
    /// Job name for A2 party import-party job.
    /// </summary>
    internal const string A2PartyImportParty = $"{LeaseNames.A2PartyImport}:party";

    /// <summary>
    /// Job name for A2 party import-external-roles job.
    /// </summary>
    internal const string A2PartyImportCCRRoleAssignments = $"{LeaseNames.A2PartyImport}:ccr-roles";

    /// <summary>
    /// Job name for A2 party import-userid job.
    /// </summary>
    internal const string A2PartyUserIdImport = $"{LeaseNames.A2PartyUserIdImport}:userid";

    /// <summary>
    /// Job name for A2 profile-changes import job.
    /// </summary>
    internal const string A2ProfileChangesImport = $"{LeaseNames.A2ProfileImport}:profile-changes";

    /// <summary>
    /// Job name for system user import job.
    /// </summary>
    internal const string SystemUserImport = $"{LeaseNames.SystemUserImport}:systemuser";
}
