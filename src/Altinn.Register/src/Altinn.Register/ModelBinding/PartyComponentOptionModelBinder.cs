#nullable enable
using System.Collections.Immutable;
using System.Text;

using Altinn.Platform.Register.Models;
using Altinn.Register.Model.Extensions;

using CommunityToolkit.Diagnostics;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Register.ModelBinding;

/// <summary>
/// Binds the <see cref="PartyComponentOption"/> model.
/// </summary>
public class PartyComponentOptionModelBinder
    : ModelBinder<PartyComponentOption>,
    ISingleton<PartyComponentOptionModelBinder>
{
    private static ImmutableDictionary<PartyComponentOption, string> _stringified = ImmutableDictionary<PartyComponentOption, string>.Empty;

    private static readonly string PersonNameOption = "person-name";

    private PartyComponentOptionModelBinder()
    {
    }

    /// <summary>
    /// The allowed values.
    /// </summary>
    public static readonly ImmutableArray<string> AllowedValues = [PersonNameOption];

    /// <summary>
    /// Stringify a <see cref="PartyComponentOption"/> to a query-valid value.
    /// </summary>
    /// <param name="value">The value to stringify.</param>
    /// <returns>The stringified value, or <see langword="null"/> if it is <see cref="PartyComponentOption.None"/>.</returns>
    public static string? Stringify(PartyComponentOption value)
    {
        if (value.HasFlag(PartyComponentOption.None))
        {
            return null;
        }

        return ImmutableInterlocked.GetOrAdd(ref _stringified, value, CreateString);

        static string CreateString(PartyComponentOption value)
        {
            var builder = new StringBuilder();
            if (value.HasFlag(PartyComponentOption.NameComponents))
            {
                builder.Append(PersonNameOption);
            }

            return builder.ToString();
        }
    }

    /// <inheritdoc/>
    public override Task BindModelAsync(ModelBindingContext bindingContext)
    {
        Guard.IsNotNull(bindingContext);

        var values = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        var result = PartyComponentOption.None;

        bool hasErrors = false;
        foreach (var value in values)
        {
            foreach (var name in value.AsSpan().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                result |= Parse(name, bindingContext, ref hasErrors);
            }
        }

        bindingContext.Result = hasErrors ? ModelBindingResult.Failed() : ModelBindingResult.Success(result);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public static PartyComponentOptionModelBinder Instance { get; } = new();

    private static PartyComponentOption Parse(ReadOnlySpan<char> name, ModelBindingContext bindingContext, ref bool hasErrors)
    {
        if (name.Equals(PersonNameOption, StringComparison.Ordinal))
        {
            return PartyComponentOption.NameComponents;
        }

        hasErrors = true;
        bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Invalid value: '{name}'");
        return PartyComponentOption.None;
    }
}
