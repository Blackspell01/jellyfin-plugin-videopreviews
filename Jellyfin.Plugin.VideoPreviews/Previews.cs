using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.VideoPreviews;

/// <summary>
/// Shared helpers for locating preview files and the items they belong to.
/// </summary>
public static class Previews
{
    /// <summary>
    /// Gets the on-disk path of the preview file for an item.
    /// </summary>
    /// <param name="id">The item id.</param>
    /// <returns>Absolute path to the (possibly not yet existing) preview mp4.</returns>
    public static string PathFor(Guid id)
        => Path.Combine(Plugin.Instance!.PreviewFolder, id.ToString("N", CultureInfo.InvariantCulture) + ".mp4");

    /// <summary>
    /// Resolves a virtual-folder ItemId to its <see cref="BaseItem"/> folder.
    /// </summary>
    /// <param name="lib">The library manager.</param>
    /// <param name="itemId">The virtual-folder item id.</param>
    /// <returns>The folder, or null.</returns>
    public static BaseItem? ResolveFolder(ILibraryManager lib, string itemId)
        => Guid.TryParse(itemId, out var gid) ? lib.GetItemById(gid) : null;

    /// <summary>
    /// Returns all video items (movies/episodes) under a parent folder.
    /// </summary>
    /// <param name="lib">The library manager.</param>
    /// <param name="parent">The parent folder.</param>
    /// <returns>List of video items.</returns>
    public static IReadOnlyList<BaseItem> QueryVideos(ILibraryManager lib, BaseItem parent)
        => lib.GetItemList(new InternalItemsQuery
        {
            Recursive = true,
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
            Parent = parent
        });

    /// <summary>
    /// Collects all video items across the given enabled libraries.
    /// </summary>
    /// <param name="lib">The library manager.</param>
    /// <param name="libraryIds">Enabled virtual-folder ItemIds.</param>
    /// <returns>List of video items.</returns>
    public static IReadOnlyList<BaseItem> CollectItems(ILibraryManager lib, IEnumerable<string> libraryIds)
    {
        var enabled = new HashSet<string>(libraryIds, StringComparer.OrdinalIgnoreCase);
        var items = new List<BaseItem>();
        foreach (var vf in lib.GetVirtualFolders())
        {
            if (!enabled.Contains(vf.ItemId))
            {
                continue;
            }

            var folder = ResolveFolder(lib, vf.ItemId);
            if (folder != null)
            {
                items.AddRange(QueryVideos(lib, folder));
            }
        }

        return items;
    }
}
