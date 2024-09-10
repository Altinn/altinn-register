using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Altinn.Register.Core.Utils;
using CommunityToolkit.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Altinn.Register.TestUtils.Assertions;

/// <summary>
/// Contains a number of methods to assert that a <see cref="FieldValue{T}"/> is in the expected state.
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
public class FieldValueAssertions<T>
    where T : notnull
{
    private static readonly string Identifier = "field value";

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldValueAssertions{T}"/> class.
    /// </summary>
    /// <param name="subject">The subject to assert on.</param>
    public FieldValueAssertions(FieldValue<T> subject)
    {
        Subject = subject;
    }

    /// <summary>
    /// Gets the field value which is being asserted.
    /// </summary>
    public FieldValue<T> Subject { get; }

    /// <summary>
    /// Asserts that the current field value is in the null state.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    [CustomAssertion]
    public AndConstraint<FieldValueAssertions<T>> BeNull(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .ForCondition(Subject.IsNull)
            .BecauseOf(because, becauseArgs)
            .WithDefaultIdentifier(Identifier)
            .FailWith("Expected {context} to be <null>{reason}, but found {0}.", Subject);

        return new(this);
    }

    /// <summary>
    /// Asserts that the current field value is not in the null state.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    [CustomAssertion]
    public AndConstraint<FieldValueAssertions<T>> NotBeNull(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .ForCondition(!Subject.IsNull)
            .BecauseOf(because, becauseArgs)
            .WithDefaultIdentifier(Identifier)
            .FailWith("Expected {context} not to be <null>{reason}.", Subject);

        return new(this);
    }

    /// <summary>
    /// Asserts that the current field value is in the unset state.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    [CustomAssertion]
    public AndConstraint<FieldValueAssertions<T>> BeUnset(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .ForCondition(Subject.IsUnset)
            .BecauseOf(because, becauseArgs)
            .WithDefaultIdentifier(Identifier)
            .FailWith("Expected {context} to be <unset>{reason}, but found {0}.", Subject);

        return new(this);
    }

    /// <summary>
    /// Asserts that the current field value is not in the unset state.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    [CustomAssertion]
    public AndConstraint<FieldValueAssertions<T>> NotBeUnset(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .ForCondition(!Subject.IsNull)
            .BecauseOf(because, becauseArgs)
            .WithDefaultIdentifier(Identifier)
            .FailWith("Expected {context} not to be <unset>{reason}.", Subject);

        return new(this);
    }

    /// <summary>
    /// Asserts that a value equals <paramref name="expected"/> using the provided <paramref name="comparer"/>.
    /// </summary>
    /// <param name="expected">The expected value</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is <see langword="null"/>.</exception>
    [CustomAssertion]
    public AndConstraint<FieldValueAssertions<T>> Be(FieldValue<T> expected, IEqualityComparer<FieldValue<T>> comparer, string because = "", params object[] becauseArgs)
    {
        Guard.IsNotNull(comparer);

        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(comparer.Equals(Subject, expected))
            .WithDefaultIdentifier(Identifier)
            .FailWith("Expected {context} to be {0}{reason}, but found {1}.", expected, Subject);

        return new(this);
    }

    /// <summary>
    /// Asserts that a value equals <paramref name="expected"/> using the provided <paramref name="comparer"/>.
    /// </summary>
    /// <param name="expected">The expected value</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is <see langword="null"/>.</exception>
    [CustomAssertion]
    public AndConstraint<FieldValueAssertions<T>> Be(FieldValue<T> expected, IEqualityComparer<T> comparer, string because = "", params object[] becauseArgs)
        => Be(expected, new FieldValueEqualityComparer(comparer), because, becauseArgs);

    /// <summary>
    /// Asserts that a value equals <paramref name="expected"/>.
    /// </summary>
    /// <param name="expected">The expected value</param>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    [CustomAssertion]
    public AndConstraint<FieldValueAssertions<T>> Be(FieldValue<T> expected, string because = "", params object[] becauseArgs)
        => Be(expected, FieldValueEqualityComparer.Default, because, becauseArgs);

    /// <summary>
    /// Asserts that the current field value has a value.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    [CustomAssertion]
    public AndWhichConstraint<FieldValueAssertions<T>, T> HaveValue(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.HasValue)
            .WithDefaultIdentifier(Identifier)
            .FailWith("Expected {context} to have a value{reason}, but found {0}.", Subject);

        return new(this, Subject.Value!);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        throw new NotSupportedException("Equals is not part of Fluent Assertions. Did you mean Be() instead?");

    /// <inheritdoc/>
    public override int GetHashCode() =>
        throw new NotSupportedException("GetHashCode is not part of Fluent Assertions.");

    private class FieldValueEqualityComparer
        : IEqualityComparer<FieldValue<T>>
    {
        public static FieldValueEqualityComparer Default { get; } = new FieldValueEqualityComparer(EqualityComparer<T>.Default);

        private readonly IEqualityComparer<T> _comparer;

        public FieldValueEqualityComparer(IEqualityComparer<T> comparer)
        {
            Guard.IsNotNull(comparer);

            _comparer = comparer;
        }

        public bool Equals(FieldValue<T> x, FieldValue<T> y)
        {
            if (x.IsNull)
            {
                return y.IsNull;
            }

            if (x.IsUnset)
            {
                return y.IsUnset;
            }

            if (!y.HasValue)
            {
                return false;
            }

            return _comparer.Equals(x.Value!, y.Value);
        }

        public int GetHashCode([DisallowNull] FieldValue<T> obj)
        {
            if (obj.IsNull)
            {
                return 0;
            }

            if (obj.IsUnset)
            {
                return -1;
            }

            return _comparer.GetHashCode(obj.Value!);
        }
    }
}
