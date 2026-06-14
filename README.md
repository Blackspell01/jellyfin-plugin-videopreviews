# Jellyfin Plugin: Video Previews

Pre-generates short **hover preview clips** per library item (a montage of *N × T seconds*,
e.g. `5×3s` or `8×1s`) and serves them so a tiny web snippet can show a smooth, instant
preview when hovering a poster.

Generation runs server-side as a **Scheduled Task** using Jellyfin's bundled **ffmpeg**, so
the browser does no heavy work — at hover time it just plays a ready static file.

## How it works
- **Scheduled task** "Generate Video Previews" walks the enabled libraries and, for each video,
  builds `{itemId}.mp4` (the N×T montage) into the plugin data folder (`<data>/videopreviews/`).
- **API**: `GET /VideoPreviews/{itemId}` returns the clip (or 404 if not generated yet).
  `GET /VideoPreviews/Status` returns per-library counts (used by the config page).
- **Config GUI** (Dashboard → Plugins → Video Previews):
  - Anzahl Stellen (segment count) and Sekunden pro Stelle (seconds each) — e.g. 5×3s, 8×1s.
  - Höhe (downscale height; 0 = source).
  - Per-library **checkbox** to enable, with a `generated / total` counter.
  - Buttons: **Speichern**, **Jetzt generieren**, **Status aktualisieren**.
- **Client script auto-injection**: on startup the plugin copies `vidprev.js` into the web client folder
  and adds the `<script>` tag to `index.html` itself (idempotent; re-applied after Jellyfin updates).
  Nothing to edit by hand. (Needs the Jellyfin process to have write access to the web folder.)

## Build (GitHub Actions)
Pushing to `main` runs `.github/workflows/build.yaml`, which does `dotnet publish` and uploads
**`Jellyfin.Plugin.VideoPreviews.dll`** as a build artifact. Download it from the Actions run.

> Targeted at **Jellyfin 10.11** (.NET 9, `Jellyfin.Controller` 10.11.11, `targetAbi 10.11.0.0`).
> For a different server version, change the versions in
> `Jellyfin.Plugin.VideoPreviews/Jellyfin.Plugin.VideoPreviews.csproj` and `build.yaml`.

## Install
1. Copy `Jellyfin.Plugin.VideoPreviews.dll` into your Jellyfin `plugins/VideoPreviews/` folder
   (e.g. `/var/lib/jellyfin/plugins/VideoPreviews/` or the Docker config volume).
2. Restart Jellyfin. The plugin appears under Dashboard → Plugins.
3. Open its config, pick libraries + the N×T values, **Speichern**, then **Jetzt generieren**.
4. Hover previews are wired up **automatically** — the plugin injects its client script into the web
   client on startup. Just hard-refresh the browser (Ctrl/Cmd+Shift+R) after install.

## Automatic generation (new videos)
- The scheduled task has a **daily** default trigger and is **incremental** (already-generated items
  are skipped), so newly added videos get previews on the next run. You can change the schedule under
  Dashboard → Scheduled Tasks.
- Additionally, a background **new-item listener** generates the preview shortly after a video is added
  (one at a time, so a big scan doesn't spawn many ffmpeg jobs). Toggle via
  "Neue Videos automatisch erkennen & generieren" in the plugin config.

## Install via repository URL (auto-updates)
Push a tag like `v1.0.0.0` → `.github/workflows/release.yaml` builds a release zip, attaches it to a
GitHub Release, and updates `manifest.json`. Then in Jellyfin:

Dashboard → Plugins → Repositories → **Add**, URL:
```
https://raw.githubusercontent.com/Blackspell01/jellyfin-plugin-videopreviews/main/manifest.json
```
The plugin then appears in the catalog and updates automatically. (Until the first tagged release the
manifest is empty — use the manual DLL install above in the meantime.)

## Notes / limits
- Storage ≈ one small mp4 per item (depends on height/length).
- New items get previews on the next task run.
- Generation re-encodes only the tiny preview clip (ffmpeg `concat`); your originals are untouched.
