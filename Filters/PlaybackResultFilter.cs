using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using Jellyfin.Plugin.DefaultSettings.Services;

namespace Jellyfin.Plugin.DefaultSettings.Filters;

/// <summary>
/// ASP.NET Core ResultFilter, der Antworten von /PlaybackInfo und /Items abfängt,
/// um Medienversionen sowie Ton- und Untertitelspuren serverseitig vorzubelegen.
/// Dadurch greifen die Voreinstellungen in ausnahmslos allen Jellyfin-Apps.
/// </summary>
public class PlaybackResultFilter : IAsyncResultFilter
{
    private readonly ILogger<PlaybackResultFilter> _logger;
    private readonly IDefaultSettingsManager _settingsManager;
    private readonly ILibraryManager _libraryManager;

    public PlaybackResultFilter(
        ILogger<PlaybackResultFilter> logger,
        IDefaultSettingsManager settingsManager,
        ILibraryManager libraryManager)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _libraryManager = libraryManager;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!_settingsManager.IsEnabled)
        {
            await next().ConfigureAwait(false);
            return;
        }

        try
        {
            if (context.Result is ObjectResult objectResult)
            {
                if (objectResult.Value is PlaybackInfoResponse playbackInfo && playbackInfo.MediaSources != null && playbackInfo.MediaSources.Count > 0)
                {
                    HandlePlaybackInfoResponse(context.HttpContext, playbackInfo);
                }
                else if (objectResult.Value is BaseItemDto itemDto && itemDto.MediaSources != null && itemDto.MediaSources.Length > 0)
                {
                    HandleBaseItemDto(context.HttpContext, itemDto);
                }
            }
        }
        catch (Exception ex)
        {
            // Fehler im Filter dürfen niemals die Wiedergabe oder den Server blockieren!
            _logger.LogError(ex, "Unerwarteter Fehler beim Anwenden der DefaultSettings im PlaybackResultFilter.");
        }

        await next().ConfigureAwait(false);
    }

    private void HandlePlaybackInfoResponse(HttpContext httpContext, PlaybackInfoResponse playbackInfo)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue)
        {
            return;
        }

        var itemId = ExtractItemId(httpContext);
        if (!itemId.HasValue)
        {
            return;
        }

        var item = _libraryManager.GetItemById(itemId.Value);
        Guid? seriesId = null;

        if (item is Episode episode)
        {
            seriesId = episode.SeriesId;
        }
        else if (item != null)
        {
            seriesId = item.FindParent<Series>()?.Id;
        }

        var userPref = _settingsManager.GetUserPreferences(userId.Value);
        var seriesOverride = seriesId.HasValue ? _settingsManager.GetSeriesOverride(userId.Value, seriesId.Value) : null;

        var sourcesList = new List<MediaSourceInfo>(playbackInfo.MediaSources);
        StreamSelectionHelper.ApplyPreferences(sourcesList, userPref, seriesOverride);

        playbackInfo.MediaSources = sourcesList;
        _logger.LogDebug("DefaultSettings angewendet auf PlaybackInfo für Item {ItemId} (User: {UserId}, Serie: {SeriesId}).",
            itemId.Value, userId.Value, seriesId);
    }

    private void HandleBaseItemDto(HttpContext httpContext, BaseItemDto itemDto)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue)
        {
            return;
        }

        Guid? seriesId = itemDto.SeriesId;
        if (!seriesId.HasValue && itemDto.Id != Guid.Empty)
        {
            var item = _libraryManager.GetItemById(itemDto.Id);
            if (item is Episode episode)
            {
                seriesId = episode.SeriesId;
            }
        }

        var userPref = _settingsManager.GetUserPreferences(userId.Value);
        var seriesOverride = seriesId.HasValue ? _settingsManager.GetSeriesOverride(userId.Value, seriesId.Value) : null;

        var sourcesList = new List<MediaSourceInfo>(itemDto.MediaSources);
        StreamSelectionHelper.ApplyPreferences(sourcesList, userPref, seriesOverride);

        itemDto.MediaSources = sourcesList.ToArray();
        _logger.LogDebug("DefaultSettings angewendet auf BaseItemDto für Item {ItemId} (User: {UserId}).", itemDto.Id, userId.Value);
    }

    private static Guid? ExtractUserId(HttpContext context)
    {
        // 1. Claims
        var claim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? context.User.FindFirst("UserId")?.Value;

        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var fromClaim))
        {
            return fromClaim;
        }

        // 2. Query String
        if (context.Request.Query.TryGetValue("UserId", out var qUserId) && Guid.TryParse(qUserId, out var fromQuery))
        {
            return fromQuery;
        }

        // 3. Route Values
        if (context.Request.RouteValues.TryGetValue("userId", out var rUserId) && Guid.TryParse(rUserId?.ToString(), out var fromRoute))
        {
            return fromRoute;
        }

        return null;
    }

    private static Guid? ExtractItemId(HttpContext context)
    {
        // 1. Route Values
        if (context.Request.RouteValues.TryGetValue("itemId", out var rItemId) && Guid.TryParse(rItemId?.ToString(), out var fromItemId))
        {
            return fromItemId;
        }

        if (context.Request.RouteValues.TryGetValue("id", out var rId) && Guid.TryParse(rId?.ToString(), out var fromId))
        {
            return fromId;
        }

        // 2. Query String
        if (context.Request.Query.TryGetValue("itemId", out var qItemId) && Guid.TryParse(qItemId, out var fromQueryItemId))
        {
            return fromQueryItemId;
        }

        if (context.Request.Query.TryGetValue("id", out var qId) && Guid.TryParse(qId, out var fromQueryId))
        {
            return fromQueryId;
        }

        return null;
    }
}
