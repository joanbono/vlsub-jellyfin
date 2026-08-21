using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.VlsubGo.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether downloaded subtitles are checked
    /// for cues that were split across duplicate timings and merged back
    /// together. See <see cref="SrtRepair"/> for why this matters.
    /// </summary>
    public bool RepairSplitCues { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether SubRip results are preferred over
    /// other formats when neither is a hash match.
    /// </summary>
    public bool PreferSubRip { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of results returned per search.
    /// </summary>
    public int MaxResults { get; set; } = 30;
}
