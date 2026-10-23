using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.DefaultSettings.Filters;
using Jellyfin.Plugin.DefaultSettings.Services;

namespace Jellyfin.Plugin.DefaultSettings;

/// <summary>
/// Registriert eigene Dienste und den PlaybackResultFilter in Jellyfins DI-Container und MVC-Pipeline.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // 1. Singleton-Manager für Plugin- und Benutzereinstellungen
        serviceCollection.AddSingleton<IDefaultSettingsManager, DefaultSettingsManager>();

        // 2. Playback-Filter für die serverseitige Interzeption von PlaybackInfo und Item-DTOs
        serviceCollection.AddTransient<PlaybackResultFilter>();

        // 3. Filter global in MVC-Pipeline eintragen
        serviceCollection.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<PlaybackResultFilter>();
        });
    }
}
