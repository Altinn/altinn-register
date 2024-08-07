using System.ComponentModel;
using System.Diagnostics;

namespace Altinn.Platform.Profile.Models;

/// <summary>
/// Class describing a users profile setting preferences.
/// </summary>
public record ProfileSettingPreference
{
    /// <summary>
    /// Sets the user's language preference in Altinn.
    /// </summary>
    /// <remarks>
    /// Only here for historical reasons. Use <see cref="Language"/> instead.
    /// </remarks>
    [Obsolete("Use Language instead")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string LanguageType
    {
        set => Language = value;
    }

    /// <summary>
    /// Gets or sets the user's language preference in Altinn.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the user's preselected party.
    /// </summary>
    public int PreSelectedPartyId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the users want
    /// to be asked for the party on every form submission.
    /// </summary>
    public bool DoNotPromptForParty { get; set; }
}
