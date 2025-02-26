using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Altinn.Authorization.ProblemDetails;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Altinn.Register.TestUtils.Assertions;

/// <summary>
/// Contains a number of methods to assert that a <see cref="Result{T}"/> is in the expected state.
/// </summary>
/// <typeparam name="T">The result value type.</typeparam>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
public sealed class ResultAssertions<T>(Result<T> subject)
    : CustomAssertions<Result<T>>(subject)
    where T : notnull
{
    /// <inheritdoc/>
    protected override string Identifier => "result";

    /// <summary>
    /// Asserts that the current result is in the problem state.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public AndWhichConstraint<ResultAssertions<T>, ProblemInstance> BeProblem(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsProblem)
            .WithDefaultIdentifier(Identifier)
            .FailWith("Expected {context} to be a problem{reason}, but found {0}.", Subject);

        return new(this, Subject.Problem!);
    }

    /// <summary>
    /// Asserts that the current result is in the problem state with a specific error code.
    /// </summary>
    /// <param name="errorCode">The expected problem error code.</param>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public AndWhichConstraint<ResultAssertions<T>, ProblemInstance> BeProblem(ErrorCode errorCode, string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsProblem)
            .WithDefaultIdentifier(Identifier)
            .FailWith("Expected {context} to be a problem{reason}, but found {0}.", Subject)
            .Then
            .ForCondition(Subject.Problem!.ErrorCode == errorCode)
            .FailWith("Expected {context} to be a problem with error code {1}{reason}, but found {0}.", Subject, errorCode);

        return new(this, Subject.Problem!);
    }

    /// <summary>
    /// Asserts that the current result has a value.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    [CustomAssertion]
    public AndWhichConstraint<ResultAssertions<T>, T> HaveValue(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsSuccess)
            .WithDefaultIdentifier(Identifier)
            .FailWith("Expected {context} to have a value{reason}, but found {0}.", Subject);

        return new(this, Subject.Value!);
    }
}
