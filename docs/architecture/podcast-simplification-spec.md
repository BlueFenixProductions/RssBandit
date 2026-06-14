# Spec: Demote podcasts from "podcatcher" to enclosure links

**Status:** Approach approved 2026-06-14. Sub-step A implemented (`786901cb`); sub-steps B and C not started.
**Owner decision:** Chris ("Captain"). **Branch context:** `claude/com-elimination`.

## 1. Summary

RSS Bandit currently behaves as a full *podcatcher*: it downloads enclosure media
files in the background, caches them on disk under a size cap, can auto-download N
enclosures per new feed, exposes a download-manager window, a Podcast Options
dialog, and ~11 preferences. Modern podcast consumption has moved to dedicated apps
(Apple Podcasts, Pocket Casts, Spotify, etc.) which subscribe to the feed's
`<enclosure>` URLs themselves.

This spec demotes enclosures to **first-class links**: RSS Bandit keeps parsing and
showing them, and a click hands the URL to the OS default handler. The entire
background-download subsystem is deleted.

### Decision: what a click does

Confirmed approach — **open the enclosure URL in the default handler**
(`Process.Start` with `UseShellExecute = true`). The OS routes it to the browser /
registered media or podcast app, which streams or downloads it. No in-app download,
cache, or progress UI. (Rejected alternatives: keep an on-demand "Save as"; open the
episode web page instead.)

> Note on the original "open in your favorite podcast player" idea: there is no
> reliable Windows mechanism to hand an arbitrary RSS feed to "the podcast app"
> (no universally-registered `podcast:`/`feed:` handler; Spotify cannot ingest
> arbitrary RSS). "Open the enclosure URL in the default handler" is the closest
> achievable behavior.

## 2. Goals / non-goals

**Goals**
- Clicking an enclosure opens it in the user's default handler. *(done — sub-step A)*
- Delete the background-download engine, its on-disk `.task` registry, the
  enclosure cache, and the auto-download-on-refresh path.
- Remove the download-manager window, the Podcast Options dialog, and the
  enclosure/podcast preference UI.
- Net reduction ≈ 3,000 lines; keep the app building and feeds fetching throughout.

**Non-goals**
- Removing the `Enclosure` feed model or hiding enclosures from items (they stay).
- Changing feed fetching/parsing behavior beyond dropping enclosure auto-download.
- Touching `plugins/**` or `ChildProjects/**` (excluded from the build).

## 3. Current architecture (what gets cut)

### Engine (`NewsComponents`) — ~2,540 lines + helpers
| File | Lines | Role |
|---|---|---|
| `Core/BackgroundDownloadManager.cs` | 979 | Orchestration, `IDownloadInfoProvider`, `IDownloader`, selection, cache-limit check, completion move + Zone.Identifier tagging |
| `Net/DownloadRegistryManager.cs` | 426 | Persistent `.task` registry (JSON) under `DownloadedFilesDataPath` |
| `Net/DownloadTask.cs` | 388 | Download task model + JSON DTO |
| `Net/HttpDownloader.cs` | 361 | Streaming HTTP downloader (added earlier today; goes away with the rest) |
| `Net/DownloadItem.cs` | 237 | Download item model + JSON DTO |
| `Net/DownloadFilesCollection.cs` | 152 | `DownloadFile` / suggested-name logic |
| `Net/Events.cs` | ~330 | **Entirely** download event args — delete whole file |
| `Net/DownloadTaskState.cs` | small | `DownloadTaskState` enum |
| `NewsComponents.UnitTests/HttpDownloaderTests.cs` | 149 | Tests for `HttpDownloader` — delete |

### `FeedSource.cs` enclosure surface (woven into the core)
- `enclosureDownloader` field (`BackgroundDownloadManager`), constructed per source.
- Static config: `EnclosureFolder`, `PodcastFolder`, `NumEnclosuresToDownloadOnNewFeed`,
  `DefaultNumEnclosuresToDownloadOnNewFeed`, `EnclosureCacheSize`, `DefaultEnclosureCacheSize`.
- `DownloadEnclosure(INewsItem)`, `DownloadEnclosure(INewsItem, string)`,
  `DownloadEnclosure(INewsItem, int)` (the 3 overloads), `MarkEnclosuresDownloaded`.
