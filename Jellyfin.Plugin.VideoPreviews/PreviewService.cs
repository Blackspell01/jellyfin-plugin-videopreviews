using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VideoPreviews;

/// <summary>
/// Builds and stores the preview montage for an item. Shared by the scheduled task and the new-item listener.
/// </summary>
public class PreviewService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILogger<PreviewService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="mediaEncoder">The media encoder (ffmpeg path).</param>
    /// <param name="logger">The logger.</param>
    public PreviewService(ILibraryManager libraryManager, IMediaEncoder mediaEncoder, ILogger<PreviewService> logger)
    {
        _libraryManager = libraryManager;
        _mediaEncoder = mediaEncoder;
        _logger = logger;
    }

    /// <summary>
    /// Determines whether the item is a video that lives in one of the enabled libraries.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>True if a preview should be generated for it.</returns>
    public bool IsInEnabledLibrary(BaseItem item)
    {
        if (item is not Video)
        {
            return false;
        }

        var configured = Plugin.Instance!.Configuration.EnabledLibraries;
        if (configured.Count == 0)
        {
            return false;
        }

        var enabled = new HashSet<Guid>();
        foreach (var s in configured)
        {
            if (Guid.TryParse(s, out var g))
            {
                enabled.Add(g);
            }
        }

        foreach (var folder in _libraryManager.GetCollectionFolders(item))
        {
            if (enabled.Contains(folder.Id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Generates the preview clip for an item (skips if it already exists, unless overwrite).
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="overwrite">Whether to regenerate even if a preview already exists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a preview was created.</returns>
    public async Task<bool> GenerateAsync(BaseItem item, bool overwrite, CancellationToken cancellationToken)
    {
        if (item.RunTimeTicks is null || item.RunTimeTicks <= 0)
        {
            return false;
        }

        var input = item.Path;
        if (string.IsNullOrEmpty(input) || !File.Exists(input))
        {
            return false;
        }

        var output = Previews.PathFor(item.Id);
        if (!overwrite && File.Exists(output))
        {
            return false;
        }

        var config = Plugin.Instance!.Configuration;
        var durationSec = item.RunTimeTicks.Value / 10_000_000.0;
        var count = Math.Max(1, config.SegmentCount);
        var seg = Math.Max(0.5, config.SegmentSeconds);
        var height = config.Height > 0 ? config.Height : 0;

        var args = new StringBuilder();
        args.Append("-y -hide_banner -loglevel error ");
        var labels = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            var frac = (i + 1.0) / (count + 1.0);
            var pos = Math.Max(0, (durationSec * frac) - (seg / 2));
            args.Append(CultureInfo.InvariantCulture, $"-ss {pos.ToString("0.000", CultureInfo.InvariantCulture)} -t {seg.ToString("0.000", CultureInfo.InvariantCulture)} -i \"{input}\" ");
            labels.Append(CultureInfo.InvariantCulture, $"[{i}:v]");
        }

        var scale = height > 0 ? $",scale=-2:{height.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        args.Append(CultureInfo.InvariantCulture, $"-filter_complex \"{labels}concat=n={count.ToString(CultureInfo.InvariantCulture)}:v=1:a=0{scale},setsar=1[v]\" ");
        args.Append(CultureInfo.InvariantCulture, $"-map \"[v]\" -an -c:v libx264 -preset veryfast -crf 28 -pix_fmt yuv420p -movflags +faststart \"{output}\"");

        var psi = new ProcessStartInfo
        {
            FileName = _mediaEncoder.EncoderPath,
            Arguments = args.ToString(),
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("ffmpeg exited {Code} for {Name}: {Error}", process.ExitCode, item.Name, stderr);
            if (File.Exists(output))
            {
                File.Delete(output);
            }

            return false;
        }

        return true;
    }
}
