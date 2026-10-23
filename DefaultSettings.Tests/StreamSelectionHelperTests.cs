using System.Collections.Generic;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Jellyfin.Plugin.DefaultSettings.Configuration;
using Jellyfin.Plugin.DefaultSettings.Services;
using Xunit;

namespace DefaultSettings.Tests;

public class StreamSelectionHelperTests
{
    [Fact]
    public void MultiVersion_ReordersToPreferredVersion_WhenFound()
    {
        // Arrange
        var sources = new List<MediaSourceInfo>
        {
            new MediaSourceInfo { Id = "1", Name = "1080p Schwarzweiss", MediaStreams = new List<MediaStream>() },
            new MediaSourceInfo { Id = "2", Name = "1080p Bunt", MediaStreams = new List<MediaStream>() }
        };

        var seriesOverride = new SeriesPreferenceOverride
        {
            PreferredVersion = "Bunt"
        };

        // Act
        StreamSelectionHelper.ApplyPreferences(sources, null, seriesOverride);

        // Assert
        Assert.Equal("2", sources[0].Id);
        Assert.Equal("1080p Bunt", sources[0].Name);
    }

    [Fact]
    public void MultiVersion_GracefulFallback_WhenPreferredVersionNotFound()
    {
        // Arrange
        var sources = new List<MediaSourceInfo>
        {
            new MediaSourceInfo { Id = "1", Name = "1080p Schwarzweiss", MediaStreams = new List<MediaStream>() }
        };

        var seriesOverride = new SeriesPreferenceOverride
        {
            PreferredVersion = "Bunt"
        };

        // Act
        StreamSelectionHelper.ApplyPreferences(sources, null, seriesOverride);

        // Assert: Unchanged order, no exception
        Assert.Single(sources);
        Assert.Equal("1", sources[0].Id);
    }

    [Fact]
    public void AudioSelection_SelectsMatchingLanguage()
    {
        // Arrange
        var sources = new List<MediaSourceInfo>
        {
            new MediaSourceInfo
            {
                Id = "1",
                DefaultAudioStreamIndex = 1,
                MediaStreams = new List<MediaStream>
                {
                    new MediaStream { Index = 0, Type = MediaStreamType.Video },
                    new MediaStream { Index = 1, Type = MediaStreamType.Audio, Language = "eng", IsDefault = true },
                    new MediaStream { Index = 2, Type = MediaStreamType.Audio, Language = "deu", IsDefault = false }
                }
            }
        };

        var seriesOverride = new SeriesPreferenceOverride
        {
            AudioLanguage = "deu"
        };

        // Act
        StreamSelectionHelper.ApplyPreferences(sources, null, seriesOverride);

        // Assert: Audio stream index 2 (deu) must be selected
        Assert.Equal(2, sources[0].DefaultAudioStreamIndex);
    }

    [Fact]
    public void AudioSelection_FallbackToContainerDefault_WhenLanguageNotFound()
    {
        // Arrange
        var sources = new List<MediaSourceInfo>
        {
            new MediaSourceInfo
            {
                Id = "1",
                DefaultAudioStreamIndex = 1,
                MediaStreams = new List<MediaStream>
                {
                    new MediaStream { Index = 0, Type = MediaStreamType.Video },
                    new MediaStream { Index = 1, Type = MediaStreamType.Audio, Language = "fra", IsDefault = true },
                    new MediaStream { Index = 2, Type = MediaStreamType.Audio, Language = "spa", IsDefault = false }
                }
            }
        };

        var seriesOverride = new SeriesPreferenceOverride
        {
            AudioLanguage = "jpn"
        };

        // Act
        StreamSelectionHelper.ApplyPreferences(sources, null, seriesOverride);

        // Assert: Container default (index 1) remains selected!
        Assert.Equal(1, sources[0].DefaultAudioStreamIndex);
    }

