using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Subtitles;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// Registers the subtitle provider with the host's dependency injection
/// container.
/// <para>
/// This is not optional. Implementing <see cref="ISubtitleProvider"/> alone is
/// not enough on Jellyfin 10.11: the plugin assembly loads and the plugin shows
/// as Active, but the provider is never constructed and its Search method is
/// never called, with nothing logged to say so. The official OpenSubtitles
/// plugin registers itself the same way.
/// </para>
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ISubtitleProvider, VlsubSubtitleProvider>();
    }
}
