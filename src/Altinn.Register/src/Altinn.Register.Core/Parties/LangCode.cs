using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Represents a language code.
/// </summary>
[DebuggerDisplay("{Code}")]
public class LangCode
    : IEquatable<LangCode>
    , IComparable<LangCode>
    , IEqualityOperators<LangCode, LangCode, bool>
    , IComparisonOperators<LangCode, LangCode, bool>
{
    /// <summary>
    /// The language code for English.
    /// </summary>
    internal const string EN_CODE = "en";

    /// <summary>
    /// The language code for Norwegian Bokmål.
    /// </summary>
    internal const string NB_CODE = "nb";

    /// <summary>
    /// The language code for Norwegian Nynorsk.
    /// </summary>
    internal const string NN_CODE = "nn";

    /// <summary>
    /// Gets the language code for English.
    /// </summary>
    public static readonly LangCode En = new LangCode(EN_CODE);

    /// <summary>
    /// Gets the language code for Norwegian Bokmål.
    /// </summary>
    public static readonly LangCode Nb = new LangCode(NB_CODE);

    /// <summary>
    /// Gets the language code for Norwegian Nynorsk.
    /// </summary>
    public static readonly LangCode Nn = new LangCode(NN_CODE);

    private static ImmutableDictionary<string, LangCode> _codes
        = ImmutableDictionary.CreateRange<string, LangCode>(
            keyComparer: StringComparer.OrdinalIgnoreCase,
            items: [
                new(En.Code, En),
                new(Nb.Code, Nb),
                new(Nn.Code, Nn),
            ]);

    /// <summary>
    /// Gets a language code from a string.
    /// </summary>
    /// <param name="code">The language code as a string.</param>
    /// <returns>A <see cref="LangCode"/>.</returns>
    public static LangCode FromCode(string code)
    {
        return code switch
        {
            // normal cases
            "en" => En,
            "nb" => Nb,
            "nn" => Nn,

            // rest
            _ => GetOrCreateCode(code),
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        static LangCode GetOrCreateCode(string code)
        {
            code = code.ToLowerInvariant();
            if (!code.IsNormalized())
            {
                code = code.Normalize();
            }

            return ImmutableInterlocked.GetOrAdd(ref _codes, code, static c => new(c));
        }
    }

    private readonly string _code;

    /// <summary>
    /// Gets the language code as a string.
    /// </summary>
    public string Code => _code;

    private LangCode(string code)
    {
        Guard.IsNotNull(code);

        _code = code;
    }

    /// <inheritdoc/>
    public override string ToString()
        => _code;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // this does not need to be OrdinalIgnoreCase, as we normalize the code before calling the constructor
        return string.GetHashCode(_code, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        // no two instances of LangCode should have the same code, the global dictionary ensures this
        return ReferenceEquals(this, obj);
    }

    /// <inheritdoc/>
    public bool Equals(LangCode? other)
    {
        // no two instances of LangCode should have the same code, the global dictionary ensures this
        return ReferenceEquals(this, other);
    }

    /// <inheritdoc/>
    public int CompareTo(LangCode? other)
    {
        return ReferenceEquals(this, other) ? 0
            : string.Compare(_code, other?._code, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public static bool operator ==(LangCode? left, LangCode? right)
    {
        // no two instances of LangCode should have the same code, the global dictionary ensures this
        return ReferenceEquals(left, right);
    }

    /// <inheritdoc/>
    public static bool operator !=(LangCode? left, LangCode? right)
    {
        // no two instances of LangCode should have the same code, the global dictionary ensures this
        return !ReferenceEquals(left, right);
    }

    /// <inheritdoc/>
    public static bool operator >(LangCode left, LangCode right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <inheritdoc/>
    public static bool operator >=(LangCode left, LangCode right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc/>
    public static bool operator <(LangCode left, LangCode right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc/>
    public static bool operator <=(LangCode left, LangCode right)
    {
        return left.CompareTo(right) <= 0;
    }
}
