using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.DefaultSettings.Configuration;

/// <summary>
/// Globale und serienspezifische Einstellungen für einen einzelnen Benutzer.
/// </summary>
public class UserDefaultPreference
{
    /// <summary>
    /// ID des Jellyfin-Benutzers.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Globale bevorzugte Audiosprache (ISO-639 Code, z. B. 'deu', 'eng').
    /// </summary>
    public string PreferredAudioLanguage { get; set; } = "deu";

    /// <summary>
    /// Globale bevorzugte Untertitelsprache (ISO-639 Code, z. B. 'deu', 'eng').
    /// </summary>
    public string PreferredSubtitleLanguage { get; set; } = "deu";

    /// <summary>
    /// Globaler bevorzugter Untertitel-Modus.
    /// </summary>
    public SubtitleModePreference SubtitleMode { get; set; } = SubtitleModePreference.OnlyForced;

    /// <summary>
    /// Globaler bevorzugter Versions-Suchbegriff (optional).
    /// </summary>
    public string PreferredVersionKeyword { get; set; } = string.Empty;

    /// <summary>
    /// Liste aller serienspezifischen Ausnahmen/Overrides für diesen Benutzer.
    /// </summary>
    public List<SeriesPreferenceOverride> SeriesOverrides { get; set; } = new();
}
