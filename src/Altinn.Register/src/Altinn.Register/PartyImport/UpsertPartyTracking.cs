#nullable enable

using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Tracking information for a party import.
/// </summary>
public readonly record struct UpsertPartyTracking
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertPartyTracking"/> record.
    /// </summary>
    /// <param name="jobName">The job name.</param>
    /// <param name="progress">The progress.</param>
    [SetsRequiredMembers]
    public UpsertPartyTracking(string jobName, uint progress)
    {
        Guard.IsNotNull(jobName);
        Guard.IsGreaterThan(progress, 0);

        JobName = jobName;
        Progress = progress;
    }

    /// <summary>
    /// The name of the job that imported the party.
    /// </summary>
    public required string JobName { get; init; }

    /// <summary>
    /// The change ID of the party.
    /// </summary>
    public required uint Progress { get; init; }
}
