﻿#nullable enable

using Altinn.Register.PartyImport.A2;

namespace Altinn.Register;

/// <summary>
/// Lease names for register.
/// </summary>
internal static class LeaseNames
{
    /// <summary>
    /// Lease name for <see cref="A2PartyImportJob"/>.
    /// </summary>
    internal const string A2PartyImport = "a2-party-import";

    /// <summary>
    /// Lease name for <see cref="A2PartyUserIdImportJob"/>.
    /// </summary>
    internal const string A2PartyUserIdImport = "a2-party-userid-import";
}
