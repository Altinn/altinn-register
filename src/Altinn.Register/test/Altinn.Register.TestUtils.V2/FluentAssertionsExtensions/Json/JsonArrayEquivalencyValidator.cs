using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using FluentAssertions.Equivalency;
using FluentAssertions.Equivalency.Tracing;
using FluentAssertions.Execution;
using Microsoft.Extensions.Primitives;

namespace Altinn.Register.TestUtils.FluentAssertionsExtensions.Json;

[ExcludeFromCodeCoverage]
internal ref struct JsonArrayEquivalencyValidator
{
    private const int FailedItemsFastFailThreshold = 10;

    public static void Validate(
        JsonElement subject,
        JsonElement expectation,
        IEquivalencyValidationContext context,
        IEquivalencyValidator parent)
    {
        Debug.Assert(subject.ValueKind == JsonValueKind.Array);
        Debug.Assert(expectation.ValueKind == JsonValueKind.Array);

        var subjectLength = subject.GetArrayLength();
        var expectationLength = expectation.GetArrayLength();

        AssertArraysHaveSameCount(subject, expectation, subjectLength, expectationLength);

        JsonElement[]? subjects = null;
        JsonElement[]? expectations = null;
        int[]? unmatchedSubjectIndices = null;
        StringValues[]? itemResults = null;

        try
        {
            subjects = ArrayPool<JsonElement>.Shared.Rent(subjectLength);
            expectations = ArrayPool<JsonElement>.Shared.Rent(expectationLength);
            unmatchedSubjectIndices = ArrayPool<int>.Shared.Rent(subjectLength);
            itemResults = ArrayPool<StringValues>.Shared.Rent(subjectLength);

            var subjectEnumerator = subject.EnumerateArray().GetEnumerator();
            var expectationEnumerator = expectation.EnumerateArray().GetEnumerator();

            for (var i = 0; i < subjectLength; i++)
            {
                if (!subjectEnumerator.MoveNext() || !expectationEnumerator.MoveNext())
                {
                    throw new UnreachableException();
                }

                subjects[i] = subjectEnumerator.Current;
                expectations[i] = expectationEnumerator.Current;
                unmatchedSubjectIndices[i] = i;
                itemResults[i] = default;
            }

            var validator = new JsonArrayEquivalencyValidator(
                subjects.AsSpan(0, subjectLength),
                expectations.AsSpan(0, expectationLength),
                subject,
                context,
                parent,
                unmatchedSubjectIndices.AsSpan(0, subjectLength),
                itemResults.AsSpan(0, subjectLength));

            using var _ = context.Tracer.WriteBlock(member => string.Create(
                CultureInfo.InvariantCulture,
                $"Structurally comparing {subject} and expectation {expectation} at {member.Description}"));

            validator.Validate();
        }
        finally
        {
            if (subjects is not null)
            {
                subjects.AsSpan().Clear();
                ArrayPool<JsonElement>.Shared.Return(subjects);
            }

            if (expectations is not null)
            {
                expectations.AsSpan().Clear();
                ArrayPool<JsonElement>.Shared.Return(expectations);
            }

            if (unmatchedSubjectIndices is not null)
            {
                unmatchedSubjectIndices.AsSpan().Clear();
                ArrayPool<int>.Shared.Return(unmatchedSubjectIndices);
            }

            if (itemResults is not null)
            {
                itemResults.AsSpan().Clear();
                ArrayPool<StringValues>.Shared.Return(itemResults);
            }
        }
    }

    private static void AssertArraysHaveSameCount(JsonElement subject, JsonElement expectation, int subjectLength, int expectationLength)
    {
        AssertionScope.Current
            .WithExpectation("Expected {context:subject} to be a collection with {0} item(s){reason}", expectationLength)
            .AssertEitherArrayIsNotEmpty(subject, expectation, subjectLength, expectationLength)
            .Then
            .AssertArrayHasEnoughItems(subject, expectation, subjectLength, expectationLength)
            .Then
            .AssertArrayHasNotTooManyItems(subject, expectation, subjectLength, expectationLength)
            .Then
            .ClearExpectation();
    }

    private readonly ReadOnlySpan<JsonElement> _subjects;
    private readonly ReadOnlySpan<JsonElement> _expectations;
    private readonly JsonElement _subjectsArray;
    private readonly IEquivalencyValidationContext _context;
    private readonly IEquivalencyValidator _parent;

    private Span<int> _unmatchedSubjectIndices;
    private Span<StringValues> _itemResults;

    private JsonArrayEquivalencyValidator(
        Span<JsonElement> subjects,
        Span<JsonElement> expectations,
        JsonElement subjectArray,
        IEquivalencyValidationContext context,
        IEquivalencyValidator parent,
        Span<int> unmatchedSubjectIndices,
        Span<StringValues> itemResults)
    {
        _subjects = subjects;
        _expectations = expectations;
        _subjectsArray = subjectArray;
        _context = context;
        _parent = parent;
        _unmatchedSubjectIndices = unmatchedSubjectIndices;
        _itemResults = itemResults;
    }

    private void Validate()
    {
        if (_context.Options.OrderingRules.IsOrderingStrictFor(JsonArrayObjectInfo.Instance))
        {
            AssertElementGraphEquivalencyWithStrictOrdering();
        }
        else
        {
            AssertElementGraphEquivalencyWithLooseOrdering();
        }
    }

    private void AssertElementGraphEquivalencyWithStrictOrdering()
    {
        var subjectArray = _subjectsArray;
        int failedCount = 0;

        foreach (int index in Enumerable.Range(0, _expectations.Length))
        {
            JsonElement expectation = _expectations[index];

            using var _ = _context.Tracer.WriteBlock(member => string.Create(
                CultureInfo.InvariantCulture,
                $"Strictly comparing expectation {expectation} at {member.Description} to item with index {index} in {subjectArray}"));

            bool succeeded = StrictlyMatchAgainst(expectation, index);

            if (!succeeded)
            {
                failedCount++;

                if (failedCount >= FailedItemsFastFailThreshold)
                {
                    _context.Tracer.WriteLine(member => string.Create(
                        CultureInfo.InvariantCulture,
                        $"Aborting strict order comparison of collections after {FailedItemsFastFailThreshold} items failed at {member.Description}"));

                    break;
                }
            }
        }
    }

    private void AssertElementGraphEquivalencyWithLooseOrdering()
    {
        var subjectArray = _subjectsArray;
        int failedCount = 0;

        foreach (int index in Enumerable.Range(0, _expectations.Length))
        {
            JsonElement expectation = _expectations[index];

            using var _ = _context.Tracer.WriteBlock(member => string.Create(
                CultureInfo.InvariantCulture,
                $"Finding the best match of {expectation} within all items in {subjectArray} at {member.Description}[{index}]"));

            bool succeeded = LooselyMatchAgainst(expectation, index);

            if (!succeeded)
            {
                failedCount++;

                if (failedCount >= FailedItemsFastFailThreshold)
                {
                    _context.Tracer.WriteLine(member => string.Create(
                        CultureInfo.InvariantCulture,
                        $"Fail failing loose order comparison of collection after {FailedItemsFastFailThreshold} items failed at {member.Description}"));

                    break;
                }
            }
        }
    }

    private bool StrictlyMatchAgainst(JsonElement expectation, int expectationIndex)
    {
        using var scope = new AssertionScope();

        var subject = _subjects[expectationIndex];
        var childContext = _context.AsCollectionItem<JsonElement>(expectationIndex.ToString(CultureInfo.InvariantCulture));

        _parent.RecursivelyAssertEquality(new Comparands(subject, expectation, typeof(JsonElement)), childContext);

        bool failed = scope.HasFailures();
        return !failed;
    }

    private bool LooselyMatchAgainst(JsonElement expectation, int expectationIndex)
    {
        int index = 0;

        GetTraceMessage getMessage = member => string.Create(
            CultureInfo.InvariantCulture,
            $"Comparing subject at {member.Description}[{index}] with the expectation at {member.Description}[{expectationIndex}]");

        int indexToBeRemoved = -1;

        _itemResults.Clear();

        for (var metaIndex = 0; metaIndex < _unmatchedSubjectIndices.Length; metaIndex++)
        {
            index = _unmatchedSubjectIndices[metaIndex];
            var subject = _subjects[index];

            using var _ = _context.Tracer.WriteBlock(getMessage);
            StringValues failures = TryMatch(subject, expectation, expectationIndex);
            var count = failures.Count;

            if (count is 0)
            {
                _context.Tracer.WriteLine(_ => "It's a match");
                indexToBeRemoved = metaIndex;
                break;
            }

            _itemResults[index] = failures;
            _context.Tracer.WriteLine(_ => $"Contained {count} failures");
        }

        if (indexToBeRemoved != -1)
        {
            SwapRemoveAt(ref _unmatchedSubjectIndices, indexToBeRemoved);
            return true;
        }
        else
        {
            StringValues bestMatch = default;
            var fewestFailures = int.MaxValue;
            for (var metaIndex = 0; metaIndex < _unmatchedSubjectIndices.Length; metaIndex++)
            {
                index = _unmatchedSubjectIndices[metaIndex];
                if (_itemResults[index].Count < fewestFailures)
                {
                    bestMatch = _itemResults[index];
                    fewestFailures = bestMatch.Count;
                }
            }

            Debug.Assert(fewestFailures > 0);

            if (_itemResults[expectationIndex].Count == fewestFailures)
            {
                bestMatch = _itemResults[expectationIndex];
            }

            foreach (var failure in bestMatch)
            {
                AssertionScope.Current.AddPreFormattedFailure(failure);
            }

            return false;
        }
    }

    private StringValues TryMatch(
            JsonElement subject,
            JsonElement expectation,
            int expectationIndex)
    {
        using var scope = new AssertionScope();

        var childContext = _context.AsCollectionItem<JsonElement>(expectationIndex.ToString(CultureInfo.InvariantCulture));

        _parent.RecursivelyAssertEquality(new Comparands(subject, expectation, typeof(JsonElement)), childContext);

        return scope.Discard();
    }

    private static void SwapRemoveAt(ref Span<int> indices, int index)
    {
        if (index == indices.Length - 1)
        {
            indices = indices.Slice(0, indices.Length - 1);
        }
        else
        {
            indices[index] = indices[indices.Length - 1];
            indices = indices.Slice(0, indices.Length - 1);
        }
    }

    private class JsonArrayObjectInfo
        : IObjectInfo
    {
        public static IObjectInfo Instance { get; } = new JsonArrayObjectInfo();

        public Type Type => typeof(Span<JsonElement>);

        public Type ParentType => throw new NotImplementedException();

        public string Path { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Type CompileTimeType => typeof(Span<JsonElement>);

        public Type RuntimeType => typeof(Span<JsonElement>);

        private JsonArrayObjectInfo() 
        {
        }
    }
}