    [Fact]
    public void SubtitleSelection_OnlyForced_SelectsForcedSubtitle()
    {
        // Arrange
        var sources = new List<MediaSourceInfo>
        {
            new MediaSourceInfo
            {
                Id = "1",
                MediaStreams = new List<MediaStream>
                {
                    new MediaStream { Index = 1, Type = MediaStreamType.Audio, Language = "deu" },
                    new MediaStream { Index = 2, Type = MediaStreamType.Subtitle, Language = "deu", IsForced = false },
                    new MediaStream { Index = 3, Type = MediaStreamType.Subtitle, Language = "deu", IsForced = true }
                }
            }
        };

        var seriesOverride = new SeriesPreferenceOverride
        {
            SubtitleLanguage = "deu",
            SubtitleMode = SubtitleModePreference.OnlyForced
        };

        // Act
        StreamSelectionHelper.ApplyPreferences(sources, null, seriesOverride);

        // Assert: Index 3 (IsForced = true) is selected
        Assert.Equal(3, sources[0].DefaultSubtitleStreamIndex);
    }

    [Fact]
    public void SubtitleSelection_OnlyForced_DisablesWhenNoForcedExists()
    {
        // Arrange
        var sources = new List<MediaSourceInfo>
        {
            new MediaSourceInfo
            {
                Id = "1",
                MediaStreams = new List<MediaStream>
                {
                    new MediaStream { Index = 1, Type = MediaStreamType.Audio, Language = "deu" },
                    new MediaStream { Index = 2, Type = MediaStreamType.Subtitle, Language = "deu", IsForced = false }
                }
            }
        };

        var seriesOverride = new SeriesPreferenceOverride
        {
            SubtitleLanguage = "deu",
            SubtitleMode = SubtitleModePreference.OnlyForced
        };

        // Act
        StreamSelectionHelper.ApplyPreferences(sources, null, seriesOverride);

        // Assert: Disabled (-1) because no forced subtitle is present
        Assert.Equal(-1, sources[0].DefaultSubtitleStreamIndex);
    }

    [Fact]
    public void SubtitleSelection_SmartMode_ActivatesFullSub_WhenForeignAudio()
    {
        // Arrange: Anime with Japanese audio, series override wants German audio + smart sub
        var sources = new List<MediaSourceInfo>
        {
            new MediaSourceInfo
            {
                Id = "1",
                MediaStreams = new List<MediaStream>
                {
                    new MediaStream { Index = 1, Type = MediaStreamType.Audio, Language = "jpn", IsDefault = true },
                    new MediaStream { Index = 2, Type = MediaStreamType.Subtitle, Language = "deu", IsForced = false },
                    new MediaStream { Index = 3, Type = MediaStreamType.Subtitle, Language = "deu", IsForced = true }
                }
            }
        };

        var seriesOverride = new SeriesPreferenceOverride
        {
            AudioLanguage = "deu",
            SubtitleLanguage = "deu",
            SubtitleMode = SubtitleModePreference.Smart
        };

        // Act
        StreamSelectionHelper.ApplyPreferences(sources, null, seriesOverride);

        // Assert: Because audio is Japanese (foreign to target deu), full subtitle (index 2) is chosen!
        Assert.Equal(2, sources[0].DefaultSubtitleStreamIndex);
    }

    [Fact]
    public void NoOverride_LeavesJellyfinDefaultsUntouched()
    {
        // Arrange
        var sources = new List<MediaSourceInfo>
        {
            new MediaSourceInfo
            {
                Id = "1",
                DefaultAudioStreamIndex = 5,
                DefaultSubtitleStreamIndex = 10,
                MediaStreams = new List<MediaStream>()
            }
        };

        // Act: No override configured
        StreamSelectionHelper.ApplyPreferences(sources, null, null);

        // Assert: Indices must remain completely untouched!
        Assert.Equal(5, sources[0].DefaultAudioStreamIndex);
        Assert.Equal(10, sources[0].DefaultSubtitleStreamIndex);
    }
}
