using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DefaultSettings.Filters;

/// <summary>
/// ASP.NET Core Middleware, die das Client-Skript für den Einstellungs-Button
/// transparent in die Jellyfin Web-Oberfläche (index.html) einbettet.
/// </summary>
public class WebInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebInjectionMiddleware> _logger;
    private const string ScriptTag = "<script defer src=\"/Plugins/DefaultSettings/ClientScript\"></script>";

    public WebInjectionMiddleware(RequestDelegate next, ILogger<WebInjectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Prüfe, ob es sich um den Aufruf der Jellyfin Web-Startseite / index.html handelt
        bool isWebIndex = path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
                          path.Equals("/web", StringComparison.OrdinalIgnoreCase) ||
                          path.Equals("/web/", StringComparison.OrdinalIgnoreCase) ||
                          path.EndsWith("index.html", StringComparison.OrdinalIgnoreCase) ||
                          path.EndsWith(".html", StringComparison.OrdinalIgnoreCase);

        if (!isWebIndex)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // 1. Verhindere, dass der Server die index.html komprimiert oder mit 304 Not Modified quittiert
        context.Request.Headers.Remove("Accept-Encoding");
        context.Request.Headers["Accept-Encoding"] = "identity";
        context.Request.Headers.Remove("If-None-Match");
        context.Request.Headers.Remove("If-Modified-Since");

        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context).ConfigureAwait(false);

            if (context.Response.StatusCode == 200 &&
                (context.Response.ContentType == null || context.Response.ContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
            {
                memoryStream.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(memoryStream, Encoding.UTF8, leaveOpen: true);
                var html = await reader.ReadToEndAsync().ConfigureAwait(false);

                if (!html.Contains("/Plugins/DefaultSettings/ClientScript", StringComparison.OrdinalIgnoreCase))
                {
                    if (Regex.IsMatch(html, "</body>", RegexOptions.IgnoreCase))
                    {
                        html = Regex.Replace(html, "</body>", ScriptTag + "</body>", RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        html += ScriptTag;
                    }

                    _logger.LogDebug("DefaultSettings Client-Skript wurde erfolgreich in index.html injiziert.");
                }

                var modifiedBytes = Encoding.UTF8.GetBytes(html);

                // Cache-Busting für die injizierte index.html
                context.Response.Headers.Remove("ETag");
                context.Response.Headers.Remove("Last-Modified");
                context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";

                context.Response.Body = originalBodyStream;
                context.Response.ContentLength = modifiedBytes.Length;
                await originalBodyStream.WriteAsync(modifiedBytes).ConfigureAwait(false);
            }
            else
            {
                memoryStream.Seek(0, SeekOrigin.Begin);
                context.Response.Body = originalBodyStream;
                await memoryStream.CopyToAsync(originalBodyStream).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Injizieren des DefaultSettings Client-Skripts in index.html.");
            memoryStream.Seek(0, SeekOrigin.Begin);
            context.Response.Body = originalBodyStream;
            await memoryStream.CopyToAsync(originalBodyStream).ConfigureAwait(false);
        }
    }
}
