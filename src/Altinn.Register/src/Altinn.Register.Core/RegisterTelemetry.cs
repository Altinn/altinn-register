using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<Type, object> _serviceMeters = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterTelemetry"/> class.
    /// </summary>
    public RegisterTelemetry(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(Name);
    }

    /// <summary>
    /// Creates and starts a new <see cref="Activity"/> object if there is any listener to the Activity, returns null otherwise.
    /// </summary>
    /// <param name="name">The operation name of the Activity.</param>
    /// <param name="kind">The <see cref="ActivityKind"/>.</param>
    /// <param name="parentContext">The parent <see cref="ActivityContext"/> object to initialize the created Activity object with.</param>
    /// <param name="tags">The optional tags list to initialize the created Activity object with.</param>
    /// <param name="links">The optional <see cref="ActivityLink"/> list to initialize the created Activity object with.</param>
    /// <param name="startTime">The optional start timestamp to set on the created Activity object.</param>
    /// <returns>The created <see cref="Activity"/> object or null if there is no any event listener.</returns>
    public static Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        ActivityContext parentContext = default,
        ReadOnlySpan<KeyValuePair<string, object?>> tags = default,
        ReadOnlySpan<ActivityLink> links = default,
        DateTimeOffset startTime = default)
        => _activitySource.StartActivity(name, kind, parentContext, tags, links, startTime);

    /// <inheritdoc cref="Meter.CreateCounter{T}(string, string?, string?)"/>
    public Counter<T> CreateCounter<T>(string name, string? unit = null, string? description = null)
        where T : struct
        => _meter.CreateCounter<T>(name, unit, description);

    /// <inheritdoc cref="Meter.CreateHistogram{T}(string, string?, string?)"/>
    public Histogram<T> CreateHistogram<T>(string name, string? unit = null, string? description = null)
        where T : struct
        => _meter.CreateHistogram<T>(name, unit, description);

    /// <summary>
    /// Gets
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetServiceMeters<T>()
        where T : class, IServiceMeters<T>
        => (T)_serviceMeters.GetOrAdd(typeof(T), static (_, telemetry) => T.Create(telemetry), this);
}

/// <summary>
/// A set of meters for a service.
/// </summary>
/// <typeparam name="TSelf">Self type.</typeparam>
public interface IServiceMeters<TSelf>
    where TSelf : class, IServiceMeters<TSelf>
{
    /// <summary>
    /// Creates a new instance of self.
    /// </summary>
    /// <param name="telemetry">The <see cref="RegisterTelemetry"/>.</param>
    /// <returns>A new instance of self.</returns>
    public static abstract TSelf Create(RegisterTelemetry telemetry);
}
