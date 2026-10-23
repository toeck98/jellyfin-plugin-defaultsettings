using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.DefaultSettings.Configuration;

namespace Jellyfin.Plugin.DefaultSettings.Services;

/// <summary>
/// Thread-sichere Implementierung des DefaultSettings-Managers.
/// </summary>
public class DefaultSettingsManager : IDefaultSettingsManager
{
    private readonly ILogger<DefaultSettingsManager> _logger;
    private readonly object _lock = new();

    public DefaultSettingsManager(ILogger<DefaultSettingsManager> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled => Plugin.Instance?.Configuration.IsEnabled ?? false;

    public PluginConfiguration GetConfiguration()
    {
        lock (_lock)
        {
            return Plugin.Instance?.Configuration ?? new PluginConfiguration();
        }
    }

    public void UpdateConfiguration(PluginConfiguration configuration)
    {
        lock (_lock)
        {
            Plugin.Instance?.UpdateConfiguration(configuration);
            _logger.LogInformation("DefaultSettings Plugin-Konfiguration wurde erfolgreich aktualisiert.");
        }
    }

    public UserDefaultPreference GetUserPreferences(Guid userId)
    {
        lock (_lock)
        {
            var config = GetConfiguration();
            var pref = config.UserPreferences.FirstOrDefault(u => u.UserId == userId);
            if (pref == null)
            {
                pref = new UserDefaultPreference
                {
                    UserId = userId,
                    PreferredAudioLanguage = config.DefaultFallbackAudio,
                    PreferredSubtitleLanguage = config.DefaultFallbackSubtitle,
                    SubtitleMode = config.DefaultFallbackSubtitleMode,
                    PreferredVersionKeyword = string.Empty,
                    SeriesOverrides = new List<SeriesPreferenceOverride>()
                };
            }

            return pref;
        }
    }

    public void SaveUserPreferences(UserDefaultPreference preferences)
    {
        lock (_lock)
        {
            var config = GetConfiguration();
            var existingIndex = config.UserPreferences.FindIndex(u => u.UserId == preferences.UserId);

            if (existingIndex >= 0)
            {
                // Erhalte bestehende SeriesOverrides, falls nicht im Request übergeben
                var existing = config.UserPreferences[existingIndex];
                if (preferences.SeriesOverrides == null || preferences.SeriesOverrides.Count == 0)
                {
                    preferences.SeriesOverrides = existing.SeriesOverrides;
                }

                config.UserPreferences[existingIndex] = preferences;
            }
            else
            {
                config.UserPreferences.Add(preferences);
            }

            UpdateConfiguration(config);
            _logger.LogDebug("Voreinstellungen für Benutzer {UserId} wurden gespeichert.", preferences.UserId);
        }
    }

    public SeriesPreferenceOverride? GetSeriesOverride(Guid userId, Guid seriesId)
    {
        lock (_lock)
        {
            var userPref = GetUserPreferences(userId);
            return userPref.SeriesOverrides?.FirstOrDefault(s => s.SeriesId == seriesId);
        }
    }

    public void SaveSeriesOverride(Guid userId, SeriesPreferenceOverride seriesOverride)
    {
        lock (_lock)
        {
            var config = GetConfiguration();
            var userPref = config.UserPreferences.FirstOrDefault(u => u.UserId == userId);

            if (userPref == null)
            {
                userPref = new UserDefaultPreference
                {
                    UserId = userId,
                    PreferredAudioLanguage = config.DefaultFallbackAudio,
                    PreferredSubtitleLanguage = config.DefaultFallbackSubtitle,
                    SubtitleMode = config.DefaultFallbackSubtitleMode,
                    SeriesOverrides = new List<SeriesPreferenceOverride>()
                };
                config.UserPreferences.Add(userPref);
            }

            userPref.SeriesOverrides ??= new List<SeriesPreferenceOverride>();

            var existingIndex = userPref.SeriesOverrides.FindIndex(s => s.SeriesId == seriesOverride.SeriesId);
            if (existingIndex >= 0)
            {
                userPref.SeriesOverrides[existingIndex] = seriesOverride;
            }
            else
            {
                userPref.SeriesOverrides.Add(seriesOverride);
            }

            UpdateConfiguration(config);
            _logger.LogInformation("Override für Serie '{SeriesName}' ({SeriesId}) von Benutzer {UserId} gespeichert.",
                seriesOverride.SeriesName, seriesOverride.SeriesId, userId);
        }
    }

    public void DeleteSeriesOverride(Guid userId, Guid seriesId)
    {
        lock (_lock)
        {
            var config = GetConfiguration();
            var userPref = config.UserPreferences.FirstOrDefault(u => u.UserId == userId);

            if (userPref != null && userPref.SeriesOverrides != null)
            {
                int removed = userPref.SeriesOverrides.RemoveAll(s => s.SeriesId == seriesId);
                if (removed > 0)
                {
                    UpdateConfiguration(config);
                    _logger.LogInformation("Override für Serie {SeriesId} von Benutzer {UserId} entfernt.", seriesId, userId);
                }
            }
        }
    }
}
