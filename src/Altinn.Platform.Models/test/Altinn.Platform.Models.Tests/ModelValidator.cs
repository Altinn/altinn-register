using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Platform.Models.Tests
{
    /// <summary>
    /// Helper methods for model validation.
    /// </summary>
    public static class ModelValidator
    {
        /// <summary>
        /// Perform validation of an instance of a model.
        /// </summary>
        /// <param name="model">The model to validate.</param>
        /// <returns>A list of identified issues.</returns>
        public static IReadOnlyList<ValidationResult> ValidateModel(object model)
        {
            List<ValidationResult> validationResults = [];
            ValidationContext ctx = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, ctx, validationResults, true);
            return validationResults;
        }
    }
}
