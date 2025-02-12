#nullable enable

using System.Text;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Parties.Records;

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
    public static void ValidatePartyForUpset(PartyRecord party)
    {
        ValidationErrorBuilder builder = default;

        Check(ref builder, party.PartyUuid.HasValue, "/partyUuid");
        Check(ref builder, party.PartyId.HasValue, "/partyId");
        Check(ref builder, party.PartyType.HasValue, "/partyType");
        Check(ref builder, party.DisplayName.HasValue, "/name");
        Check(ref builder, party.PersonIdentifier.IsSet, "/personIdentifier");
        Check(ref builder, party.OrganizationIdentifier.IsSet, "/organizationIdentifier");
        Check(ref builder, party.CreatedAt.HasValue, "/createdAt");
        Check(ref builder, party.ModifiedAt.HasValue, "/modifiedAt");
        Check(ref builder, party.IsDeleted.HasValue, "/isDeleted");

        if (party is PersonRecord person)
        {
            Check(ref builder, person.FirstName.HasValue, "/firstName");
            Check(ref builder, person.MiddleName.IsSet, "/middleName");
            Check(ref builder, person.LastName.HasValue, "/lastName");
            Check(ref builder, person.ShortName.HasValue, "/shortName");
            Check(ref builder, person.Address.IsSet, "/address");
            Check(ref builder, person.MailingAddress.IsSet, "/mailingAddress");
            Check(ref builder, person.DateOfBirth.HasValue, "/dateOfBirth");
            Check(ref builder, person.DateOfDeath.IsSet, "/dateOfDeath");
        }
        else if (party is OrganizationRecord org)
        {
            Check(ref builder, org.UnitStatus.HasValue, "/unitStatus");
            Check(ref builder, org.UnitType.HasValue, "/unitType");
            Check(ref builder, org.TelephoneNumber.IsSet, "/telephoneNumber");
            Check(ref builder, org.MobileNumber.IsSet, "/mobileNumber");
            Check(ref builder, org.FaxNumber.IsSet, "/faxNumber");
            Check(ref builder, org.EmailAddress.IsSet, "/emailAddress");
            Check(ref builder, org.InternetAddress.IsSet, "/internetAddress");
            Check(ref builder, org.MailingAddress.IsSet, "/mailingAddress");
            Check(ref builder, org.BusinessAddress.IsSet, "/businessAddress");
        }

        if (builder.TryBuild(out var error))
        {
            var messageBuilder = new StringBuilder("Party validation failed. The following fields contains errors:");
            foreach (var e in error.Errors)
            {
                messageBuilder.AppendLine().Append(" - ").Append(e.Paths.FirstOrDefault()).Append(": ").Append(e.Detail);
            }

            throw new ProblemInstanceException(messageBuilder.ToString(), error);
        }

        static void Check(ref ValidationErrorBuilder builder, bool condition, string path)
        {
            if (!condition)
            {
                builder.Add(StdValidationErrors.Required, path);
            }
        }
    }
}
