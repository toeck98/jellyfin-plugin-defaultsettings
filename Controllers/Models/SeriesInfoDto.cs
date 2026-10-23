using System;

namespace Jellyfin.Plugin.DefaultSettings.Controllers.Models;

/// <summary>
/// DTO für die Auflistung von Serien im Dashboard.
/// </summary>
public class SeriesInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SortName { get; set; }
    public string? PrimaryImageUrl { get; set; }
}
