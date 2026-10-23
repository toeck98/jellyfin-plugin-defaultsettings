using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Jellyfin.Plugin.DefaultSettings.Configuration;
using Jellyfin.Plugin.DefaultSettings.Controllers.Models;
using Jellyfin.Plugin.DefaultSettings.Services;

namespace Jellyfin.Plugin.DefaultSettings.Controllers;

/// <summary>
/// REST-API Controller für die Konfiguration von DefaultSettings.
/// </summary>
[ApiController]
[Authorize]
[Route("Plugins/DefaultSettings")]
[Produces(MediaTypeNames.Application.Json)]
public class DefaultSettingsController : ControllerBase
{
    private readonly ILogger<DefaultSettingsController> _logger;
    private readonly IDefaultSettingsManager _settingsManager;
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;

    public DefaultSettingsController(
        ILogger<DefaultSettingsController> logger,
        IDefaultSettingsManager settingsManager,
        IUserManager userManager,
        ILibraryManager libraryManager)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _userManager = userManager;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Ruft die Einstellungen des aktuell angemeldeten Benutzers ab.
    /// GET: /Plugins/DefaultSettings/MyPreferences
    /// </summary>
    [HttpGet("MyPreferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UserPreferencesDto> GetMyPreferences()
    {
        var user = GetCurrentUser();
        if (user == null)
        {
            return Unauthorized("Benutzer konnte nicht authentifiziert werden.");
        }

        var pref = _settingsManager.GetUserPreferences(user.Id);
        bool isAdmin = user.HasPermission(PermissionKind.IsAdministrator);

        return Ok(new UserPreferencesDto
        {
            UserId = user.Id,
            Username = user.Username,
            IsAdmin = isAdmin,
            PreferredAudioLanguage = pref.PreferredAudioLanguage,
            PreferredSubtitleLanguage = pref.PreferredSubtitleLanguage,
            SubtitleMode = pref.SubtitleMode,
            PreferredVersionKeyword = pref.PreferredVersionKeyword,
            SeriesOverrides = pref.SeriesOverrides ?? new List<SeriesPreferenceOverride>()
        });
    }

    /// <summary>
    /// Speichert die globalen Einstellungen des aktuell angemeldeten Benutzers.
    /// POST: /Plugins/DefaultSettings/MyPreferences
    /// </summary>
    [HttpPost("MyPreferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult SaveMyPreferences([FromBody] SaveUserPreferencesRequest request)
    {
        var user = GetCurrentUser();
        if (user == null)
        {
            return Unauthorized("Benutzer konnte nicht authentifiziert werden.");
        }

        var pref = _settingsManager.GetUserPreferences(user.Id);
        pref.PreferredAudioLanguage = request.PreferredAudioLanguage ?? "deu";
        pref.PreferredSubtitleLanguage = request.PreferredSubtitleLanguage ?? "deu";
        pref.SubtitleMode = request.SubtitleMode;
        pref.PreferredVersionKeyword = request.PreferredVersionKeyword ?? string.Empty;

        _settingsManager.SaveUserPreferences(pref);
        return Ok(new { Success = true });
    }

    /// <summary>
    /// Ruft die Liste aller Serien ab, auf die der Benutzer Zugriff hat.
    /// GET: /Plugins/DefaultSettings/Series
    /// </summary>
    [HttpGet("Series")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<List<SeriesInfoDto>> GetSeries()
    {
        var user = GetCurrentUser();
        if (user == null)
        {
            return Unauthorized("Benutzer konnte nicht authentifiziert werden.");
        }

        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Series },
            Recursive = true
        };

        var items = _libraryManager.GetItemList(query);
        var result = items
            .OfType<Series>()
            .Select(s => new SeriesInfoDto
            {
                Id = s.Id,
                Name = s.Name,
                SortName = s.SortName
            })
            .OrderBy(s => s.SortName ?? s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Analysiert Episoden der Serie und gibt erkannte Versionen, Ton- und Untertitelspuren zurück.
    /// GET: /Plugins/DefaultSettings/Series/{seriesId}/Metadata
    /// </summary>
    [HttpGet("Series/{seriesId}/Metadata")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<SeriesMetadataDto> GetSeriesMetadata([FromRoute] Guid seriesId)
    {
        var user = GetCurrentUser();
        if (user == null)
        {
            return Unauthorized();
        }

        var seriesItem = _libraryManager.GetItemById(seriesId) as Series;
        if (seriesItem == null)
        {
            return NotFound("Serie nicht gefunden.");
        }

        var query = new InternalItemsQuery(user)
        {
            ParentId = seriesId,
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            Recursive = true,
            Limit = 50 // Begrenzung zur Schonung von Ressourcen bei sehr langen Serien
        };

        var episodes = _libraryManager.GetItemList(query).OfType<Episode>().ToList();

        var versionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var audioLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var subLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ep in episodes)
        {
            var sources = ep.GetMediaSources(false);
            if (sources == null)
            {
                continue;
            }

            // Erkennung mehrerer Versionen
            if (sources.Count > 1)
            {
                foreach (var src in sources)
                {
                    if (!string.IsNullOrWhiteSpace(src.Name))
                    {
                        versionNames.Add(src.Name.Trim());
                    }
                }
            }

            // Erkennung von Audio- und Untertitel-Sprachen
            foreach (var src in sources)
            {
                if (src.MediaStreams == null) continue;

                foreach (var stream in src.MediaStreams)
                {
                    if (stream.Type == MediaStreamType.Audio && !string.IsNullOrWhiteSpace(stream.Language))
                    {
                        audioLanguages.Add(LanguageCodeHelper.Normalize(stream.Language));
                    }
                    else if (stream.Type == MediaStreamType.Subtitle && !string.IsNullOrWhiteSpace(stream.Language))
                    {
                        subLanguages.Add(LanguageCodeHelper.Normalize(stream.Language));
                    }
                }
            }
        }

        return Ok(new SeriesMetadataDto
        {
            SeriesId = seriesId,
            SeriesName = seriesItem.Name,
            AvailableVersions = versionNames.OrderBy(v => v).ToList(),
            AvailableAudioLanguages = audioLanguages.OrderBy(a => a).ToList(),
            AvailableSubtitleLanguages = subLanguages.OrderBy(s => s).ToList()
        });
    }

    /// <summary>
    /// Speichert oder aktualisiert den Override für eine bestimmte Serie beim aktuellen Benutzer.
    /// POST: /Plugins/DefaultSettings/MyPreferences/Series/{seriesId}
    /// </summary>
    [HttpPost("MyPreferences/Series/{seriesId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult SaveSeriesOverride([FromRoute] Guid seriesId, [FromBody] SaveSeriesOverrideRequest request)
    {
        var user = GetCurrentUser();
        if (user == null)
        {
            return Unauthorized();
        }

        var series = _libraryManager.GetItemById(seriesId) as Series;
        if (series == null)
        {
            return NotFound("Serie wurde nicht gefunden.");
        }

        var overrideItem = new SeriesPreferenceOverride
        {
            SeriesId = seriesId,
            SeriesName = series.Name,
            AudioLanguage = string.IsNullOrWhiteSpace(request.AudioLanguage) ? null : request.AudioLanguage.Trim(),
            SubtitleLanguage = string.IsNullOrWhiteSpace(request.SubtitleLanguage) ? null : request.SubtitleLanguage.Trim(),
            SubtitleMode = request.SubtitleMode,
            PreferredVersion = string.IsNullOrWhiteSpace(request.PreferredVersion) ? null : request.PreferredVersion.Trim()
        };

        _settingsManager.SaveSeriesOverride(user.Id, overrideItem);
        return Ok(new { Success = true });
    }

    /// <summary>
    /// Entfernt den Override für eine bestimmte Serie beim aktuellen Benutzer.
    /// DELETE: /Plugins/DefaultSettings/MyPreferences/Series/{seriesId}
    /// </summary>
    [HttpDelete("MyPreferences/Series/{seriesId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult DeleteSeriesOverride([FromRoute] Guid seriesId)
    {
        var user = GetCurrentUser();
        if (user == null)
        {
            return Unauthorized();
        }

        _settingsManager.DeleteSeriesOverride(user.Id, seriesId);
        return Ok(new { Success = true });
    }

    /// <summary>
    /// Admin-Endpunkt: Ruft die gesamte Plugin-Konfiguration ab.
    /// GET: /Plugins/DefaultSettings/Configuration
    /// </summary>
    [HttpGet("Configuration")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<PluginConfiguration> GetConfiguration()
    {
        return Ok(_settingsManager.GetConfiguration());
    }

    /// <summary>
    /// Admin-Endpunkt: Speichert die gesamte Plugin-Konfiguration.
    /// POST: /Plugins/DefaultSettings/Configuration
    /// </summary>
    [HttpPost("Configuration")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult SaveConfiguration([FromBody] PluginConfiguration config)
    {
        _settingsManager.UpdateConfiguration(config);
        return Ok(new { Success = true });
    }

    /// <summary>
    /// Admin-Endpunkt: Listet alle registrierten Server-Benutzer auf.
    /// GET: /Plugins/DefaultSettings/Users
    /// </summary>
    [HttpGet("Users")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<object>> GetUsers()
    {
        var users = _userManager.Users
            .Select(u => new
            {
                Id = u.Id,
                Username = u.Username
            })
            .OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .ToList<object>();

        return Ok(users);
    }

    /// <summary>
    /// Admin-Endpunkt: Ruft die Einstellungen für einen beliebigen Benutzer ab.
    /// GET: /Plugins/DefaultSettings/Users/{userId}/Preferences
    /// </summary>
    [HttpGet("Users/{userId}/Preferences")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<UserPreferencesDto> GetUserPreferences([FromRoute] Guid userId)
    {
        var targetUser = _userManager.GetUserById(userId);
        if (targetUser == null)
        {
            return NotFound("Benutzer nicht gefunden.");
        }

        var pref = _settingsManager.GetUserPreferences(userId);
        return Ok(new UserPreferencesDto
        {
            UserId = targetUser.Id,
            Username = targetUser.Username,
            IsAdmin = targetUser.HasPermission(PermissionKind.IsAdministrator),
            PreferredAudioLanguage = pref.PreferredAudioLanguage,
            PreferredSubtitleLanguage = pref.PreferredSubtitleLanguage,
            SubtitleMode = pref.SubtitleMode,
            PreferredVersionKeyword = pref.PreferredVersionKeyword,
            SeriesOverrides = pref.SeriesOverrides ?? new List<SeriesPreferenceOverride>()
        });
    }

    /// <summary>
    /// Admin-Endpunkt: Speichert die Einstellungen für einen beliebigen Benutzer.
    /// POST: /Plugins/DefaultSettings/Users/{userId}/Preferences
    /// </summary>
    [HttpPost("Users/{userId}/Preferences")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult SaveUserPreferencesAdmin([FromRoute] Guid userId, [FromBody] UserDefaultPreference preferences)
    {
        var targetUser = _userManager.GetUserById(userId);
        if (targetUser == null)
        {
            return NotFound("Benutzer nicht gefunden.");
        }

        preferences.UserId = userId;
        _settingsManager.SaveUserPreferences(preferences);
        return Ok(new { Success = true });
    }

    private User? GetCurrentUser()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("UserId")?.Value;

        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var userId))
        {
            return _userManager.GetUserById(userId);
        }

        return null;
    }
}
