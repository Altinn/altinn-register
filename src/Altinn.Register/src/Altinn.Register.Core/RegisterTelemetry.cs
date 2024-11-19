using System.Diagnostics;
using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.Telemetry;

namespace Altinn.Register.Core;

/// <summary>
/// Telemetry source for altinn register.
/// </summary>
public sealed class RegisterTelemetry
{
    /// <summary>
    /// The name of the activity source.
    /// </summary>
    public static readonly string Name = "Altinn.Register";
    private static readonly ActivitySource _activitySource = new(Name);
    
    private readonly Meter _meter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterTelemetry"/> class.
    /// </summary>
    public RegisterTelemetry(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(Name);
    }

    /// <summary>
    /// Starts a new activity.
    /// </summary>
    /// <param name="kind">The activity kind.</param>
    /// <param name="name">The activity name.</param>
    /// <param name="tags">The tags to add to the activity.</param>
    /// <returns>A <see cref="Activity"/>, or <see langword="null"/> if the activity is not traced.</returns>
    public static Activity? StartActivity(ActivityKind kind, string name, ReadOnlySpan<KeyValuePair<string, object?>> tags = default)
        => _activitySource.StartActivity(kind, name, tags);

    /// <inheritdoc cref="Meter.CreateCounter{T}(string, string?, string?)"/>
    public Counter<T> CreateCounter<T>(string name, string? unit = null, string? description = null)
        where T : struct
        => _meter.CreateCounter<T>(name, unit, description);
}
