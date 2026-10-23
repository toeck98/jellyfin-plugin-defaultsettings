using System;
using System.Collections.Generic;
using Jellyfin.Plugin.DefaultSettings.Configuration;

namespace Jellyfin.Plugin.DefaultSettings.Controllers.Models;

/// <summary>
/// DTO zur Übertragung der Benutzereinstellungen an das Web-Dashboard.
/// </summary>
public class UserPreferencesDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string PreferredAudioLanguage { get; set; } = "deu";
    public string PreferredSubtitleLanguage { get; set; } = "deu";
    public SubtitleModePreference SubtitleMode { get; set; } = SubtitleModePreference.OnlyForced;
    public string PreferredVersionKeyword { get; set; } = string.Empty;
    public List<SeriesPreferenceOverride> SeriesOverrides { get; set; } = new();
}

public class SaveUserPreferencesRequest
{
    public string PreferredAudioLanguage { get; set; } = "deu";
    public string PreferredSubtitleLanguage { get; set; } = "deu";
    public SubtitleModePreference SubtitleMode { get; set; } = SubtitleModePreference.OnlyForced;
    public string PreferredVersionKeyword { get; set; } = string.Empty;
}

public class SaveSeriesOverrideRequest
{
    public string? AudioLanguage { get; set; }
    public string? SubtitleLanguage { get; set; }
    public SubtitleModePreference? SubtitleMode { get; set; }
    public string? PreferredVersion { get; set; }
}
