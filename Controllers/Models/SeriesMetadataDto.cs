using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.DefaultSettings.Controllers.Models;

/// <summary>
/// DTO mit automatisch ermittelten Versionen, Tonspuren und Untertitelsprachen einer Serie.
/// </summary>
public class SeriesMetadataDto
{
    public Guid SeriesId { get; set; }
    public string SeriesName { get; set; } = string.Empty;
    public List<string> AvailableVersions { get; set; } = new();
    public List<string> AvailableAudioLanguages { get; set; } = new();
    public List<string> AvailableSubtitleLanguages { get; set; } = new();
}
