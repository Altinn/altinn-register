using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FluentAssertions.Execution;

namespace Altinn.Register.TestUtils.FluentAssertionsExtensions.Json;

[ExcludeFromCodeCoverage]
internal static class JsonElementEquivalencyStepHelperExtensions
{
    public static Continuation AssertEitherArrayIsNotEmpty(
        this IAssertionScope scope,
        JsonElement subject,
        JsonElement expectation,
        int subjectLength,
        int expectationLength)
    {
        return scope
            .ForCondition(subjectLength > 0 || expectationLength == 0)
            .FailWith(", but found an empty array.")
            .Then
            .ForCondition(subjectLength == 0 || expectationLength > 0)
            .FailWith($", but {{0}}{Environment.NewLine}contains {{1}} item(s).", subject, subjectLength);
    }

    public static Continuation AssertArrayHasEnoughItems(
        this IAssertionScope scope,
        JsonElement subject,
        JsonElement expectation,
        int subjectLength,
        int expectationLength)
    {
        return scope
            .ForCondition(subjectLength >= expectationLength)
            .FailWith(
                $", but {{0}}{Environment.NewLine}contains {{1}} item(s) less than{Environment.NewLine}{{2}}.",
                subject,
                expectationLength - subjectLength,
                expectation);
    }

    public static Continuation AssertArrayHasNotTooManyItems(
        this IAssertionScope scope,
        JsonElement subject,
        JsonElement expectation,
        int subjectLength,
        int expectationLength)
    {
        return scope
            .ForCondition(subjectLength <= expectationLength)
            .FailWith(
                $", but {{0}}{Environment.NewLine}contains {{1}} item(s) more than{Environment.NewLine}{{2}}.",
                subject,
                subjectLength - expectationLength,
                expectation);
    }
}
