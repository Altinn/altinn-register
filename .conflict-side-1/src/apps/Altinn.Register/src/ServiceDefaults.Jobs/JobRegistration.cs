using System.Collections.Immutable;
using System.Reflection;
using CommunityToolkit.Diagnostics;

namespace Altinn.Authorization.ServiceDefaults.Jobs;

/// <summary>
/// A registration for a job.
/// </summary>
public abstract class JobRegistration(
    string name,
    string? leaseName,
    TimeSpan interval,
    JobHostLifecycles runAt,
    IEnumerable<string> tags,
    Func<IServiceProvider, CancellationToken, ValueTask<bool>>? enabled,
    Func<IServiceProvider, CancellationToken, ValueTask>? waitForReady)
    : IJobRegistration
{
    private readonly ImmutableArray<string> _tags = ToTagArray(tags);
    private readonly Lock _lock = new();
    private Task? _ready;

    /// <summary>
    /// Creates a new instance of the job.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <returns>A new instance of <see cref="IJob"/>.</returns>
    public abstract IJob Create(IServiceProvider services);

    /// <inheritdoc/>
    public string JobName => name;

    /// <inheritdoc/>
    public string? LeaseName => leaseName;

    /// <inheritdoc/>
    public TimeSpan Interval => interval;

    /// <inheritdoc/>
    public JobHostLifecycles RunAt => runAt;

    /// <inheritdoc/>
    public ImmutableArray<string> Tags => _tags;

    /// <summary>
    /// Checks if the job has a specific tag.
    /// </summary>
    /// <param name="tag">The tag name.</param>
    /// <returns><see langword="true"/> if the job has the tag <paramref name="tag"/>, otherwise <see langword="false"/>.</returns>
    public bool HasTag(string tag)
    {
        Guard.IsNotNullOrEmpty(tag);

        // _tags is immutable and sorted, so we can use binary search
        int index = _tags.AsSpan().BinarySearch(tag, StringComparer.Ordinal);
        return index >= 0;
    }

    /// <summary>
    /// Checks if the job is enabled or not.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <remarks>
    /// Enabled runs in singleton scope, so it should not resolve scoped services. Should never throw.
    /// 
    /// Note: this is an earlier check than <see cref="IJob.ShouldRun(CancellationToken)"/>, which
    /// technically runs in the scope of the job itself. This means that if this returns <see langword="false"/>,
    /// the job will not be created or run at all, and the job's <see cref="IJob.ShouldRun(CancellationToken)"/> 
    /// method will not be called. <see cref="Enabled(IServiceProvider, CancellationToken)"/> is typically used to
    /// check if the job is enabled based on configuration or other conditions that do not require the job to be
    /// instantiated.
    /// </remarks>
    /// <returns><see langword="true"/> if the job is enabled, otherwise <see langword="false"/>.</returns>
    public ValueTask<bool> Enabled(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        return enabled switch
        {
            null => ValueTask.FromResult(true),
            { } fn => fn(services, cancellationToken),
        };
    }

    /// <summary>
    /// Waits for the job to be ready to run. This is only ran once per job registration.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <remarks>
    /// Wait for ready runs in singleton scope, so it should not resolve scoped services. Should never throw.
    /// </remarks>
    public Task WaitForReady(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        Task? task;
        lock (_lock)
        {
            task = _ready;
            if (task is null)
            {
                task = waitForReady is null ? Task.CompletedTask : waitForReady(services, cancellationToken).AsTask();
                _ready = task;
            }
        }

        return task.WaitAsync(cancellationToken);
    }

    private static ImmutableArray<string> ToTagArray(IEnumerable<string> tags)
    {
        if (tags is IReadOnlyCollection<string> { Count: 0 }
            || tags is ICollection<string> { Count: 0 })
        {
            return [];
        }

        var set = new SortedSet<string>(tags, StringComparer.Ordinal);
        return [.. set];
    }

    /// <summary>
    /// Gets the job name for a given job type.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <returns>The job name. Normally same as the type name.</returns>
    public static string GetJobNameForType(Type jobType)
    {
        Guard.IsNotNull(jobType);

        var nameIface = jobType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHasJobName<>) && i.GetGenericArguments()[0] == jobType);

        if (nameIface is not null)
        {
            var genericMethod = typeof(JobRegistration).GetMethod(nameof(GetJobNameFromIfaceImpl), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(jobType);
            return (string)genericMethod.Invoke(null, [])!;
        }

        return jobType.Name;
    }

    private static string GetJobNameFromIfaceImpl<T>()
        where T : IHasJobName<T>
        => T.JobName;
}
