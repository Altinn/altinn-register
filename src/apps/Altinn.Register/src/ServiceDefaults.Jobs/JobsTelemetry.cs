using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.Telemetry;

namespace Altinn.Authorization.ServiceDefaults.Jobs;

/// <summary>
/// Telemetry for Altinn.Authorization.ServiceDefaults.Jobs.
/// </summary>
internal class JobsTelemetry
{
    /// <summary>
    /// The name of the activity source.
    /// </summary>
    public static readonly string Name = "Altinn.Authorization.ServiceDefaults.Jobs";
    private static readonly ActivitySource _activitySource = new(Name);

    private readonly Meter _meter;
    private readonly ConcurrentDictionary<string, long> _lastSuccessfulRuns = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="JobsTelemetry"/> class.
    /// </summary>
    public JobsTelemetry(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(Name);
        _meter.CreateObservableGauge(
            "register.jobs.last_success_time",
            ObserveLastSuccessfulRuns,
            unit: "s",
            description: "Time of the last successful job completion, in seconds since the Unix epoch. Per instance; absent until the first success and lost on restart.");
    }

    /// <summary>
    /// Records a successful run for a logical job, across all run sources on this instance.
    /// </summary>
    /// <param name="jobName">The job name.</param>
    /// <param name="completedAt">The completion time.</param>
    public void RecordJobSuccess(string jobName, DateTimeOffset completedAt)
        => _lastSuccessfulRuns.AddOrUpdate(jobName, completedAt.ToUnixTimeSeconds(), (_, previous) => Math.Max(previous, completedAt.ToUnixTimeSeconds()));

    private IEnumerable<Measurement<long>> ObserveLastSuccessfulRuns()
    {
        foreach (var (jobName, timestamp) in _lastSuccessfulRuns)
        {
            yield return new(timestamp, new KeyValuePair<string, object?>("job.name", jobName));
        }
    }

    /// <inheritdoc cref="Meter.CreateCounter{T}(string, string?, string?)"/>
    public Counter<T> CreateCounter<T>(string name, string? unit = null, string? description = null)
        where T : struct
        => _meter.CreateCounter<T>(name, unit, description);

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

    /// <summary>
    /// Creates and starts a new <see cref="Activity"/> object if there is any listener to the Activity, returns null otherwise.
    /// </summary>
    /// <param name="name">The operation name of the Activity.</param>
    /// <param name="tags">The optional tags list to initialize the created Activity object with.</param>
    /// <param name="kind">The <see cref="ActivityKind"/>.</param>
    /// <param name="parentContext">The parent <see cref="ActivityContext"/> object to initialize the created Activity object with.</param>
    /// <param name="links">The optional <see cref="ActivityLink"/> list to initialize the created Activity object with.</param>
    /// <param name="startTime">The optional start timestamp to set on the created Activity object.</param>
    /// <returns>The created <see cref="Activity"/> object or null if there is no any event listener.</returns>
    public static Activity? StartActivity(
        string name,
        in TagList tags,
        ActivityKind kind = ActivityKind.Internal,
        ActivityContext parentContext = default,
        ReadOnlySpan<ActivityLink> links = default,
        DateTimeOffset startTime = default)
        => _activitySource.StartActivity(name, kind, in tags, parentContext, links, startTime);

    /// <summary>
    /// Creates and starts a new <see cref="Activity"/> object if there is any listener to the Activity, returns null otherwise.
    /// </summary>
    /// <param name="name">The operation name of the Activity.</param>
    /// <param name="kind">The <see cref="ActivityKind"/>.</param>
    /// <param name="tags">The optional tags list to initialize the created Activity object with.</param>
    /// <param name="parentContext">The parent <see cref="ActivityContext"/> object to initialize the created Activity object with.</param>
    /// <param name="links">The optional <see cref="ActivityLink"/> list to initialize the created Activity object with.</param>
    /// <param name="startTime">The optional start timestamp to set on the created Activity object.</param>
    /// <returns>The created <see cref="Activity"/> object or null if there is no any event listener.</returns>
    public static Activity? StartActivity(
        string name,
        ActivityKind kind,
        in TagList tags,
        ActivityContext parentContext = default,
        ReadOnlySpan<ActivityLink> links = default,
        DateTimeOffset startTime = default)
        => _activitySource.StartActivity(name, kind, in tags, parentContext, links, startTime);
}
