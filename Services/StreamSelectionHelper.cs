using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Jellyfin.Plugin.DefaultSettings.Configuration;

namespace Jellyfin.Plugin.DefaultSettings.Services;

/// <summary>
/// Logik zur Berechnung und Anwendung von Sprach-, Untertitel- und Versions-Voreinstellungen auf MediaSources.
/// </summary>
public static class StreamSelectionHelper
{
    /// <summary>
    /// Wendet die konfigurierten Voreinstellungen des Nutzers auf die Liste der MediaSources an.
    /// </summary>
    public static void ApplyPreferences(
        IList<MediaSourceInfo> mediaSources,
        UserDefaultPreference userPref,
        SeriesPreferenceOverride? seriesOverride)
    {
        if (mediaSources == null || mediaSources.Count == 0)
        {
            return;
        }

        // 1. Versionen / MediaSources priorisieren (Multi-Version Handling)
        var preferredVersion = seriesOverride?.PreferredVersion;
        if (string.IsNullOrWhiteSpace(preferredVersion))
        {
            preferredVersion = userPref.PreferredVersionKeyword;
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

            // Wenn die Version gefunden wurde und nicht bereits an Index 0 liegt: nach vorne ziehen
            if (matchIndex > 0)
            {
                var matchedSource = mediaSources[matchIndex];
                mediaSources.RemoveAt(matchIndex);
                mediaSources.Insert(0, matchedSource);
            }
            // Falls Wunschversion bei dieser Folge nicht vorhanden: Jellyfin Standard beibehalten
        }

        // 2. Audio- und Untertitel-Spuren für alle verfügbaren Quellen konfigurieren (primär Index 0)
        var targetAudioLang = seriesOverride?.AudioLanguage;
        if (string.IsNullOrWhiteSpace(targetAudioLang))
        {
            targetAudioLang = userPref.PreferredAudioLanguage;
        }

        var targetSubLang = seriesOverride?.SubtitleLanguage;
        if (string.IsNullOrWhiteSpace(targetSubLang))
        {
            targetSubLang = userPref.PreferredSubtitleLanguage;
        }

        var subMode = seriesOverride?.SubtitleMode ?? userPref.SubtitleMode;

        // Wir wenden die Logik auf jede MediaSource an, damit auch bei manuellem Versionswechsel im Client die Spuren passen
        foreach (var source in mediaSources)
        {
            ApplyStreamsToSource(source, targetAudioLang, targetSubLang, subMode);
        }
    }

    private static void ApplyStreamsToSource(
        MediaSourceInfo source,
        string targetAudioLang,
        string targetSubLang,
        SubtitleModePreference subMode)
    {
        if (source.MediaStreams == null || source.MediaStreams.Count == 0)
        {
            return;
        }

        var audioStreams = source.MediaStreams.Where(s => s.Type == MediaStreamType.Audio).ToList();
        var subStreams = source.MediaStreams.Where(s => s.Type == MediaStreamType.Subtitle).ToList();

        // --- Audio Stream Auswahl ---
        MediaStream? chosenAudio = null;
        if (!string.IsNullOrWhiteSpace(targetAudioLang) && audioStreams.Count > 0)
        {
            // 1. Bevorzuge Spur mit passender Sprache, die bereits IsDefault ist
            chosenAudio = audioStreams.FirstOrDefault(s => MatchesLanguage(s, targetAudioLang) && s.IsDefault);

            // 2. Beliebige Spur mit passender Sprache
            chosenAudio ??= audioStreams
                .Where(s => MatchesLanguage(s, targetAudioLang))
                .OrderByDescending(s => s.Channels ?? 2)
                .FirstOrDefault();
        }

        if (chosenAudio != null)
        {
            source.DefaultAudioStreamIndex = chosenAudio.Index;
            foreach (var a in audioStreams)
            {
                a.IsDefault = (a.Index == chosenAudio.Index);
            }
        }
        else
        {
            // Wenn kein Match vorhanden ist: Behalte bestehende Container-Standardspur bei
            var existingDefault = audioStreams.FirstOrDefault(a => a.IsDefault) ?? audioStreams.FirstOrDefault();
            if (existingDefault != null)
            {
                chosenAudio = existingDefault;
            }
        }

        // --- Untertitel Stream Auswahl ---
        int chosenSubIndex = -1;

        if (subMode == SubtitleModePreference.None || subStreams.Count == 0)
        {
            chosenSubIndex = -1;
        }
        else if (subMode == SubtitleModePreference.OnlyForced)
        {
            var forced = subStreams.FirstOrDefault(s => MatchesLanguage(s, targetSubLang) && s.IsForced);
            chosenSubIndex = forced?.Index ?? -1;
        }
        else if (subMode == SubtitleModePreference.Always)
        {
            var regular = subStreams.FirstOrDefault(s => MatchesLanguage(s, targetSubLang) && !s.IsForced)
                       ?? subStreams.FirstOrDefault(s => MatchesLanguage(s, targetSubLang));
            chosenSubIndex = regular?.Index ?? -1;
        }
        else if (subMode == SubtitleModePreference.Smart)
        {
            // Smart: Wenn gewählte Audiospur fremdsprachig ist -> vollständige Untertitel
            // Wenn gewählte Audiospur bereits die Zielsprache ist -> nur Forced (falls vorhanden), sonst keine
            bool isForeignAudio = chosenAudio != null && !MatchesLanguage(chosenAudio, targetAudioLang);

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

        source.DefaultSubtitleStreamIndex = chosenSubIndex;
        foreach (var s in subStreams)
        {
            s.IsDefault = (s.Index == chosenSubIndex);
        }
    }

    private static bool MatchesLanguage(MediaStream stream, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(stream.Language))
        {
            // Manche Dateien haben Titel wie "German" oder "Deutsch", aber keinen Language-Tag
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
