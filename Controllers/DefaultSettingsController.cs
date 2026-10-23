using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Security.Claims;
using System.Text;
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
    /// Liefert das JavaScript-Skript aus, welches den Einstellungs-Button direkt in die Serien-Detailseite im Web-Client einbettet.
    /// GET: /Plugins/DefaultSettings/ClientScript
    /// </summary>
    [HttpGet("ClientScript")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public ActionResult GetClientScript()
    {
        var asm = typeof(Plugin).Assembly;
        using var stream = asm.GetManifestResourceStream("Jellyfin.Plugin.DefaultSettings.Web.clientScript.js");
        if (stream == null)
        {
            return NotFound("Client-Skript Ressource nicht gefunden.");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var script = reader.ReadToEnd();
        return Content(script, "application/javascript", Encoding.UTF8);
    }

    /// <summary>
    /// Ruft die Einstellungen des aktuell angemeldeten Benutzers ab.
    /// GET: /Plugins/DefaultSettings/MyPreferences
    /// </summary>
    [HttpGet("MyPreferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
    /// Ruft die Liste aller Serien der Mediathek ab.
    /// GET: /Plugins/DefaultSettings/Series
    /// </summary>
    [HttpGet("Series")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<SeriesInfoDto>> GetSeries()
    {
        var query = new InternalItemsQuery
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
    /// Ruft den Override für eine bestimmte Serie des aktuellen Nutzers ab.
    /// GET: /Plugins/DefaultSettings/Series/{seriesId}
    /// </summary>
    [HttpGet("Series/{seriesId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<SeriesPreferenceOverride?> GetSeriesOverrideDirect([FromRoute] Guid seriesId)
    {
        var user = GetCurrentUser();
        if (user == null)
        {
            return Unauthorized();
        }

        var overrideItem = _settingsManager.GetSeriesOverride(user.Id, seriesId);
        return Ok(overrideItem);
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

        var query = new InternalItemsQuery
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
    /// POST: /Plugins/DefaultSettings/Series/{seriesId}
    /// </summary>
    [HttpPost("Series/{seriesId}")]
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
    /// DELETE: /Plugins/DefaultSettings/Series/{seriesId}
    /// DELETE: /Plugins/DefaultSettings/MyPreferences/Series/{seriesId}
    /// </summary>
    [HttpDelete("Series/{seriesId}")]
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

    private User? GetCurrentUser()
    {
        // 1. Alle Claims nach einer gültigen User-GUID durchsuchen
        foreach (var claim in User.Claims)
        {
            if (Guid.TryParse(claim.Value, out var fromClaimId))
            {
                var u = _userManager.GetUserById(fromClaimId);
                if (u != null) return u;
            }
        }

        // 2. Suche über Identity Name / Username
        if (!string.IsNullOrEmpty(User.Identity?.Name))
        {
            var u = _userManager.GetUserByName(User.Identity.Name);
            if (u != null) return u;
        }

        // 3. Suche über Query String 'userId' (vom Web-Client explizit übergeben)
        if (Request.Query.TryGetValue("userId", out var qUserId) && Guid.TryParse(qUserId, out var qId))
        {
            var u = _userManager.GetUserById(qId);
            if (u != null) return u;
        }

        // 4. Suche über Header 'X-Emby-UserId'
        if (Request.Headers.TryGetValue("X-Emby-UserId", out var hUserId) && Guid.TryParse(hUserId, out var hId))
        {
            var u = _userManager.GetUserById(hId);
            if (u != null) return u;
        }

        // 5. Fallback: Erster konfigurierter Administrator/Nutzer
        return _userManager.Users.FirstOrDefault();
    }
}
