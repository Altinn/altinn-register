using System.Diagnostics;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten;

/// <summary>
/// Provides telemetry-related constants and the activity source for MaskinPorten HTTP client operations.
/// </summary>
internal static class MaskinPortenClientTelemetry
{
    /// <summary>
    /// The name of the activity source.
    /// </summary>
    public static readonly string Name = "Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten";
    private static readonly ActivitySource _activitySource = new(Name);

    /// <summary>
    /// Gets the <see cref="ActivitySource"/> for <c>ServiceDefaults.HttpClient.MaskinPorten</c>.
    /// </summary>
    internal static ActivitySource Source => _activitySource;
}
