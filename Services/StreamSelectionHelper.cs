using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Jellyfin.Plugin.DefaultSettings.Configuration;

namespace Jellyfin.Plugin.DefaultSettings.Services;

/// <summary>
/// Logik zur Berechnung und Anwendung von Versionen, Ton- und Untertitel-Voreinstellungen auf MediaSources.
/// </summary>
public static class StreamSelectionHelper
{
    /// <summary>
    /// Wendet die konfigurierten Voreinstellungen des Nutzers auf die Liste der MediaSources an.
    /// </summary>
    public static void ApplyPreferences(
        IList<MediaSourceInfo> mediaSources,
        UserDefaultPreference? userPref,
        SeriesPreferenceOverride? seriesOverride)
    {
        if (mediaSources == null || mediaSources.Count == 0)
        {
            return;
        }

        // Wenn weder ein Serien-Override noch ein Versions-Keyword existiert: Native Jellyfin-Auswahl unverändert lassen!
        if (seriesOverride == null && string.IsNullOrWhiteSpace(userPref?.PreferredVersionKeyword))
        {
            return;
        }

        // 1. Versionen / MediaSources priorisieren (Multi-Version Handling)
        var preferredVersion = seriesOverride?.PreferredVersion;
        if (string.IsNullOrWhiteSpace(preferredVersion))
        {
            preferredVersion = userPref?.PreferredVersionKeyword;
        }

        if (!string.IsNullOrWhiteSpace(preferredVersion) && mediaSources.Count > 1)
        {
            var preferredTrimmed = preferredVersion.Trim();
            int matchIndex = -1;

            for (int i = 0; i < mediaSources.Count; i++)
            {
                var ms = mediaSources[i];
                if ((ms.Name != null && ms.Name.Contains(preferredTrimmed, StringComparison.OrdinalIgnoreCase)) ||
                    (ms.Path != null && ms.Path.Contains(preferredTrimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    matchIndex = i;
                    break;
                }
            }

            // Wenn die Wunschversion gefunden wurde und nicht bereits an Index 0 liegt: nach vorne ziehen
            if (matchIndex > 0)
            {
                var matchedSource = mediaSources[matchIndex];
                mediaSources.RemoveAt(matchIndex);
                mediaSources.Insert(0, matchedSource);
            }
            // Falls Wunschversion bei dieser Folge nicht vorhanden: Jellyfin Standard beibehalten
        }

        // 2. Audio- und Untertitel-Spuren konfigurieren (nur wenn für diese Serie explizit überschrieben)
        var targetAudioLang = seriesOverride?.AudioLanguage;
        var targetSubLang = seriesOverride?.SubtitleLanguage;
        var subMode = seriesOverride?.SubtitleMode;

        // Falls für diese Serie weder Audio noch Untertitel überschrieben wurden: Streams unangetastet lassen
        if (string.IsNullOrWhiteSpace(targetAudioLang) && string.IsNullOrWhiteSpace(targetSubLang) && !subMode.HasValue)
        {
            return;
        }

        foreach (var source in mediaSources)
        {
            ApplyStreamsToSource(source, targetAudioLang, targetSubLang, subMode);
        }
    }

    private static void ApplyStreamsToSource(
        MediaSourceInfo source,
        string? targetAudioLang,
        string? targetSubLang,
        SubtitleModePreference? subMode)
    {
        if (source.MediaStreams == null || source.MediaStreams.Count == 0)
        {
            return;
        }

        var audioStreams = source.MediaStreams.Where(s => s.Type == MediaStreamType.Audio).ToList();
        var subStreams = source.MediaStreams.Where(s => s.Type == MediaStreamType.Subtitle).ToList();

        // --- Audio Stream Auswahl (nur wenn targetAudioLang angegeben ist) ---
        MediaStream? chosenAudio = null;
        if (!string.IsNullOrWhiteSpace(targetAudioLang) && audioStreams.Count > 0)
        {
            chosenAudio = audioStreams.FirstOrDefault(s => MatchesLanguage(s, targetAudioLang) && s.IsDefault)
                       ?? audioStreams.Where(s => MatchesLanguage(s, targetAudioLang))
                                      .OrderByDescending(s => s.Channels ?? 2)
                                      .FirstOrDefault();

            if (chosenAudio != null)
            {
                source.DefaultAudioStreamIndex = chosenAudio.Index;
                foreach (var a in audioStreams)
                {
                    a.IsDefault = (a.Index == chosenAudio.Index);
                }
            }
        }

        // Falls keine passende Audiospur gefunden oder keine Zielsprache definiert: Aktiven Container-Standard ermitteln
        chosenAudio ??= audioStreams.FirstOrDefault(a => a.IsDefault) ?? audioStreams.FirstOrDefault();

        // --- Untertitel Stream Auswahl (nur wenn SubtitleLanguage oder SubtitleMode gesetzt sind) ---
        if (subMode.HasValue || !string.IsNullOrWhiteSpace(targetSubLang))
        {
            var effectiveMode = subMode ?? SubtitleModePreference.OnlyForced;
            int chosenSubIndex = -1;

            if (effectiveMode == SubtitleModePreference.None || subStreams.Count == 0)
            {
                chosenSubIndex = -1;
            }
            else if (!string.IsNullOrWhiteSpace(targetSubLang))
            {
                if (effectiveMode == SubtitleModePreference.OnlyForced)
                {
                    var forced = subStreams.FirstOrDefault(s => MatchesLanguage(s, targetSubLang) && s.IsForced);
                    chosenSubIndex = forced?.Index ?? -1;
                }
                else if (effectiveMode == SubtitleModePreference.Always)
                {
                    var regular = subStreams.FirstOrDefault(s => MatchesLanguage(s, targetSubLang) && !s.IsForced)
                               ?? subStreams.FirstOrDefault(s => MatchesLanguage(s, targetSubLang));
                    chosenSubIndex = regular?.Index ?? -1;
                }
                else if (effectiveMode == SubtitleModePreference.Smart)
                {
                    bool isForeignAudio = chosenAudio != null && !string.IsNullOrWhiteSpace(targetAudioLang) && !MatchesLanguage(chosenAudio, targetAudioLang);

                    if (isForeignAudio)
                    {
                        var regular = subStreams.FirstOrDefault(s => MatchesLanguage(s, targetSubLang) && !s.IsForced)
                                   ?? subStreams.FirstOrDefault(s => MatchesLanguage(s, targetSubLang));
                        chosenSubIndex = regular?.Index ?? -1;
                    }
                    else
                    {
                        var forced = subStreams.FirstOrDefault(s => MatchesLanguage(s, targetSubLang) && s.IsForced);
                        chosenSubIndex = forced?.Index ?? -1;
                    }
                }
            }

            source.DefaultSubtitleStreamIndex = chosenSubIndex;
            foreach (var s in subStreams)
            {
                s.IsDefault = (s.Index == chosenSubIndex);
            }
        }
    }

    private static bool MatchesLanguage(MediaStream stream, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(stream.Language))
        {
            if (!string.IsNullOrWhiteSpace(stream.Title) && LanguageCodeHelper.AreEquivalent(stream.Title, targetLang))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(stream.DisplayTitle) && LanguageCodeHelper.AreEquivalent(stream.DisplayTitle, targetLang))
            {
                return true;
            }

            return false;
        }

        return LanguageCodeHelper.AreEquivalent(stream.Language, targetLang);
    }
}
