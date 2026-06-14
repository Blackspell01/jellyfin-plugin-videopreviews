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
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VideoPreviews.ScheduledTasks;

/// <summary>
/// Scheduled task that pre-generates the hover preview clips.
/// </summary>
public class PreviewGenerationTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILogger<PreviewGenerationTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewGenerationTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="mediaEncoder">The media encoder (for the ffmpeg path).</param>
    /// <param name="logger">The logger.</param>
    public PreviewGenerationTask(ILibraryManager libraryManager, IMediaEncoder mediaEncoder, ILogger<PreviewGenerationTask> logger)
    {
        _libraryManager = libraryManager;
        _mediaEncoder = mediaEncoder;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Generate Video Previews";

    /// <inheritdoc />
    public string Key => "VideoPreviewsGenerate";

    /// <inheritdoc />
    public string Description => "Generates the short hover preview clips for the enabled libraries.";

    /// <inheritdoc />
    public string Category => "Video Previews";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance!.Configuration;
        var items = Previews.CollectItems(_libraryManager, config.EnabledLibraries);
        var total = items.Count;
        if (total == 0)
        {
            progress.Report(100);
            return;
        }

        var done = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await GenerateOneAsync(item, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preview generation failed for {Name}", item.Name);
            }

            done++;
            progress.Report(done * 100.0 / total);
        }
    }

    private async Task GenerateOneAsync(BaseItem item, CancellationToken cancellationToken)
    {
        if (item.RunTimeTicks is null || item.RunTimeTicks <= 0)
        {
            return;
        }

        var input = item.Path;
        if (string.IsNullOrEmpty(input) || !File.Exists(input))
        {
            return;
        }

        var output = Previews.PathFor(item.Id);
        if (File.Exists(output))
        {
            return;
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
        }
    }
}
