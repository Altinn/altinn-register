﻿using System.Diagnostics;
using System.Numerics;
using Altinn.Authorization.ModelUtils.EnumUtils;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Persistence;

/// <summary>
/// What to filter by when looking up parties.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly record struct PartyQueryFilters
{
    private readonly static FlagsEnumModel<PartyLookupIdentifiers> _identifiersModel = FlagsEnumModel.Create<PartyLookupIdentifiers>();
    private readonly static FlagsEnumModel<PartyListFilters> _filtersModel = FlagsEnumModel.Create<PartyListFilters>();

    /// <summary>
    /// Gets a <see cref="PartyQueryFilters"/> instance configured for lookup queries that return at most one party,
    /// selected by the specified identifier.
    /// </summary>
    /// <param name="identifier">The party identifier to lookup based on.</param>
    /// <returns>A <see cref="PartyQueryFilters"/>.</returns>
    public static PartyQueryFilters LookupOne(PartyLookupIdentifiers identifier)
    {
        var bitsSet = BitOperations.PopCount((uint)identifier);
        if (bitsSet == 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(identifier), identifier, $"A single party lookup identifier must be set when using {nameof(LookupOne)}");
        }
        else if (bitsSet > 1)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(identifier), identifier, $"Only a single party lookup identifier can be set when using {nameof(LookupOne)}");
        }

        return new PartyQueryFilters(QueryMode.LookupOne, identifier, PartyListFilters.None);
    }

    /// <summary>
    /// Gets a <see cref="PartyQueryFilters"/> instance configured for lookup queries that return one or more parties,
    /// selected by the specified identifiers, and optionally filtered by additional list filters.
    /// </summary>
    /// <param name="identifiers">The party identifiers to lookup based on.</param>
    /// <param name="filters">Optional set of filters to apply post-lookup.</param>
    /// <returns>A <see cref="PartyQueryFilters"/>.</returns>
    public static PartyQueryFilters Lookup(PartyLookupIdentifiers identifiers, PartyListFilters filters = PartyListFilters.None)
    {
        var bitsSet = BitOperations.PopCount((uint)identifiers);
        if (bitsSet == 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(identifiers), identifiers, $"At least a single party lookup identifier must be set when using {nameof(Lookup)}");
        }

        return new PartyQueryFilters(QueryMode.LookupMultiple, identifiers, filters);
    }

    /// <summary>
    /// Gets a <see cref="PartyQueryFilters"/> instance configured for stream queries that return a filtered list of parties.
    /// </summary>
    /// <param name="filters">Optional set of filters to apply to the stream.</param>
    /// <returns>A <see cref="PartyQueryFilters"/>.</returns>
    public static PartyQueryFilters Stream(PartyListFilters filters = PartyListFilters.None)
    {
        return new PartyQueryFilters(QueryMode.FilteredStream, PartyLookupIdentifiers.None, filters);
    }

    private readonly QueryMode _mode;
    private readonly PartyLookupIdentifiers _identifiers;
    private readonly PartyListFilters _filters;

    private PartyQueryFilters(QueryMode mode, PartyLookupIdentifiers identifiers, PartyListFilters filters)
        => (_mode, _identifiers, _filters) = (mode, identifiers, filters);

    /// <summary>
    /// Validates the current <see cref="PartyQueryFilters"/> instance.
    /// </summary>
    internal void Validate(string argumentName)
    {
        if (_mode == QueryMode.Invalid)
        {
            ThrowHelper.ThrowArgumentException(argumentName, $"default({nameof(PartyQueryFilters)}) is not valid");
        }
    }

    /// <summary>
    /// Gets the query mode.
    /// </summary>
    internal readonly QueryMode Mode => _mode;

    /// <summary>
    /// Gets the lookup identifiers.
    /// </summary>
    internal readonly PartyLookupIdentifiers LookupIdentifiers => _identifiers;

    /// <summary>
    /// Gets the list filters.
    /// </summary>
    internal readonly PartyListFilters ListFilters => _filters;

    /// <summary>
    /// Gets a value indicating whether the current query mode is set to stream mode.
    /// </summary>
    public bool IsStream => _mode == QueryMode.FilteredStream;

    /// <inheritdoc/>
    public override string ToString()
    {
        switch (_mode)
        {
            case QueryMode.LookupOne:
                return $"lookupOne({_identifiersModel.Format(_identifiers)})";

            case QueryMode.LookupMultiple when _filters is PartyListFilters.None:
                return $"lookup({_identifiersModel.Format(_identifiers)})";

            case QueryMode.LookupMultiple:
                return $"lookup({_identifiersModel.Format(_identifiers)}; {_filtersModel.Format(_filters)})";

            case QueryMode.FilteredStream when _filters is PartyListFilters.None:
                return $"stream()";

            case QueryMode.FilteredStream:
                return $"stream({_filtersModel.Format(_filters)})";

            default:
                return "invalid";
        }
    }

    /// <summary>
    /// Specifies the available modes for querying parties.
    /// </summary>
    /// <remarks>
    /// <c>default(QueryMode)</c> is explicitly invalid, such that <c>default(PartyQueryFilters)</c> can be detected.
    /// </remarks>
    internal enum QueryMode
        : byte
    {
        /// <summary>
        /// Invalid default value.
        /// </summary>
        Invalid = default,

        /// <summary>
        /// Lookup a single party by an identifier that returns at most one party.
        /// </summary>
        LookupOne = 1,

        /// <summary>
        /// Lookup one or more parties by identifiers that return at most one party each.
        /// </summary>
        LookupMultiple,

        /// <summary>
        /// Represents a stream that provides filtered access to an underlying data source.
        /// </summary>
        FilteredStream,
    }
}
