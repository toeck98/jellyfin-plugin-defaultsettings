using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.DefaultSettings.Services;

/// <summary>
/// Hilfsklasse für den Vergleich und die Normalisierung von Sprachcodes (ISO 639-1 und ISO 639-2/B/T).
/// </summary>
public static class LanguageCodeHelper
{
    private static readonly Dictionary<string, string> StandardizedMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Deutsch
        { "de", "deu" },
        { "ger", "deu" },
        { "deu", "deu" },
        { "german", "deu" },
        { "deutsch", "deu" },

        // Englisch
        { "en", "eng" },
        { "eng", "eng" },
        { "english", "eng" },

        // Japanisch
        { "ja", "jpn" },
        { "jpn", "jpn" },
        { "japanese", "jpn" },

        // Französisch
        { "fr", "fra" },
        { "fre", "fra" },
        { "fra", "fra" },
        { "french", "fra" },

        // Spanisch
        { "es", "spa" },
        { "spa", "spa" },
        { "spanish", "spa" },

        // Italienisch
        { "it", "ita" },
        { "ita", "ita" },
        { "italian", "ita" },

        // Russisch
        { "ru", "rus" },
        { "rus", "rus" },
        { "russian", "rus" },

        // Chinesisch
        { "zh", "zho" },
        { "chi", "zho" },
        { "zho", "zho" },
        { "chinese", "zho" },

        // Koreanisch
        { "ko", "kor" },
        { "kor", "kor" },
        { "korean", "kor" },

        // Portugiesisch
        { "pt", "por" },
        { "por", "por" },
        { "portuguese", "por" },

        // Niederländisch
        { "nl", "nld" },
        { "dut", "nld" },
        { "nld", "nld" },
        { "dutch", "nld" },

        // Polnisch
        { "pl", "pol" },
        { "pol", "pol" },
        { "polish", "pol" },

        // Türkisch
        { "tr", "tur" },
        { "tur", "tur" },
        { "turkish", "tur" }
    };

    /// <summary>
    /// Normalisiert einen Sprachcode in einen kanonischen 3-Buchstaben-Code (ISO 639-2/T).
    /// </summary>
    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }

        var trimmed = language.Trim();
        if (StandardizedMap.TryGetValue(trimmed, out var canonical))
        {
            return canonical;
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Prüft, ob zwei Sprachangaben äquivalent sind.
    /// </summary>
    public static bool AreEquivalent(string? lang1, string? lang2)
    {
        if (string.IsNullOrWhiteSpace(lang1) || string.IsNullOrWhiteSpace(lang2))
        {
            return false;
        }

        var norm1 = Normalize(lang1);
        var norm2 = Normalize(lang2);

        return norm1.Equals(norm2, StringComparison.OrdinalIgnoreCase);
    }
}
