using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ProblemDetails.Validation;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.PartyImport.Validation;

/// <summary>
/// Validator for parties to be imported.
/// </summary>
public readonly struct PartyForImportValidator
    : IValidator<PartyRecord, PartyRecord>
    , IValidator<PersonRecord, PersonRecord>
    , IValidator<OrganizationRecord, OrganizationRecord>
    , IValidator<SelfIdentifiedUserRecord, SelfIdentifiedUserRecord>
    , IValidator<SystemUserRecord, SystemUserRecord>
    , IValidator<EnterpriseUserRecord, EnterpriseUserRecord>
    , IValidator<PartyUserRecord, PartyUserRecord>
{
    /// <inheritdoc/>
    public bool TryValidate(
        ref ValidationContext context,
        PartyRecord input,
        [NotNullWhen(true)] out PartyRecord? validated)
    {
        validated = input;

        if (input is PersonRecord person)
        {
            TryValidate(ref context, person, out PersonRecord? _);
        }
        else if (input is OrganizationRecord org)
        {
            TryValidate(ref context, org, out OrganizationRecord? _);
        }
        else if (input is SelfIdentifiedUserRecord siu)
        {
            TryValidate(ref context, siu, out SelfIdentifiedUserRecord? _);
        }
        else if (input is SystemUserRecord sys)
        {
            TryValidate(ref context, sys, out SystemUserRecord? _);
        }
        else if (input is EnterpriseUserRecord ent)
        {
            TryValidate(ref context, ent, out EnterpriseUserRecord? _);
        }
        else
        {
            CheckCommon(ref context, input);
        }

        return !context.HasErrors;
    }

    /// <inheritdoc/>
    public bool TryValidate(
        ref ValidationContext context,
        PersonRecord input,
        [NotNullWhen(true)] out PersonRecord? validated)
    {
        validated = input;

        CheckCommon(ref context, input);

        Check(ref context, !input.Source.IsNull, ValidationErrors.Null, "/source");
        CheckRequired(ref context, input.FirstName.HasValue, "/firstName");
        CheckRequired(ref context, input.MiddleName.IsSet, "/middleName");
        CheckRequired(ref context, input.LastName.HasValue, "/lastName");
        CheckRequired(ref context, input.ShortName.HasValue, "/shortName");
        CheckRequired(ref context, input.Address.IsSet, "/address");
        CheckRequired(ref context, input.MailingAddress.IsSet, "/mailingAddress");
        CheckRequired(ref context, input.DateOfBirth.IsSet, "/dateOfBirth");
        CheckRequired(ref context, input.DateOfDeath.IsSet, "/dateOfDeath");

        return !context.HasErrors;
    }

    /// <inheritdoc/>
    public bool TryValidate(
        ref ValidationContext context,
        OrganizationRecord input,
        [NotNullWhen(true)] out OrganizationRecord? validated)
    {
        validated = input;

        CheckCommon(ref context, input);

        Check(ref context, !input.Source.IsNull, ValidationErrors.Null, "/source");
        CheckRequired(ref context, input.UnitStatus.HasValue, "/unitStatus");
        CheckRequired(ref context, input.UnitType.HasValue, "/unitType");
        CheckRequired(ref context, input.TelephoneNumber.IsSet, "/telephoneNumber");
        CheckRequired(ref context, input.MobileNumber.IsSet, "/mobileNumber");
        CheckRequired(ref context, input.FaxNumber.IsSet, "/faxNumber");
        CheckRequired(ref context, input.EmailAddress.IsSet, "/emailAddress");
        CheckRequired(ref context, input.InternetAddress.IsSet, "/internetAddress");
        CheckRequired(ref context, input.MailingAddress.IsSet, "/mailingAddress");
        CheckRequired(ref context, input.BusinessAddress.IsSet, "/businessAddress");

        return !context.HasErrors;
    }

    /// <inheritdoc/>
    public bool TryValidate(
        ref ValidationContext context,
        SelfIdentifiedUserRecord input,
        [NotNullWhen(true)] out SelfIdentifiedUserRecord? validated)
    {
        validated = input;

        CheckCommon(ref context, input);

        CheckRequired(ref context, input.SelfIdentifiedUserType.IsSet, "/selfIdentifiedUserType");

        if (input.SelfIdentifiedUserType.HasValue)
        {
            switch (input.SelfIdentifiedUserType.Value)
            {
                case SelfIdentifiedUserType.IdPortenEmail:
                    CheckRequired(ref context, input.Email.HasValue, "/email");
                    CheckNull(ref context, input.ExtRef, "/extRef");
                    break;

                case SelfIdentifiedUserType.Educational:
                    CheckRequired(ref context, input.ExtRef.HasValue, "/extRef");
                    CheckNull(ref context, input.Email, "/email");
                    break;

                default:
                    CheckNull(ref context, input.Email, "/email");
                    CheckNull(ref context, input.ExtRef, "/extRef");
                    break;
            }
        }
        else
        {
            Debug.Assert(input.SelfIdentifiedUserType.IsNull);

            CheckNull(ref context, input.Email, "/email");
            CheckNull(ref context, input.ExtRef, "/extRef");
        }

        return !context.HasErrors;
    }

    /// <inheritdoc/>
    public bool TryValidate(
        ref ValidationContext context,
        SystemUserRecord input,
        [NotNullWhen(true)] out SystemUserRecord? validated)
    {
        validated = input;

        CheckCommon(ref context, input);

        CheckRequired(ref context, input.OwnerUuid.HasValue, "/owner");
        CheckRequired(ref context, input.SystemUserType.HasValue, "/systemUserType");

        return !context.HasErrors;
    }

    /// <inheritdoc/>
    public bool TryValidate(
        ref ValidationContext context,
        EnterpriseUserRecord input,
        [NotNullWhen(true)] out EnterpriseUserRecord? validated)
    {
        validated = input;

        CheckCommon(ref context, input);

        CheckRequired(ref context, input.OwnerUuid.HasValue, "/owner");

        return !context.HasErrors;
    }

    /// <inheritdoc/>
    public bool TryValidate(
        ref ValidationContext context,
        PartyUserRecord input,
        [NotNullWhen(true)] out PartyUserRecord? validated)
    {
        validated = input;

        Check(ref context, !input.UserIds.IsNull, ValidationErrors.Null, "/user/userIds");
        if (input.UserIds.HasValue)
        {
            Check(ref context, !input.UserIds.Value.IsDefaultOrEmpty, ValidationErrors.Empty, "/user/userIds");
        }

        return !context.HasErrors;
    }

    private void CheckCommon(ref ValidationContext context, PartyRecord input)
    {
        CheckRequired(ref context, input.PartyUuid.HasValue, "/partyUuid");
        CheckRequired(ref context, input.PartyType.HasValue, "/partyType");
        CheckRequired(ref context, input.ExternalUrn.IsSet, "/externalUrn");
        CheckRequired(ref context, input.DisplayName.HasValue, "/displayName");
        CheckRequired(ref context, input.PersonIdentifier.IsSet, "/personIdentifier");
        CheckRequired(ref context, input.OrganizationIdentifier.IsSet, "/organizationIdentifier");
        CheckRequired(ref context, input.CreatedAt.HasValue, "/createdAt");
        CheckRequired(ref context, input.ModifiedAt.HasValue, "/modifiedAt");
        CheckRequired(ref context, !input.IsDeleted.IsNull, "/isDeleted");

        if (input.PartyType.HasValue)
        {
            var type = input.PartyType.Value;

            if (type is PartyRecordType.Person or PartyRecordType.Organization or PartyRecordType.SelfIdentifiedUser)
            {
                CheckRequired(ref context, input.PartyId.HasValue, "/partyId");
            }
            else
            {
                Check(ref context, input.PartyId.IsNull, ValidationErrors.NotNull, "/partyId");
            }
        }

        Check(ref context, !input.User.IsNull, ValidationErrors.Null, "/user");
        if (input.User.HasValue)
        {
            context.TryValidateChild(path: "/user", input.User.Value, this, out PartyUserRecord? _);
        }
    }

    private static void Check(ref ValidationContext context, bool condition, ValidationErrorDescriptor descriptor, string path)
    {
        if (!condition)
        {
            context.AddChildProblem(descriptor, path);
        }
    }

    private static void CheckRequired(ref ValidationContext context, bool condition, string path)
    {
        Check(ref context, condition, StdValidationErrors.Required, path);
    }

    private static void CheckNull<T>(ref ValidationContext context, FieldValue<T> value, string path)
        where T : notnull
    {
        Check(ref context, value.IsNull, ValidationErrors.NotNull, path);
    }
}
