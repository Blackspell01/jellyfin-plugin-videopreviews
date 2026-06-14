using System.Collections.ObjectModel;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.VideoPreviews.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        SegmentCount = 5;
        SegmentSeconds = 3;
        Height = 480;
        EnabledLibraries = new Collection<string>();
    }

    /// <summary>
    /// Gets or sets how many positions (segments) the montage is made of (e.g. 5 or 8).
    /// </summary>
    public int SegmentCount { get; set; }

    /// <summary>
    /// Gets or sets how many seconds each position is shown (e.g. 3 or 1).
    /// </summary>
    public double SegmentSeconds { get; set; }

    /// <summary>
    /// Gets or sets the height the preview is scaled to (width keeps aspect). 0 = keep source.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the virtual-folder (library) ItemIds for which previews are generated.
    /// </summary>
    public Collection<string> EnabledLibraries { get; set; }
}
