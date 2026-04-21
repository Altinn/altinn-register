using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ProblemDetails.Validation;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.PartyImport.Validation;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Helpers for importing parties.
/// </summary>
public static class PartyImportHelper
{
    /// <summary>
    /// Validates that a party is valid for upserting.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <exception cref="ProblemInstanceException">Thrown if the party is not valid.</exception>
    public static void ValidatePartyForUpsert(PartyRecord party)
    {
        ValidationProblemBuilder builder = default;

        builder.TryValidate(path: "/", party, default(PartyForImportValidator), out PartyRecord? _);

        if (builder.TryBuild(out var error))
        {
            throw new ProblemInstanceException(error);
        }
    }
}
