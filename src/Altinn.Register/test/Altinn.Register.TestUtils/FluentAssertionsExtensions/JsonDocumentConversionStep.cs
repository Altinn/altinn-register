using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using FluentAssertions.Equivalency;

namespace Altinn.Register.TestUtils.FluentAssertionsExtensions;

[ExcludeFromCodeCoverage]
internal class JsonDocumentConversionStep
    : IEquivalencyStep
{
    public EquivalencyResult Handle(Comparands comparands, IEquivalencyValidationContext context, IEquivalencyValidator nestedValidator)
    {
        {
            if (comparands.Subject is JsonDocument doc)
            {
                context.Tracer.WriteLine(member => string.Create(
                    CultureInfo.InvariantCulture,
                    $"Converted subject {comparands.Subject} at {member.Description} to JsonElement"));

                comparands.Subject = doc.RootElement;
            }
        }

        {
            if (comparands.Expectation is JsonDocument doc)
            {
                context.Tracer.WriteLine(member => string.Create(
                    CultureInfo.InvariantCulture,
                    $"Converted expectation {comparands.Expectation} at {member.Description} to JsonElement"));

                comparands.Expectation = doc.RootElement;
            }
        }

        return EquivalencyResult.ContinueWithNext;
    }
}
