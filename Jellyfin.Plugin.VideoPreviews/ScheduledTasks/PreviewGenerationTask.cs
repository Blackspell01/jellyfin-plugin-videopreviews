using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VideoPreviews.ScheduledTasks;

/// <summary>
/// Scheduled task that generates preview clips for all enabled libraries.
/// Runs on a recurring interval so newly added videos get previews automatically
/// (it skips items that already have one).
/// </summary>
public class PreviewGenerationTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly PreviewService _previewService;
    private readonly ILogger<PreviewGenerationTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewGenerationTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="previewService">The preview service.</param>
    /// <param name="logger">The logger.</param>
    public PreviewGenerationTask(ILibraryManager libraryManager, PreviewService previewService, ILogger<PreviewGenerationTask> logger)
    {
        _libraryManager = libraryManager;
        _previewService = previewService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Generate Video Previews";

    /// <inheritdoc />
    public string Key => "VideoPreviewsGenerate";

    /// <inheritdoc />
    public string Description => "Generates the short hover preview clips for the enabled libraries (new items are picked up automatically).";

    /// <inheritdoc />
    public string Category => "Video Previews";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run once a day; incremental (already-generated items are skipped) so new videos get covered.
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(24).Ticks
            }
        };
    }

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
                await _previewService.GenerateAsync(item, false, cancellationToken).ConfigureAwait(false);
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
}
