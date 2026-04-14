using System.Text;
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
            var messageBuilder = new StringBuilder("Party validation failed. The following fields contains errors:");
            foreach (var e in error.Errors)
            {
                messageBuilder.AppendLine().Append(" - ").Append(e.Paths.FirstOrDefault()).Append(": ").Append(e.Detail);
            }

            throw new ProblemInstanceException(messageBuilder.ToString(), error);
        }
    }
}
