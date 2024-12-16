using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Altinn.Register.TestUtils.FluentAssertionsExtensions.Json;
using FluentAssertions;
using FluentAssertions.Equivalency;
using FluentAssertions.Execution;

namespace Altinn.Register.TestUtils.FluentAssertionsExtensions;

[ExcludeFromCodeCoverage]
internal class JsonElementEquivalencyStep
    : IEquivalencyStep
{
    private const int FailedItemsFastFailThreshold = 10;

    public EquivalencyResult Handle(Comparands comparands, IEquivalencyValidationContext context, IEquivalencyValidator nestedValidator)
    {
        if (comparands.Subject is JsonElement subject
            && comparands.Expectation is JsonElement expectation)
        {
            return Handle(subject, expectation, comparands, context, nestedValidator);
        }

        return EquivalencyResult.ContinueWithNext;
    }

    private EquivalencyResult Handle(
        JsonElement subject,
        JsonElement expectation,
        Comparands comparands,
        IEquivalencyValidationContext context,
        IEquivalencyValidator nestedValidator)
    {
        AssertSameType(subject, expectation);

        switch (subject.ValueKind)
        {
            case JsonValueKind.Object:
                return HandleObject(subject, expectation, comparands, context, nestedValidator);

            case JsonValueKind.Array:
                return HandleArray(subject, expectation, comparands, context, nestedValidator);

            case JsonValueKind.String:
                return HandleString(subject, expectation, comparands, context, nestedValidator);

            case JsonValueKind.Number:
                return HandleNumber(subject, expectation, comparands, context, nestedValidator);

            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
            case JsonValueKind.True:
            case JsonValueKind.False:
                // These only require the type to be the same as they carry no data
                return EquivalencyResult.AssertionCompleted;

            default:
                throw new UnreachableException();
        }
    }

    private EquivalencyResult HandleString(
        JsonElement subject,
        JsonElement expectation,
        Comparands comparands,
        IEquivalencyValidationContext context,
        IEquivalencyValidator nestedValidator)
    {
        Debug.Assert(subject.ValueKind == JsonValueKind.String);
        Debug.Assert(expectation.ValueKind == JsonValueKind.String);

        var subjectString = subject.GetString();
        var expectationString = expectation.GetString();

        context.Tracer.WriteLine(member => string.Create(
            CultureInfo.InvariantCulture,
            $"Comparing strings at {member.Description}"));

        subjectString.Should().Be(expectationString, context.Reason.FormattedMessage, context.Reason.Arguments);
        return EquivalencyResult.AssertionCompleted;
    }

    private EquivalencyResult HandleNumber(
        JsonElement subject,
        JsonElement expectation,
        Comparands comparands,
        IEquivalencyValidationContext context,
        IEquivalencyValidator nestedValidator)
    {
        Debug.Assert(subject.ValueKind == JsonValueKind.Number);
        Debug.Assert(expectation.ValueKind == JsonValueKind.Number);

        if (subject.TryGetInt64(out var subjectI64)
            && expectation.TryGetInt64(out var expectationI64))
        {
            context.Tracer.WriteLine(member => string.Create(
                CultureInfo.InvariantCulture,
                $"Comparing Int64s at {member.Description}"));

            subjectI64.Should().Be(expectationI64, context.Reason.FormattedMessage, context.Reason.Arguments);
            return EquivalencyResult.AssertionCompleted;
        }

        if (subject.TryGetUInt64(out var subjectU64)
            && expectation.TryGetUInt64(out var expectationU64))
        {
            context.Tracer.WriteLine(member => string.Create(
                CultureInfo.InvariantCulture,
                $"Comparing UInt64s at {member.Description}"));

            subjectU64.Should().Be(expectationU64, context.Reason.FormattedMessage, context.Reason.Arguments);
            return EquivalencyResult.AssertionCompleted;
        }

        if (subject.TryGetDecimal(out var subjectDecimal)
            && expectation.TryGetDecimal(out var expectationDecimal))
        {
            context.Tracer.WriteLine(member => string.Create(
                CultureInfo.InvariantCulture,
                $"Comparing Decimals at {member.Description}"));

            subjectDecimal.Should().Be(expectationDecimal, context.Reason.FormattedMessage, context.Reason.Arguments);
            return EquivalencyResult.AssertionCompleted;
        }

        if (subject.TryGetDouble(out var subjectDouble)
            && expectation.TryGetDouble(out var expectationDouble))
        {
            context.Tracer.WriteLine(member => string.Create(
                CultureInfo.InvariantCulture,
                $"Comparing Doubles at {member.Description}"));

            subjectDouble.Should().Be(expectationDouble, context.Reason.FormattedMessage, context.Reason.Arguments);
            return EquivalencyResult.AssertionCompleted;
        }

        var subjectText = subject.GetRawText();
        var expectationText = expectation.GetRawText();

        context.Tracer.WriteLine(member => string.Create(
            CultureInfo.InvariantCulture,
            $"Comparing numbers as text at {member.Description}"));

        subjectText.Should().Be(expectationText, context.Reason.FormattedMessage, context.Reason.Arguments);
        return EquivalencyResult.AssertionCompleted;
    }

    private EquivalencyResult HandleArray(
        JsonElement subject,
        JsonElement expectation,
        Comparands comparands,
        IEquivalencyValidationContext context,
        IEquivalencyValidator parent)
    {
        JsonArrayEquivalencyValidator.Validate(subject, expectation, context, parent);
        return EquivalencyResult.AssertionCompleted;
    }

    private EquivalencyResult HandleObject(
        JsonElement subject,
        JsonElement expectation,
        Comparands comparands,
        IEquivalencyValidationContext context,
        IEquivalencyValidator parent)
    {
        JsonObjectEquivalencyValidator.Validate(subject, expectation, context, parent);
        return EquivalencyResult.AssertionCompleted;
    }

    private void AssertSameType(JsonElement subject, JsonElement expectation)
    {
        AssertionScope.Current
            .ForCondition(subject.ValueKind == expectation.ValueKind)
            .FailWith("Expected {context:subject} to be a json element with type {0}, but it has type {1}", expectation.ValueKind, subject.ValueKind);
    }

    private class JsonArrayObjectInfo
        : IObjectInfo
    {
        public static IObjectInfo Instance { get; } = new JsonArrayObjectInfo();

        public Type Type => throw new NotImplementedException();

        public Type ParentType => throw new NotImplementedException();

        public string Path { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Type CompileTimeType => throw new NotImplementedException();

        public Type RuntimeType => throw new NotImplementedException();

        private JsonArrayObjectInfo() { }
    }
}
