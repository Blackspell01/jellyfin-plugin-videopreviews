using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.VideoPreviews.Api;

/// <summary>
/// Serves generated preview clips and per-library status.
/// </summary>
[ApiController]
[Route("VideoPreviews")]
public class PreviewController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewController"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    public PreviewController(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Gets the generated preview clip for an item (or 404 if not generated yet).
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The preview mp4.</returns>
    [HttpGet("{itemId}")]
    [Authorize(Policy = "DefaultAuthorization")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetPreview([FromRoute] Guid itemId)
    {
        var path = Previews.PathFor(itemId);
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        return PhysicalFile(path, "video/mp4", true);
    }

    /// <summary>
    /// Gets per-library counts of total vs generated previews (for the config page).
    /// </summary>
    /// <returns>List of library status objects.</returns>
    [HttpGet("Status")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<LibraryStatus>> GetStatus()
    {
        var result = new List<LibraryStatus>();
        foreach (var vf in _libraryManager.GetVirtualFolders())
        {
            var folder = Previews.ResolveFolder(_libraryManager, vf.ItemId);
            if (folder == null)
            {
                continue;
            }

            var items = Previews.QueryVideos(_libraryManager, folder);
            var generated = 0;
            foreach (var it in items)
            {
                if (System.IO.File.Exists(Previews.PathFor(it.Id)))
                {
                    generated++;
                }
            }

            result.Add(new LibraryStatus
            {
                Id = vf.ItemId,
                Name = vf.Name,
                CollectionType = vf.CollectionType?.ToString(),
                Total = items.Count,
                Generated = generated
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Per-library status DTO.
    /// </summary>
    public class LibraryStatus
    {
        /// <summary>Gets or sets the virtual-folder item id.</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the library name.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the collection type.</summary>
        public string? CollectionType { get; set; }

        /// <summary>Gets or sets the total number of video items.</summary>
        public int Total { get; set; }

        /// <summary>Gets or sets how many previews are already generated.</summary>
        public int Generated { get; set; }
    }
}
