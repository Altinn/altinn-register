using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ProblemDetails.Validation;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Npr;
using Altinn.Register.Core.Validation;

namespace Altinn.Register.Integrations.Npr.Feed;

/// <summary>
/// Validator for <see cref="UpdateItem"/>.
/// </summary>
public readonly struct UpdateItemValidator
    : IValidator<UpdateItem?, NprUpdate>
{
    /// <inheritdoc/>
    public bool TryValidate(
        ref ValidationContext context,
        UpdateItem? input,
        [NotNullWhen(true)] out NprUpdate? validated)
    {
        if (input is null)
        {
            context.AddProblem(StdValidationErrors.Required);
            validated = default;
            return false;
        }

        context.TryValidateChild(
            path: "/hendelse/folkeregisteridentifikator",
            input: input.UpdateInfo.PersonIdentifier,
            validator: default(PersonIdentifierValidator),
            out PersonIdentifier? personIdentifier);

        if (context.HasErrors)
        {
            validated = default;
            return false;
        }

        Debug.Assert(personIdentifier is not null);
        validated = new NprUpdate
        {
            SequenceNumber = input.SequenceNumber,
            PersonIdentifier = personIdentifier,
            UpdateTime = input.UpdateInfo.UpdateTime,
        };
        return true;
    }
}
