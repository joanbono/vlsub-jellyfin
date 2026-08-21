using Jellyfin.Plugin.VlsubGo.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// Plugin entry point. Jellyfin discovers <see cref="VlsubSubtitleProvider"/>
/// on its own, so nothing needs registering here.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the single loaded instance, so the provider can read configuration.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    public override string Name => "vlsub-go";

    public override Guid Id => Guid.Parse("7c9e6a41-3b5d-4f28-9a1c-6d2e8b4f7a13");

    public override string Description =>
        "Subtitles from opensubtitles.org, matched by file hash. No account or API key required.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html",
        };
    }
}
