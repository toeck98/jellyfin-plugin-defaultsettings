using System;

namespace Jellyfin.Plugin.DefaultSettings.Configuration;

/// <summary>
/// Serienspezifische Überschreibung der Einstellungen eines Benutzers.
/// </summary>
public class SeriesPreferenceOverride
{
    /// <summary>
    /// Eindeutige ID der Serie in Jellyfin.
    /// </summary>
    public Guid SeriesId { get; set; }

    /// <summary>
    /// Name der Serie zur einfachen Anzeige.
    /// </summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Bevorzugte Audiosprache (ISO-639 Code, z. B. 'jpn', 'deu').
    /// Null oder leer bedeutet: Globale Einstellung verwenden.
    /// </summary>
    public string? AudioLanguage { get; set; }

    /// <summary>
    /// Bevorzugte Untertitelsprache (ISO-639 Code, z. B. 'deu', 'eng').
    /// Null oder leer bedeutet: Globale Einstellung verwenden.
    /// </summary>
    public string? SubtitleLanguage { get; set; }

    /// <summary>
    /// Bevorzugter Untertitel-Modus.
    /// Null bedeutet: Globale Einstellung verwenden.
    /// </summary>
    public SubtitleModePreference? SubtitleMode { get; set; }

    /// <summary>
    /// Bevorzugte Medienversion (z. B. 'Bunt', 'Schwarz/Weiß', 'Extended').
    /// Null oder leer bedeutet: Standard-Version von Jellyfin beibehalten.
    /// </summary>
    public string? PreferredVersion { get; set; }
}
