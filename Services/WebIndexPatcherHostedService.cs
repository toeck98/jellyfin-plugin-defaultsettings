using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DefaultSettings.Services;

/// <summary>
/// Hintergrunddienst, der beim Serverstart die statische index.html von jellyfin-web prüft
/// und das Client-Skript direkt einbettet, falls Schreibrechte auf das Web-Verzeichnis vorliegen.
/// Dadurch funktioniert das Skript selbst bei statischem File-Serving und aggressivem Browser-Caching.
/// </summary>
public class WebIndexPatcherHostedService : IHostedService
{
    private readonly ILogger<WebIndexPatcherHostedService> _logger;
    private readonly IApplicationPaths _applicationPaths;
    private const string ScriptTag = "<script defer src=\"/Plugins/DefaultSettings/ClientScript\"></script>";

    public WebIndexPatcherHostedService(
        ILogger<WebIndexPatcherHostedService> logger,
        IApplicationPaths applicationPaths)
    {
        _logger = logger;
        _applicationPaths = applicationPaths;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var webDir = _applicationPaths.WebPath;
            if (string.IsNullOrWhiteSpace(webDir) || !Directory.Exists(webDir))
            {
                return Task.CompletedTask;
            }

            var indexPath = Path.Combine(webDir, "index.html");
            if (!File.Exists(indexPath))
            {
                return Task.CompletedTask;
            }

            var html = File.ReadAllText(indexPath);
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

                File.WriteAllText(indexPath, html);
                _logger.LogInformation("DefaultSettings Client-Skript wurde permanent in '{IndexPath}' eingetragen.", indexPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "index.html konnte nicht direkt gepatcht werden (z. B. Read-Only Dateisystem). Fallback auf Middleware aktiv.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
