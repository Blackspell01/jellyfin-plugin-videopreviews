using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VideoPreviews;

/// <summary>
/// Listens for newly added items and generates their preview shortly after, one at a time
/// (so a library scan doesn't spawn many ffmpeg processes at once).
/// </summary>
public sealed class NewItemNotifier : IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly PreviewService _previewService;
    private readonly ILogger<NewItemNotifier> _logger;
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    private CancellationTokenSource? _cts;
    private Task? _worker;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewItemNotifier"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="previewService">The preview service.</param>
    /// <param name="logger">The logger.</param>
    public NewItemNotifier(ILibraryManager libraryManager, PreviewService previewService, ILogger<NewItemNotifier> logger)
    {
        _libraryManager = libraryManager;
        _previewService = previewService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _libraryManager.ItemAdded += OnItemAdded;
        _worker = Task.Run(() => WorkerAsync(_cts.Token));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _queue.Writer.TryComplete();
        if (_cts != null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_worker != null)
        {
            try
            {
                await _worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Dispose();
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        var item = e.Item;
        if (Plugin.Instance?.Configuration.AutoGenerate != true)
        {
            return;
        }

        if (item is not Video || !_previewService.IsInEnabledLibrary(item))
        {
            return;
        }

        _queue.Writer.TryWrite(item.Id);
    }

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (_queue.Reader.TryRead(out var id))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = _libraryManager.GetItemById(id);
                    if (item == null)
                    {
                        continue;
                    }

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
                        _logger.LogError(ex, "Auto preview generation failed for {Name}", item.Name);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }
}
