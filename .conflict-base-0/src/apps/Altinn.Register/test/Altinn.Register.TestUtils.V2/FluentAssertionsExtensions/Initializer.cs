using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FluentAssertions;

namespace Altinn.Register.TestUtils.FluentAssertionsExtensions;

/// <summary>
/// Setup for FluentAssertions.
/// </summary>
[ExcludeFromCodeCoverage]
public static class Initializer
{
    /// <summary>
    /// Initializes the fluent assertions.
    /// </summary>
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "Test setup")]
    public static void Initialize()
    {
        AssertionOptions.AssertEquivalencyUsing(options =>
        {
            options.Using(new JsonDocumentConversionStep());
            options.Using(new JsonStringConversionStep());
            options.Using(new JsonElementEquivalencyStep());

            return options;
        });
    }
}
