using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using FluentAssertions.Equivalency;
using FluentAssertions.Execution;

namespace Altinn.Register.TestUtils.FluentAssertionsExtensions.Json;

[ExcludeFromCodeCoverage]
internal ref struct JsonObjectEquivalencyValidator
{
    public static void Validate(
        JsonElement subject,
        JsonElement expectation,
        IEquivalencyValidationContext context,
        IEquivalencyValidator parent)
    {
        Debug.Assert(subject.ValueKind == JsonValueKind.Object);
        Debug.Assert(expectation.ValueKind == JsonValueKind.Object);

        JsonProperty[]? subjectProperties = null;
        JsonProperty[]? expectationProperties = null;

        try
        {
            subjectProperties = ArrayPool<JsonProperty>.Shared.Rent(4);
            expectationProperties = ArrayPool<JsonProperty>.Shared.Rent(4);

            PopulateProperties(ref subjectProperties, subject.EnumerateObject(), out var subjectLength);
            PopulateProperties(ref expectationProperties, expectation.EnumerateObject(), out var expectationLength);

            AssertSameLength(subject, expectation, subjectLength, expectationLength);

            var validator = new JsonObjectEquivalencyValidator(
                subjectProperties.AsSpan(0, subjectLength),
                expectationProperties.AsSpan(0, expectationLength),
                context,
                parent);

            validator.Validate();
        }
        finally
        {
            if (subjectProperties is not null)
            {
                ArrayPool<JsonProperty>.Shared.Return(subjectProperties);
            }

            if (expectationProperties is not null)
            {
                ArrayPool<JsonProperty>.Shared.Return(expectationProperties);
            }
        }

        static void PopulateProperties(ref JsonProperty[] properties, JsonElement.ObjectEnumerator enumerator, out int length)
        {
            length = 0;
            foreach (var property in enumerator)
            {
                if (length == properties.Length)
                {
                    var grown = ArrayPool<JsonProperty>.Shared.Rent(properties.Length * 2);
                    try
                    {
                        properties.CopyTo(grown, 0);
                        (properties, grown) = (grown, properties);
                    }
                    finally
                    {
                        ArrayPool<JsonProperty>.Shared.Return(grown);
                    }
                }

                properties[length++] = property;
            }
        }
    }

    private static void AssertSameLength(JsonElement subject, JsonElement expectation, int subjectLength, int expectationLength)
    {
        AssertionScope.Current
            .ForCondition(subjectLength == expectationLength)
            .FailWith(
                "Expected {context:subject} to be a json object with {0} item(s) ({2}), but it only contains {1} item(s) ({3}).",
                expectationLength,
                subjectLength,
                expectation,
                subject);
    }

    private readonly ReadOnlySpan<JsonProperty> _subjects;
    private readonly ReadOnlySpan<JsonProperty> _expectations;
    private readonly IEquivalencyValidationContext _context;
    private readonly IEquivalencyValidator _parent;

    private JsonObjectEquivalencyValidator(
        Span<JsonProperty> subjects,
        Span<JsonProperty> expectations,
        IEquivalencyValidationContext context,
        IEquivalencyValidator parent)
    {
        _subjects = subjects;
        _expectations = expectations;
        _context = context;
        _parent = parent;
    }

    private void Validate()
    {
        foreach (var expectation in _expectations)
        {
            var subject = FindSubject(expectation);

            _context.Tracer.WriteLine(member => string.Create(
                CultureInfo.InvariantCulture,
                $"Recursing into dictionary item {expectation.Name} at {member.Description}"));

            _parent.RecursivelyAssertEquality(
                new Comparands(subject, expectation.Value, typeof(JsonElement)),
                _context.AsDictionaryItem<string, JsonElement>(expectation.Name));
        }
    }

    private JsonElement FindSubject(JsonProperty property)
    {
        foreach (var subject in _subjects)
        {
            if (subject.NameEquals(property.Name))
            {
                return subject.Value;
            }
        }

        AssertionScope.Current
            .FailWith("Expected {context:subject} to contain a property with name {0}, but it did not.", property.Name);
        throw new UnreachableException();
    }
}
