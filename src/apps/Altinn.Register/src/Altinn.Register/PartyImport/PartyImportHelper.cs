#nullable enable

using System.Diagnostics;
using System.Text;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Errors;
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

        CheckRequired(ref builder, party.PartyUuid.HasValue, "/partyUuid");
        CheckRequired(ref builder, party.PartyType.HasValue, "/partyType");
        CheckRequired(ref builder, party.DisplayName.HasValue, "/name");
        CheckRequired(ref builder, party.PersonIdentifier.IsSet, "/personIdentifier");
        CheckRequired(ref builder, party.OrganizationIdentifier.IsSet, "/organizationIdentifier");
        CheckRequired(ref builder, party.CreatedAt.HasValue, "/createdAt");
        CheckRequired(ref builder, party.ModifiedAt.HasValue, "/modifiedAt");
        CheckRequired(ref builder, party.IsDeleted.HasValue, "/isDeleted");

        if (party.PartyType.HasValue)
        {
            var type = party.PartyType.Value;

            if (type is PartyRecordType.Person or PartyRecordType.Organization or PartyRecordType.SelfIdentifiedUser)
            {
                CheckRequired(ref builder, party.PartyId.HasValue, "/partyId");
            }
            else
            {
                Check(ref builder, party.PartyId.IsNull, ValidationErrors.NotNull, "/partyId");
            }
        }

        Check(ref builder, !party.User.IsNull, ValidationErrors.Null, "/user");
        if (party.User.HasValue)
        {
            CheckUser(ref builder, party.User.Value);
        }

        if (party is PersonRecord person)
        {
            CheckPerson(ref builder, person);
        }
        else if (party is OrganizationRecord org)
        {
            CheckOrganization(ref builder, org);
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

        static void Check(ref ValidationErrorBuilder builder, bool condition, ValidationErrorDescriptor descriptor, string path)
        {
            if (!condition)
            {
                builder.Add(descriptor, path);
            }
        }

        static void CheckRequired(ref ValidationErrorBuilder builder, bool condition, string path)
        {
            Check(ref builder, condition, StdValidationErrors.Required, path);
        }

        static void CheckUser(ref ValidationErrorBuilder builder, PartyUserRecord user)
        {
            Check(ref builder, !user.UserIds.IsNull, ValidationErrors.Null, "/user/userIds");
            if (user.UserIds.HasValue)
            {
                Check(ref builder, !user.UserIds.Value.IsDefaultOrEmpty, ValidationErrors.Empty, "/user/userIds");
            }
        }

        static void CheckPerson(ref ValidationErrorBuilder builder, PersonRecord person)
        {
            CheckRequired(ref builder, person.FirstName.HasValue, "/firstName");
            CheckRequired(ref builder, person.MiddleName.IsSet, "/middleName");
            CheckRequired(ref builder, person.LastName.HasValue, "/lastName");
            CheckRequired(ref builder, person.ShortName.HasValue, "/shortName");
            CheckRequired(ref builder, person.Address.IsSet, "/address");
            CheckRequired(ref builder, person.MailingAddress.IsSet, "/mailingAddress");
            CheckRequired(ref builder, person.DateOfBirth.IsSet, "/dateOfBirth");
            CheckRequired(ref builder, person.DateOfDeath.IsSet, "/dateOfDeath");
        }

        static void CheckOrganization(ref ValidationErrorBuilder builder, OrganizationRecord org)
        {
            CheckRequired(ref builder, org.UnitStatus.HasValue, "/unitStatus");
            CheckRequired(ref builder, org.UnitType.HasValue, "/unitType");
            CheckRequired(ref builder, org.TelephoneNumber.IsSet, "/telephoneNumber");
            CheckRequired(ref builder, org.MobileNumber.IsSet, "/mobileNumber");
            CheckRequired(ref builder, org.FaxNumber.IsSet, "/faxNumber");
            CheckRequired(ref builder, org.EmailAddress.IsSet, "/emailAddress");
            CheckRequired(ref builder, org.InternetAddress.IsSet, "/internetAddress");
            CheckRequired(ref builder, org.MailingAddress.IsSet, "/mailingAddress");
            CheckRequired(ref builder, org.BusinessAddress.IsSet, "/businessAddress");
        }
    }
}
