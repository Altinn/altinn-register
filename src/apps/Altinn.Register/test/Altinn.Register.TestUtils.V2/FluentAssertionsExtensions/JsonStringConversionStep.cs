using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using FluentAssertions.Equivalency;

namespace Altinn.Register.TestUtils.FluentAssertionsExtensions;

[ExcludeFromCodeCoverage]
internal class JsonStringConversionStep
    : IEquivalencyStep
{
    public EquivalencyResult Handle(Comparands comparands, IEquivalencyValidationContext context, IEquivalencyValidator nestedValidator)
    {
        {
            if (comparands.Subject is string doc
                && comparands.Expectation is JsonElement)
            {
                if (TryConvert(doc, out var element))
                {
                    context.Tracer.WriteLine(member => string.Create(
                        CultureInfo.InvariantCulture,
                        $"Converted subject {comparands.Subject} at {member.Description} to JsonElement"));

                    comparands.Subject = element;
                }
                else
                {
                    context.Tracer.WriteLine(member => string.Create(
                        CultureInfo.InvariantCulture,
                        $"Subject {comparands.Subject} at {member.Description} could not be converted to JsonElement"));
                }
            }
        }

        {
            if (comparands.Expectation is string doc
                && comparands.Subject is JsonElement)
            {
                if (TryConvert(doc, out var element))
                {
                    context.Tracer.WriteLine(member => string.Create(
                        CultureInfo.InvariantCulture,
                        $"Converted expectation {comparands.Expectation} at {member.Description} to JsonElement"));

                    comparands.Expectation = element;
                }
                else
                {
                    context.Tracer.WriteLine(member => string.Create(
                        CultureInfo.InvariantCulture,
                        $"Expectation {comparands.Expectation} at {member.Description} could not be converted to JsonElement"));
                }
            }
        }

        return EquivalencyResult.ContinueWithNext;
    }

    private static bool TryConvert(string value, out JsonElement element)
    {
        try
        {
            using var doc = JsonDocument.Parse(value);
            element = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            element = default;
            return false;
        }
    }
}