- **Auto-download inside the refresh loop** (≈ lines 4677–4702): after a feed update,
  if `GetDownloadEnclosures(feed)` it calls `DownloadEnclosure(item, maxDownloads)`.
  **This is the highest-risk removal — it sits in the feed-update path.**
- `OnEnclosureDownloadComplete` callback + `DownloadedEnclosureCallback` delegate.
- Per-feed/category settings: `Set/GetEnclosureFolder`, `Set/GetDownloadEnclosures`,
  `Set/GetEnclosureAlert`, `Set/GetCategoryEnclosureFolder`,
  `Set/GetCategoryDownloadEnclosures`, `Set/GetCategoryEnclosureAlert`,
  `GetEnclosureFolder(feed, filename)`, `IsPodcast`. These read/write feed-XML
  attributes (`enclosurefolder`, `downloadenclosures`, `enclosurealert`).

### Configuration interfaces
- `INewsComponentsConfiguration`: `DownloadedFilesDataPath`, `DownloadEnclosures`,
  `EnclosureCacheSize`, `EnclosureFolder` (+ the `NewsComponentsConfiguration`
  backing fields/properties).
- `RssBandit.AppServices/Core/ICoreApplication.cs`: enclosure/download members.

### UI + app layer (`RssBandit`)
- `WinGui/Dialogs/DownloadManagerWindow.xaml(.cs)` — WPF progress window. Find and
  remove its open command/menu entry (search `DownloadManagerWindow`,
  `cmdDownloadManager`).
- `WinGui/Dialogs/PodcastOptionsDialog.cs` (+ `.Designer`, `.resx`) and its Tools-menu
  entry.
- `RssBanditApplication.Podcasts.cs` — `OpenPodcastInDefaultPlayer`, `IsPodcastFile`,
  `EnclosureFolder`/`PodcastFolder`/`DownloadEnclosures`/`EnclosureCacheSize`/
  `NumEnclosuresToDownloadOnNewFeed`/`PodcastFileExtensions` accessors. Most of the
  file goes; keep only what something outside the podcast feature still needs.
- `RssBanditApplication.EventHandlers.cs` — `OnDownloadedEnclosure` handler (the
  post-download hook that calls `OpenPodcastInDefaultPlayer`).
- `WinGui/Dialogs/PreferencesDialog.cs` (+ `.Designer`) — the enclosure/podcast
  preference section (`numEnclosureCacheSize`, `checkEnclosureSizeOnDiskLimited`,
  `lblDownloadAttachmentsSmallerThan*`, download-folder pickers).
- `WinGui/Dialogs/FeedProperties.cs` and `CategoryProperties.cs` — per-feed/category
  enclosure tabs/controls.
- `WinGui/ToastNotifier.cs`, `WinGui/Forms/WinGuiMain.Helpers.cs`,
  `Core/RssBanditApplication.Commands.cs`, `Converters/FileNameToIconConverter.cs` —
  incidental references to the download types / enclosure folder.

### Preferences (`RssBanditPreferences.cs`)
OptionalFlags bits: `DownloadEnclosures` (0x4000000), `EnclosureAlert` (0x8000000),
`CreateSubfoldersForEnclosures` (0x100000000), `AddPodcasts2ITunes/WMP/Folder`,
`SinglePodcastPlaylist`. String/int: `EnclosureFolder`, `PodcastFolder`,
`PodcastFileExtensions`, `NumEnclosuresToDownloadOnNewFeed`, `EnclosureCacheSize`,
`SinglePlaylistName`.

## 4. Target architecture

- `Enclosure` model: **unchanged.** Still parsed by `RssParser`, still shown on items.
- Enclosure interaction: the "Download Attachment" context-menu submenu opens the
  URL via `WinGuiMain.CmdDownloadAttachment` → `Process.Start(UseShellExecute)`
  *(done)*. Consider relabeling the menu caption from "Download Attachment" to
  "Open Attachment" (`SR.MenuDownloadAttachmentCaption`) — cosmetic resx change.
- No download engine, registry, cache, manager window, podcast options, or
  enclosure preferences.

## 5. Work breakdown

