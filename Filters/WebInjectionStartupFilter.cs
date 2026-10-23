using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Jellyfin.Plugin.DefaultSettings.Filters;

/// <summary>
/// Registriert die WebInjectionMiddleware in der ASP.NET Core Pipeline von Jellyfin.
/// </summary>
public class WebInjectionStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            builder.UseMiddleware<WebInjectionMiddleware>();
            next(builder);
        };
    }
}
