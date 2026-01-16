#nullable enable

using System.Diagnostics.CodeAnalysis;
using Altinn.Register.Contracts.ExternalRoles;

namespace Altinn.Register.PartyImport.Npr;

/// <summary>Mappings of guardianship values from npr to Altinn Register.</summary>
internal partial class GuardianshipRoleMapper
{
    /// <summary>Tries to find a guardianship role by NPR values.</summary>
    /// <param name="vergeTjenestevirksomhet">The NPR value for the guardianship area.</param>
    /// <param name="vergeTjenesteoppgave">The NPR value for the guardianship task.</param>
    /// <param name="role">The found role, if any.</param>
    /// <returns><see langword="true"/> if a role was found; otherwise, <see langword="false"/>.</returns>
    public static bool TryFindRoleByNprValues(
        string vergeTjenestevirksomhet,
        string vergeTjenesteoppgave,
        [NotNullWhen(true)] out ExternalRoleReference? role)
        => TryFindRoleByNprValues(vergeTjenestevirksomhet.AsSpan(), vergeTjenesteoppgave.AsSpan(), out role);
}
