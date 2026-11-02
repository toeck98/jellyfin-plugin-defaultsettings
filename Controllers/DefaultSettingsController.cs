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
/// Entkoppelt von internen Jellyfin EF-Core Entities für volle Kompatibilität mit Jellyfin 12.4+.
/// </summary>
[ApiController]
[Authorize]
[Route("Plugins/DefaultSettings")]
[Produces(MediaTypeNames.Application.Json)]
public class DefaultSettingsController : ControllerBase
{
    private readonly ILogger<DefaultSettingsController> _logger;
    private readonly IDefaultSettingsManager _settingsManager;
    private readonly ILibraryManager _libraryManager;

    public DefaultSettingsController(
        ILogger<DefaultSettingsController> logger,
        IDefaultSettingsManager settingsManager,
        ILibraryManager libraryManager)
    {
        _logger = logger;
        _settingsManager = settingsManager;
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
    /// Ruft alle Benutzer des Systems ab (sofern konfiguriert) bzw. den aktuellen Aufrufer.
    /// GET: /Plugins/DefaultSettings/Users
    /// </summary>
    [HttpGet("Users")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<UserInfoDto>> GetUsers()
    {
        var callerId = GetCurrentUserId();
        if (!callerId.HasValue)
        {
            return Unauthorized("Benutzer konnte nicht authentifiziert werden.");
        }

        bool isAdmin = IsAdminCaller();
        var callerName = User.Identity?.Name ?? "Benutzer";

        if (!isAdmin)
        {
            return Ok(new List<UserInfoDto>
            {
                new() { Id = callerId.Value, Name = callerName, IsAdmin = false }
            });
        }

        // Für Administratoren: Alle bereits in DefaultSettings hinterlegten Benutzer auflisten
        var config = _settingsManager.GetConfiguration();
        var userList = new List<UserInfoDto>();
        var seenIds = new HashSet<Guid>();

        // Aktuellen Admin zuerst hinzufügen
        userList.Add(new UserInfoDto
        {
            Id = callerId.Value,
            Name = callerName + " (Du)",
            IsAdmin = true
        });
        seenIds.Add(callerId.Value);

        foreach (var pref in config.UserPreferences)
        {
            if (seenIds.Add(pref.UserId))
            {
                userList.Add(new UserInfoDto
                {
                    Id = pref.UserId,
                    Name = $"Benutzer ({pref.UserId.ToString()[..8]})",
                    IsAdmin = false
                });
            }
        }

        return Ok(userList);
    }

    /// <summary>
    /// Ruft die Einstellungen des Benutzers ab. Admins können targetUserId übergeben.
    /// GET: /Plugins/DefaultSettings/MyPreferences?targetUserId=...
    /// </summary>
    [HttpGet("MyPreferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<UserPreferencesDto> GetMyPreferences(
        [FromQuery] Guid? targetUserId = null,
        [FromQuery] Guid? userId = null)
    {
        var callerId = GetCurrentUserId();
        if (!callerId.HasValue)
        {
            return Unauthorized("Benutzer konnte nicht authentifiziert werden.");
        }

        bool isAdmin = IsAdminCaller();
        var effectiveUserId = callerId.Value;

        var requestedId = targetUserId ?? userId;
        if (requestedId.HasValue && isAdmin)
        {
            effectiveUserId = requestedId.Value;
        }

        var pref = _settingsManager.GetUserPreferences(effectiveUserId);

        return Ok(new UserPreferencesDto
        {
            UserId = effectiveUserId,
            Username = (effectiveUserId == callerId.Value) ? (User.Identity?.Name ?? "Benutzer") : effectiveUserId.ToString(),
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
    public ActionResult<List<SeriesInfoDto>> GetSeries(
        [FromQuery] Guid? targetUserId = null,
        [FromQuery] Guid? userId = null)
    {
        var seriesDict = new Dictionary<Guid, Series>();

        // Strategie 1: In-Memory Durchsuchung des Root-Folders
        try
        {
            var rootSeries = _libraryManager.RootFolder?.GetRecursiveChildren(i => i is Series);
            if (rootSeries != null)
            {
                foreach (var s in rootSeries.OfType<Series>())
                {
                    seriesDict[s.Id] = s;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Abrufen von Serien über RootFolder.");
        }

        // Strategie 2: Globale InternalItemsQuery
        try
        {
            var globalQuery = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Series },
                Recursive = true
            };
            var globalItems = _libraryManager.GetItemList(globalQuery);
            foreach (var s in globalItems.OfType<Series>())
            {
                seriesDict[s.Id] = s;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fehler beim Abrufen von Serien mit globaler Query.");
        }

        var result = seriesDict.Values
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
    /// Ruft den Override für eine bestimmte Serie des Nutzers ab. Gibt immer HTTP 200 mit Objekt zurück (auch wenn kein Override existiert).
    /// GET: /Plugins/DefaultSettings/Series/{seriesId}
    /// </summary>
    [HttpGet("Series/{seriesId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<SeriesPreferenceOverride> GetSeriesOverrideDirect(
        [FromRoute] Guid seriesId,
        [FromQuery] Guid? targetUserId = null,
        [FromQuery] Guid? userId = null)
    {
        var callerId = GetCurrentUserId();
        if (!callerId.HasValue)
        {
            return Unauthorized();
        }

        bool isAdmin = IsAdminCaller();
        var effectiveUserId = callerId.Value;

        var requestedId = targetUserId ?? userId;
        if (requestedId.HasValue && isAdmin)
        {
            effectiveUserId = requestedId.Value;
        }

        var overrideItem = _settingsManager.GetSeriesOverride(effectiveUserId, seriesId);
        if (overrideItem == null)
        {
            // Valides leeres DTO zurückgeben, damit der Client nicht auf HTTP 204 JSON-Parsing-Fehler läuft
            return Ok(new SeriesPreferenceOverride
            {
                SeriesId = seriesId,
                SeriesName = string.Empty,
                AudioLanguage = null,
                SubtitleLanguage = null,
                SubtitleMode = null,
                PreferredVersion = null
            });
        }

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
        var callerId = GetCurrentUserId();
        if (!callerId.HasValue)
        {
            return Unauthorized();
        }

        var seriesItem = _libraryManager.GetItemById(seriesId) as Series;
        if (seriesItem == null)
        {
            return NotFound("Serie nicht gefunden.");
        }

        // Zuverlässige Episoden-Suche: Erst rekursiv über seriesItem, Fallback über InternalItemsQuery mit AncestorIds
        var episodes = new List<Episode>();
        try
        {
            var children = seriesItem.GetRecursiveChildren(i => i is Episode);
            if (children != null)
            {
                episodes.AddRange(children.OfType<Episode>().Take(50));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fehler beim Laden von Episoden via GetRecursiveChildren.");
        }

        if (episodes.Count == 0)
        {
            try
            {
                var query = new InternalItemsQuery
                {
                    AncestorIds = new[] { seriesId },
                    IncludeItemTypes = new[] { BaseItemKind.Episode },
                    Recursive = true,
                    Limit = 50
                };
                episodes.AddRange(_libraryManager.GetItemList(query).OfType<Episode>());
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Fehler beim Laden von Episoden via AncestorIds Query.");
            }
        }

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

            foreach (var src in sources)
            {
                // Erkennung von benannten Versionen (wenn mehrere Quellen existieren oder explizit benannt sind)
                if (!string.IsNullOrWhiteSpace(src.Name) && !src.Name.Equals(ep.Name, StringComparison.OrdinalIgnoreCase))
                {
                    versionNames.Add(src.Name.Trim());
                }

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
    /// Speichert oder aktualisiert den Override für eine bestimmte Serie beim angegebenen bzw. aktuellen Benutzer.
    /// POST: /Plugins/DefaultSettings/Series/{seriesId}
    /// </summary>
    [HttpPost("Series/{seriesId}")]
    [HttpPost("MyPreferences/Series/{seriesId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult SaveSeriesOverride(
        [FromRoute] Guid seriesId,
        [FromBody] SaveSeriesOverrideRequest request,
        [FromQuery] Guid? targetUserId = null,
        [FromQuery] Guid? userId = null)
    {
        var callerId = GetCurrentUserId();
        if (!callerId.HasValue)
        {
            return Unauthorized();
        }

        bool isAdmin = IsAdminCaller();
        var effectiveUserId = callerId.Value;

        var requestedId = targetUserId ?? userId;
        if (requestedId.HasValue && isAdmin)
        {
            effectiveUserId = requestedId.Value;
        }

        var series = _libraryManager.GetItemById(seriesId) as Series;
        string seriesName = series?.Name ?? "Serie";

        var overrideItem = new SeriesPreferenceOverride
        {
            SeriesId = seriesId,
            SeriesName = seriesName,
            AudioLanguage = string.IsNullOrWhiteSpace(request.AudioLanguage) ? null : request.AudioLanguage.Trim(),
            SubtitleLanguage = string.IsNullOrWhiteSpace(request.SubtitleLanguage) ? null : request.SubtitleLanguage.Trim(),
            SubtitleMode = request.SubtitleMode,
            PreferredVersion = string.IsNullOrWhiteSpace(request.PreferredVersion) ? null : request.PreferredVersion.Trim()
        };

        _settingsManager.SaveSeriesOverride(effectiveUserId, overrideItem);
        return Ok(new { Success = true });
    }

    /// <summary>
    /// Entfernt den Override für eine bestimmte Serie beim angegebenen bzw. aktuellen Benutzer.
    /// DELETE: /Plugins/DefaultSettings/Series/{seriesId}
    /// DELETE: /Plugins/DefaultSettings/MyPreferences/Series/{seriesId}
    /// </summary>
    [HttpDelete("Series/{seriesId}")]
    [HttpDelete("MyPreferences/Series/{seriesId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult DeleteSeriesOverride(
        [FromRoute] Guid seriesId,
        [FromQuery] Guid? targetUserId = null,
        [FromQuery] Guid? userId = null)
    {
        var callerId = GetCurrentUserId();
        if (!callerId.HasValue)
        {
            return Unauthorized();
        }

        bool isAdmin = IsAdminCaller();
        var effectiveUserId = callerId.Value;

        var requestedId = targetUserId ?? userId;
        if (requestedId.HasValue && isAdmin)
        {
            effectiveUserId = requestedId.Value;
        }

        _settingsManager.DeleteSeriesOverride(effectiveUserId, seriesId);
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

    private Guid? GetCurrentUserId()
    {
        // 1. Primär: Jellyfin Standard-Claim "Jellyfin-UserId" (oder InternalClaimTypes.UserId)
        var jfUserIdClaim = User.FindFirst("Jellyfin-UserId")?.Value
                         ?? User.FindFirst("UserId")?.Value
                         ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(jfUserIdClaim) && Guid.TryParse(jfUserIdClaim, out var claimId))
        {
            return claimId;
        }

        // 2. Query String 'userId' oder 'targetUserId'
        if (Request.Query.TryGetValue("userId", out var qUserId) && Guid.TryParse(qUserId, out var parsedQueryId))
        {
            return parsedQueryId;
        }

        if (Request.Query.TryGetValue("targetUserId", out var qTargetUserId) && Guid.TryParse(qTargetUserId, out var parsedTargetId))
        {
            return parsedTargetId;
        }

        // 3. Header 'X-Emby-UserId'
        if (Request.Headers.TryGetValue("X-Emby-UserId", out var hUserId) && Guid.TryParse(hUserId, out var parsedHeaderId))
        {
            return parsedHeaderId;
        }

        return null;
    }

    private bool IsAdminCaller()
    {
        if (User.IsInRole("Administrator"))
        {
            return true;
        }

        if (User.HasClaim(ClaimTypes.Role, "Administrator"))
        {
            return true;
        }

        if (User.HasClaim("Jellyfin-IsApiKey", "True") || User.HasClaim("IsApiKey", "True"))
        {
            return true;
        }

        return false;
    }
}
