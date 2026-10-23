using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DefaultSettings.Configuration;

/// <summary>
/// Plugin-Konfiguration für DefaultSettings.
/// Wird automatisch vom Jellyfin-Host in data/plugins/configurations/DefaultSettings.xml persistiert.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gibt an, ob das Plugin aktiv ist.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Serverweiter Fallback für die Audiosprache für neue Benutzer.
    /// </summary>
    public string DefaultFallbackAudio { get; set; } = "deu";

    /// <summary>
    /// Serverweiter Fallback für die Untertitelsprache für neue Benutzer.
    /// </summary>
    public string DefaultFallbackSubtitle { get; set; } = "deu";

    /// <summary>
    /// Serverweiter Fallback für den Untertitel-Modus für neue Benutzer.
    /// </summary>
    public SubtitleModePreference DefaultFallbackSubtitleMode { get; set; } = SubtitleModePreference.OnlyForced;

    /// <summary>
    /// Alle benutzerspezifischen Einstellungen.
    /// </summary>
    public List<UserDefaultPreference> UserPreferences { get; set; } = new();
}
