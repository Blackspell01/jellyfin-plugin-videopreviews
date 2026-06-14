using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VideoPreviews;

/// <summary>
/// Copies the client script into the web client folder and injects a script tag into index.html,
/// so the user doesn't have to edit anything by hand. Runs on every startup (idempotent), which
/// also re-applies it after Jellyfin updates overwrite the web client.
/// </summary>
public sealed class ScriptInjector : IHostedService
{
    private const string Marker = "<!-- VideoPreviews -->";
    private const string ResourceName = "Jellyfin.Plugin.VideoPreviews.Web.vidprev.js";

    private readonly IApplicationPaths _paths;
    private readonly ILogger<ScriptInjector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptInjector"/> class.
    /// </summary>
    /// <param name="paths">Application paths (for the web client folder).</param>
    /// <param name="logger">The logger.</param>
    public ScriptInjector(IApplicationPaths paths, ILogger<ScriptInjector> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Inject();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VideoPreviews: could not auto-inject client script (you may need to add it manually)");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void Inject()
    {
        var webPath = _paths.WebPath;
        if (string.IsNullOrEmpty(webPath) || !Directory.Exists(webPath))
        {
            _logger.LogWarning("VideoPreviews: web path '{WebPath}' not found; skipping injection", webPath);
            return;
        }

        // 1) write vidprev.js next to index.html (refresh every start)
        var jsTarget = Path.Combine(webPath, "vidprev.js");
        using (var resource = GetType().Assembly.GetManifestResourceStream(ResourceName))
        {
            if (resource == null)
            {
                _logger.LogWarning("VideoPreviews: embedded resource {Resource} missing", ResourceName);
                return;
            }

            using var fs = File.Create(jsTarget);
            resource.CopyTo(fs);
        }

        // 2) inject the script tag into index.html (once)
        var indexPath = Path.Combine(webPath, "index.html");
        if (!File.Exists(indexPath))
        {
            _logger.LogWarning("VideoPreviews: index.html not found in web path");
            return;
        }

        var html = File.ReadAllText(indexPath);
        if (html.Contains(Marker, StringComparison.Ordinal))
        {
            return;
        }

        var tag = Marker + "<script defer src=\"vidprev.js\"></script>";
        var pos = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (pos < 0)
        {
            pos = html.LastIndexOf("</html>", StringComparison.OrdinalIgnoreCase);
        }

        html = pos < 0 ? html + tag : html.Insert(pos, tag);
        File.WriteAllText(indexPath, html);
        _logger.LogInformation("VideoPreviews: injected client script into web index.html");
    }
}
