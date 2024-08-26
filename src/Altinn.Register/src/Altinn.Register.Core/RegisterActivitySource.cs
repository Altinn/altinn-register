using System.Diagnostics;
using Altinn.Authorization.ServiceDefaults.Telemetry;

namespace Altinn.Register.Core;

/// <summary>
/// Activity source for altinn register.
/// </summary>
public static class RegisterActivitySource
{
    /// <summary>
    /// The name of the activity source.
    /// </summary>
    public static readonly string Name = "Altinn.Register";
    private static readonly ActivitySource _activitySource = new(Name);

    /// <summary>
    /// Starts a new activity.
    /// </summary>
    /// <param name="kind">The activity kind.</param>
    /// <param name="name">The activity name.</param>
    /// <param name="tags">The tags to add to the activity.</param>
    /// <returns>A <see cref="Activity"/>, or <see langword="null"/> if the activity is not traced.</returns>
    public static Activity? StartActivity(ActivityKind kind, string name, ReadOnlySpan<KeyValuePair<string, object?>> tags = default)
        => _activitySource.StartActivity(kind, name, tags);
}
