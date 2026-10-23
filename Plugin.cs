using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.DefaultSettings.Configuration;

namespace Jellyfin.Plugin.DefaultSettings;

/// <summary>
/// Haupteinstiegspunkt des Plugins DefaultSettings.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Eindeutiger Name des Plugins im Server.
    /// </summary>
    public override string Name => "DefaultSettings";

    /// <summary>
    /// Unveränderliche GUID dieses Plugins.
    /// </summary>
    public override Guid Id => Guid.Parse("2ff22cec-2c92-4428-a1b3-d1ff33748bf2");

    /// <summary>
    /// Statische Singleton-Instanz für einfachen Zugriff innerhalb des Plugins.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Registrierung der Dashboard-Webseite.
    /// </summary>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = "DefaultSettings",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
                EnableInMainMenu = true
            }
        };
    }
}
