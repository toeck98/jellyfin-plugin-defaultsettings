using System;
using System.Collections.Generic;
using Jellyfin.Plugin.DefaultSettings.Configuration;

namespace Jellyfin.Plugin.DefaultSettings.Services;

/// <summary>
/// Schnittstelle zur thread-sicheren Verwaltung von Plugin- und Benutzer-Voreinstellungen.
/// </summary>
public interface IDefaultSettingsManager
{
    /// <summary>
    /// Prüft, ob das Plugin global aktiviert ist.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Liefert die gesamte Plugin-Konfiguration.
    /// </summary>
    PluginConfiguration GetConfiguration();

    /// <summary>
    /// Aktualisiert und persistiert die gesamte Plugin-Konfiguration.
    /// </summary>
    void UpdateConfiguration(PluginConfiguration configuration);

    /// <summary>
    /// Ruft die Einstellungen für einen bestimmten Benutzer ab (oder erzeugt Standardwerte).
    /// </summary>
    UserDefaultPreference GetUserPreferences(Guid userId);

    /// <summary>
    /// Speichert die globalen Einstellungen für einen Benutzer.
    /// </summary>
    void SaveUserPreferences(UserDefaultPreference preferences);

    /// <summary>
    /// Ruft den Override für eine bestimmte Serie und einen Benutzer ab.
    /// </summary>
    SeriesPreferenceOverride? GetSeriesOverride(Guid userId, Guid seriesId);

    /// <summary>
    /// Speichert oder aktualisiert den Override für eine Serie bei einem Benutzer.
    /// </summary>
    void SaveSeriesOverride(Guid userId, SeriesPreferenceOverride seriesOverride);

    /// <summary>
    /// Entfernt den Override für eine Serie bei einem Benutzer.
    /// </summary>
    void DeleteSeriesOverride(Guid userId, Guid seriesId);
}
