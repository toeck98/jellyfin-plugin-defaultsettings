using System;

namespace Jellyfin.Plugin.DefaultSettings.Controllers.Models;

/// <summary>
/// DTO für die Benutzerliste im Admin-Dashboard.
/// </summary>
public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}
