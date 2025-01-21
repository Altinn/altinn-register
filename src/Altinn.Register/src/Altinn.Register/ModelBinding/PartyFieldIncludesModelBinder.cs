#nullable enable

using System.Text.Json;
using Altinn.Register.Core.Parties;

namespace Altinn.Register.ModelBinding;

/// <summary>
/// Model binder for <see cref="PartyFieldIncludes"/>.
/// </summary>
public class PartyFieldIncludesModelBinder
    : FlagsEnumModelBinder<PartyFieldIncludes>
    , ISingleton<PartyFieldIncludesModelBinder>
{
    private static FlagsEnumModel<PartyFieldIncludes> _model
        = FlagsEnumModel.Create<PartyFieldIncludes>(JsonNamingPolicy.KebabCaseLower, StringComparison.Ordinal);

    private PartyFieldIncludesModelBinder()
        : base(_model)
    {
    }

    /// <inheritdoc/>
    public static PartyFieldIncludesModelBinder Instance { get; } = new();

    /// <summary>
    /// Formats a <see cref="PartyFieldIncludes"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The formatted <paramref name="value"/>.</returns>
    public static string? Format(PartyFieldIncludes value)
        => _model.Format(value);
}
