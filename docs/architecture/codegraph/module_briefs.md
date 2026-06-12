# Module Briefs

One-page modernization briefs per module, produced by scoped read-only agents
(June 2026). Evidence is grep-based file counts against this worktree; numbers
are approximate but verified against real sources. See `CODE_GRAPH.md` for the
synthesis.

---

## RssBandit (UI shell)

### Purpose & architecture
RssBandit is a WinForms-based desktop news aggregator (RSS/Atom reader) built as a Windows-only desktop shell (net5.0-windows10.0.19041, WinExe). The architecture uses a Core/WinGui split where Core manages application logic, feed sources, and subscription data, while WinGui handles the presentation layer with Infragistics commercial controls (toolbars, trees, grids) and SandDock for docking panels. The app instantiates a Unity DI container and previously relied on IE/MSHTML for content rendering, now transitioning to WebView2.

### Key subareas
- **Core/** (36 files): RssBanditApplication, FeedSourceManager, storage/serialization, command mediation, threading
- **WinGui/Forms** (13 files): Main form shell (WinGuiMain split across 6 partial files: Init, Callbacks, OwnerInteraction, Helpers, Commands), NewsGroupsConfiguration, SplashScreen, ControlHelpers
- **WinGui/Controls** (38 files): Infragistics-extended controls (UltraTreeExtended, TreeFeedsNodeBase), ListView/TreeView custom filters and painters, SearchPanel, tooltips, state serialization
- **WinGui/Dialogs** (28 files): Subscription/sync wizards (Divelements.WizardFramework), configuration dialogs, plugin UI hosts
- **Plugin host** (~30 files in plugins/): Twitter, OneNote, Delicious, Blog integrations, MailThis

### Third-party UI coupling
- **Infragistics.WinForms** (6 packages, 20.2.14): 36 C# files actively use it (Editors, ExplorerBar, StatusBar, TabControl, Toolbars, Tree). Pervasive in Forms/ and Controls/ for grid rendering, toolbar management, tooltips, node painters, and state serialization helpers. Moderate-to-high coupling; grid-based views and tree UI would require substantial replacement.
- **SandDock** (7 files): Docking panel framework in WinGuiMain and Core; contained but integral to main form layout (panels, dock hosts, document manager). Vendor proprietary.
- **Divelements.WizardFramework** (9 files): Wizards for subscription and sync flows; isolated to Dialogs/, moderate decoupling possible via custom WinForms panels.
- **WebView2** (7 files): Modern rendering in WinGuiMain variants and supporting files; replaces commented-out IEControl code. Well-isolated, modern direction.

### Modernization risk flags
- **Serialization**: Active use of `SoapFormatter` (2 instantiation sites in RssBanditApplication.cs, lines 2258/2320) and `BinaryFormatter` for persisted feed state; SoapFormatter NuGet package 1.0.11 added as a workaround. Both are blocked in .NET 9+; migration to JSON/XML serialization required.
- **P/Invoke** (7 files): Win32 imports in WinInetAPI, Win32.cs helpers, WheelSupport (mouse wheel), TrayAnimation, OneNote interop (NativeMethods). Mostly UI polish.
- **COM interop** (5 binary refs): Interop.iTunesLib, Interop.WMPLib, blogExtension, Eyefinder, Interop.ThumbCache via HintPaths to Common\Libraries\. Plugin/podcast feature set; non-core to the reader.
- **IE remnants** (5 files): Commented-out IEControl code in WinGuiMain variants and WheelSupport; semantically replaced by WebView2 but dead code remains.
- **Commercial controls** (Infragistics, SandDock): License-locked. Infragistics 20.2.14 (2020 era) may lack .NET 8 compatibility; vendor support required.

### Build-fix notes toward .NET 8/10
- **TargetFramework**: net5.0-windows10.0.19041 → net8.0-windows (or net10.0). No major API breakage expected for core WinForms; test Unity 5.11.9 DI.
- **Serialization**: SoapFormatter/BinaryFormatter must migrate to System.Text.Json or XML — unblock first.
- **Infragistics**: verify 20.2.14 on net8.0; likely needs a newer licensed version.
- **SandDock**: binary HintPath, unknown .NET 8 support; contact vendor or replace with open-source docking (e.g. DockPanelSuite).
- **WebView2**: update package 1.0.774.44 → current.
- **RuntimeIdentifiers**: win-x86 only today; audit P/Invoke for x64/ARM64 if widening.

### MAUI feasibility
Not viable without a UI rewrite: SandDock docking, Infragistics grids, COM interop, and Win32 P/Invoke are all Windows-desktop paradigms. Recommend .NET 8/10 WinForms modernization for the shell; revisit MAUI only via a portable engine split.

---

## NewsComponents (feed engine)

### Purpose & architecture
NewsComponents is the core feed aggregation and syndication library, handling feed discovery, fetching, caching, indexing, and parsing across RSS, Atom, NNTP, and specialized feed sources. It implements a plugin architecture via FeedSource subclasses and manages background downloads, search indexing, feed protocol negotiation, and XML serialization of feed/item metadata. ~103 C# files, SDK-style net5.0-windows library with log4net logging and Lucene 2.9 full-text search.

### Feed source implementations
- **BanditFeedSource** — direct HTTP feed fetching (primary, active)
- **WindowsRssFeedSource** — Windows RSS platform integration (Windows-specific, legacy)
- **FeedlyCloudFeedSource** — Feedly cloud sync
- **GoogleReaderFeedSource** — DEAD (service shut down July 2013)
- **NewsGatorFeedSource** — DEAD (service discontinued)
- **FacebookFeedSource** — DEAD in practice (Facebook API v7 era; non-functional with modern API)

### Subsystems
- **Storage:** FileStorageDataService with XmlSerializer-based cache (file-system backed, no DB)
- **Search:** Lucene.Net 2.9.4.1 + Contrib (5 files: indexer, searcher, settings, modifiers)
- **Networking:** HttpWebRequest (18 occurrences across 10 files, 100% legacy WebRequest; no HttpClient), AsyncWebRequest, SyncWebRequest, BITS COM interop (8 files)
- **Protocols:** NNTP client (NntpClient/Parser/WebRequest with Org.Mime4Net 1.8 binary ref); Atom/RSS via RssParser, SgmlParser HTML recovery

### Modernization risk flags
- **XmlSerializer pervasive:** 83 matches across 30 files (storage, feeds, sources, collections)
- **BinaryFormatter/SoapFormatter:** 2 matches (DownloadRegistryManager, DownloadTask) — blocked in .NET 9+
- **HttpWebRequest obsolete:** 18 matches in 10 files; no HttpClient adoption
- **P/Invoke:** 9 DllImports in 4 files (NTFS streams, MimeType, FileHelper, HttpCookieManager) — Windows-only
- **Lucene 2.9 EOL:** 2011-era; Lucene.Net 4.8+ is a breaking migration (analyzers, query API, index format)
- **WindowsAPICodePack:** COM interop for BITS/shell — Windows-bound
- **Org.Mime4Net 1.8 (2012):** binary DLL only, no source; MimeKit is the modern replacement

### Build-fix notes toward .NET 8/10
1. BinaryFormatter removal (.NET 9+): rewrite DownloadRegistryManager/DownloadTask serialization to JSON
2. HttpWebRequest → HttpClient: wrap Async/SyncWebRequest internals
3. XmlSerializer → System.Text.Json: gradual; prioritize feed/cache serialization
4. Lucene.Net 2.9 → 4.8+: major; index rebuild required; plan phased migration
5. WindowsAPICodePack: BITS via COM still works on Windows; or replace with HttpClient resumable downloads
6. Target net8.0-windows: DllImports and COM keep working; Windows-only is acceptable for the WinForms milestone

### MAUI feasibility
Not portable as-is (WindowsAPICodePack COM, P/Invoke, Mime4Net binary). A headless `NewsComponents.Core` (BanditFeedSource + parsing + storage, minus BITS/WindowsRss/NNTP) extracted to cross-platform .NET could serve both WinForms and a future MAUI shell.

---

## RssBandit.AppServices (contract layer)

### Purpose
Defines the core abstraction layer and service interfaces, decoupling the UI shell from business logic. Zero project references and zero package dependencies — a true boundary contract. Both NewsComponents and RssBandit reference it.

### Main interfaces
- **ICoreApplication** — primary service gateway (feed sources, preferences, search engines, identities, NNTP definitions)
- **IFeedSources / INewsFeed / INewsItem** — feed and item contracts
- **IUserPreferences** — user settings service
- **IInternetService** — HTTP/proxy configuration
- **IChannelProcessor** — feed processing pipeline
- **ICommandBarManager / IDocumentWindowManager** — UI command/window abstraction
- **IAddInManager / IPersistedSettings** — add-in lifecycle, config persistence

### UI-type leakage
5 files couple the contracts to WinForms/GDI+: ICoreApplication.cs (`System.Windows.Forms`), IUserPreferences.cs (`System.Drawing`), CommandBarInterfaces.cs (both — `Keys` enum and `Image` type used directly), WindowManagementInterfaces.cs, AddInInterfaces.cs.

### Modernization notes
- Blocking for portability: command-bar and window-management contracts export `Keys`/`Image`. Abstract these before any MAUI split.
- Positive: zero dependencies keeps this the natural seam for a portable core.
- Refactor path: platform-neutral command definitions and an image abstraction.

---

## ChildProjects (satellite libraries)

### Consumption status

| Project | Consumed by main solution? | Evidence |
|---------|---------------------------|----------|
| IEControl | No — vestigial | 6 refs in RssBandit; all comments/dead code. WebView2 replaced it. |
| ShellLib / Jumplist | No | Zero references in RssBandit or NewsComponents. |
| RelationCosmos | Yes — embedded copy | Canonical source embedded at `source/NewsComponents/RelationCosmos/`; ~41 refs across codebase. ChildProjects copy is redundant. |
| ThreadedListViewControl | Yes — compiled in-tree | RssBandit.csproj (SDK glob) compiles `WinGui/Controls/ThListView/*.cs` directly into the exe; no Compile Remove. The standalone csproj is vestigial. |
| ExceptionManagement | Binary only | Main app uses the prebuilt DLLs; ChildProjects has the (unused) source. |
| BanditBuildTasks | Build-time only | Legacy custom MSBuild tasks; unclear if still invoked. |
| DiffPatchResources | Build-time tooling | Legacy resx localization sync console app. |

### IEControl verdict
Fully vestigial and replaced by WebView2. All remaining references in RssBandit are comments, TODO markers, or dead code (`CreateAndInitIEControl` now returns `Task<WebView2>`). No binary dependency in the main solution. Safe to drop from the modernization scope.

### Duplicated/embedded code
- RelationCosmos: live copy inside NewsComponents; ChildProjects satellite is stale.
- ThreadedListViewControl: in-tree sources, not a real satellite; modernize with RssBandit.
- ExceptionManagement: bought-in binary with bundled reference source.

### Recommendations
Ignore for the build-fix milestone: IEControl, ShellLib, Jumplist, BanditBuildTasks, DiffPatchResources (verify not invoked by build). Attention: consolidate RelationCosmos to the NewsComponents copy; treat ThListView as part of RssBandit; consider replacing the ExceptionManagement binaries with the in-repo source or modern logging.

---

## Plugins & AddIns

### Plugin architecture
Plugins implement `IBlogExtension` (from `blogExtension.dll` 1.1.0.2, binary HintPath). `ServiceManager.SearchForIBlogExtensions()` scans the plugin directory via reflection at runtime (`WinGuiMain.Helpers.cs` → `RssBanditApplication.GetPlugInPath()`). A second AddIn mechanism uses `IAddInPackage`/`IAddInManager` (implemented by ServiceManager). Both are legacy patterns.

### Inventory & viability

| Plugin | External service | Service status | Recommendation |
|--------|------------------|---|---|
| Delicious | del.icio.us REST API | Dead | Drop |
| Twitter | Twitter API v1 | Dead | Drop |
| OneNote | COM interop (OneNote 2003-era) | Obsolete | Drop |
| MailThis | MAPI / Outlook interop | Deprecated | Deprecate |
| BlogThis.LiveWriter | Windows Live Writer | Discontinued | Drop |
| BlogThis.WBloggar | w.bloggar | Inactive | Drop |
| IBlogExtensionsSolution (misc) | Various legacy APIs | Obsolete | Drop |

### Modernization notes
- All legacy plugins target dead services; none block the build-fix milestone (they're outside the main solution).
- `AdsBlocker2.AddIn` hard-references NewsComponents + AppServices and will break on retarget; rewrite or drop.
- Longer term: replace binary `IBlogExtension` discovery with a modern source/NuGet plugin contract.

---

## Test projects

### NewsComponents.UnitTests
13 .cs files, 41 NUnit `[Test]` attributes across 10 test classes. Dual-framework packages (NUnit 3.13.1 + unused xunit 2.4.1). SDK-style net5.0-windows, in the main solution. Three test classes inherit `CassiniHelperTestFixture`, spinning up the ancient Cassini in-process web server (binary ref, no NuGet) for RSS-fetch integration tests — fragile and a modernization blocker for CI.

### RssBandit.UnitTests
4 .cs files, 4 `[Test]`s (one real test file: ShortcutManagerTests.cs). Legacy net-fx 4.0 csproj referencing the net5.0 main projects — cannot build. Effectively abandoned.

### Recommendations
- Consolidate to NUnit; drop the unused xunit packages.
- Replace Cassini with WireMock.Net (or Kestrel test host) to unblock CI.
- Delete RssBandit.UnitTests or fold its one real test into NewsComponents.UnitTests.