### Sub-step A — open enclosures in the default handler *(DONE, `786901cb`)*
`CmdDownloadAttachment` ShellExecutes the matched enclosure URL; shared
last-path-segment logic extracted to `WinGuiMain.EnclosureLinkLabel` and reused by
the menu builder. No longer calls `source.DownloadEnclosure`.

### Sub-step B — delete the download engine
Cohesive removal — **there is no partial green state**, because the engine files
cannot be deleted until all 24 referencing files stop using the types. Suggested
order:
1. Remove the UI/app callers first so the engine is unreferenced from `RssBandit`:
   the `OnDownloadedEnclosure` hook, `DownloadManagerWindow` + its open command, and
   any `BackgroundDownloadManager`/`DownloadItem`/`DownloadTask` usage in the app
   layer. (Sub-step C overlaps here; can be merged.)
2. In `FeedSource.cs`: delete the auto-download block in the refresh loop, the
   `DownloadEnclosure` overloads, `MarkEnclosuresDownloaded`,
   `OnEnclosureDownloadComplete`, the `enclosureDownloader` field, the per-feed/
   category enclosure setters/getters, and the enclosure-folder/cache statics. Keep
   `Enclosure` handling in parsing intact.
3. Trim `INewsComponentsConfiguration` / `NewsComponentsConfiguration` /
   `ICoreApplication` of the enclosure/download members.
4. Delete the engine files + `Events.cs` + `DownloadTaskState.cs` +
   `HttpDownloaderTests.cs`.
5. Build to green (expect many compile errors — chase them down), then full runtime
   gate.

### Sub-step C — remove download/podcast UI + preferences
- Delete `DownloadManagerWindow.xaml(.cs)` and `PodcastOptionsDialog.*` + their menu
  entries.
- Remove the enclosure/podcast section from `PreferencesDialog` and the
  per-feed/category enclosure controls from `FeedProperties`/`CategoryProperties`.
- Retire the preference *logic* (accessors in `RssBanditApplication.Podcasts.cs` /
  `RssBanditPreferences.cs`). **Keep the `OptionalFlags` enum bit values** so older
  `.preferences.json` still deserializes (same pattern as the dead `FeedSourceType`
  members). The string/int prefs can be removed or left as inert.

## 6. Back-compat / migration

- **`.preferences.json`**: keep `OptionalFlags` bit *values* stable (do not renumber)
  — the mask is persisted. Unused string/int prefs deserialize harmlessly if left.
- **`feedsources.xml` / subscriptions**: per-feed `enclosurefolder` /
  `downloadenclosures` / `enclosurealert` attributes become inert; leave the schema
  tolerant (ignore unknown), do not crash on their presence.
- **`.task` registry**: `DownloadedFilesDataPath` folder + `*.task` files become
  orphaned. One-time best-effort cleanup is optional; leaving them is harmless.
- **Already-downloaded enclosures on disk**: untouched; user keeps their files.

## 7. Risks & mitigations

- **Feed-fetch regression (highest):** the auto-download call is inside the refresh
  loop. Mitigate by removing only the enclosure block, building, and runtime-gating a
  real feed refresh (launch, confirm feeds load + items appear, empty `error.log`,
  no `FATAL` in `trace.log`).
- **Config-interface churn:** removing interface members ripples to all implementers
  (`NewsComponentsConfiguration`, test configs). Compile will surface them.
- **Designer/resx breakage:** `PodcastOptionsDialog` and `PreferencesDialog` are
  WinForms designer files — edit field decls, `InitializeComponent`, and resx
  together; orphaned resx strings are harmless.
- **Re-removes today's `HttpDownloader`:** intended. WS2's real win (deleting the
  BITS COM) stands regardless.

## 8. Testing & gating

Per the project rhythm: after each gated checkpoint, **build the csproj files (0
errors)**, run `NewsComponents.UnitTests` (baseline **38/0**; drops to 36/0 once
`HttpDownloaderTests` is deleted — update the baseline note then), and run the app to
a healthy main window with an empty `error.log` and no new `FATAL` in `trace.log`.
The download tests are removed with the engine, so add no new tests; the gate is
build + existing suite + runtime.

## 9. Estimated size

≈ 3,000+ lines removed across ~24 files, one cohesive engine commit (B) plus a
UI/prefs commit (C). Should be executed as a dedicated focused pass — deep `FeedSource`
surgery is not something to rush.
