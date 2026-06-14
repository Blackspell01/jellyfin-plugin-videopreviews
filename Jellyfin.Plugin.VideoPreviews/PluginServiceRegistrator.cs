using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.VideoPreviews;

/// <summary>
/// Registers the plugin's services (preview generator + new-item listener) with Jellyfin's DI.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<PreviewService>();
        serviceCollection.AddHostedService<NewItemNotifier>();
        serviceCollection.AddHostedService<ScriptInjector>();
    }
}
