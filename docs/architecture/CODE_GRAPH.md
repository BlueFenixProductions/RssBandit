# RssBandit Code Graph & Modernization Map

*Generated June 2026 from a graphify knowledge graph (tree-sitter AST over all C# sources)
plus MSBuild-layer extraction and per-module agent briefs.*

## Artifacts

| Artifact | What it is |
|---|---|
| `graphify-out/graph.json` | Type/member-level knowledge graph: 10,124 nodes, 16,287 edges, 613 Leiden communities. Query it with `/graphify query "<question>"` (~9k tokens/query vs ~890k naive — 100× cheaper). |
| `graphify-out/GRAPH_REPORT.md` | Audit report: god nodes, cohesion scores, surprising connections. |
| `docs/architecture/codegraph/projects.json` | MSBuild layer: all 30 csproj — TFMs, references, packages. |
| `docs/architecture/codegraph/msbuild_graph.md` | Project dependency Mermaid diagram + binary-dep and package inventories. |
| `docs/architecture/codegraph/module_briefs.md` | One-page modernization brief per module (grep-evidenced). |
| `docs/architecture/codegraph/extract_msbuild.py` | Regenerates the MSBuild layer. Graphify regenerates incrementally via `/graphify source --update`. |

## The shape of the system

The **main solution (`source/RSS Bandit.sln`) contains only 4 projects**, all already
SDK-style targeting `net5.0-windows10.0.19041`:

```mermaid
flowchart TD
    RssBandit[["RssBandit (WinExe)<br/>UI shell — 319 files"]] --> NewsComponents
    RssBandit --> AppServices["RssBandit.AppServices<br/>contract layer — 27 files, zero deps"]
    NewsComponents["NewsComponents<br/>feed engine — 104 files"] --> AppServices
    Tests["NewsComponents.UnitTests<br/>41 NUnit tests"] --> NewsComponents
    Tests --> AppServices
```

The other **26 projects are satellites** (plugins, child libraries, installer, build
tooling) on legacy csproj formats and .NET Framework 3.5/4.0. Almost all can be
ignored or dropped for the modernization milestone (see briefs).

God nodes (coupling hubs) from the knowledge graph: `FeedSource` (189 edges),
`RssBanditApplication` (180), `WinGuiMain` (161). The MSHTML/IE interop cluster that
also ranks top-10 lives entirely in the **vestigial IEControl satellite** — the main
app already migrated to WebView2 (old IEControl calls are commented out).

## Key findings

1. **A prior modernization pass already happened** (net5.0 + WebView2 + SDK-style core). The milestone is finishing it: net5.0 has been EOL since May 2022.
2. **Serialization is the #1 runtime blocker**: `SoapFormatter` (RssBanditApplication.cs:2258, 2320) + `BinaryFormatter` (UI feed state; engine DownloadRegistryManager/DownloadTask). BinaryFormatter is removed in .NET 9+.
3. **Commercial UI lock-in**: Infragistics WinForms 20.2.14 across 36 files (pervasive), SandDock docking in 7 files (core layout), Divelements wizard in 9 files (contained). License/compat verification is on the critical path.
4. **Legacy networking**: 18 `HttpWebRequest` occurrences in 10 engine files; zero HttpClient. BITS COM interop for enclosure downloads (8 files).
5. **Dead weight**: GoogleReader/NewsGator/Facebook feed sources are dead services; every plugin targets a dead service; IEControl/ShellLib/Jumplist are unreferenced; RssBandit.UnitTests can't build.
6. **Duplicated code**: RelationCosmos has its live copy inside NewsComponents (ChildProjects copy is stale); ThListView compiles directly into RssBandit.exe via SDK glob (its standalone csproj is vestigial).
7. **Contract layer is almost portable**: AppServices has zero dependencies but leaks WinForms types (`Keys`, `Image`) in 5 files — the one repair needed to make it a clean seam.
8. **Tests**: 41 real NUnit tests exist for the engine but 3 fixtures depend on the ancient Cassini web server binary.

## Recommended build-fix order → "smoothly running WinForms"

**Phase A — make it build (low risk, mechanical)** — ✅ **DONE 2026-06-12** (commit `1d6e4a17` on develop)
1. ✅ Retargeted the 4 main projects to `net10.0-windows10.0.19041` (net8 already near EOL at the time); bumped WebView2 1.0.4022.49, log4net 2.0.17, Newtonsoft.Json 13.0.4, NUnit 3.14.0 / adapter 4.6.0 / Test SDK 17.14.1, Unity 5.11.10, Nerdbank.GitVersioning 3.9.50; dropped the Microsoft.NETCore.Targets workaround and System.Windows.Extensions (in-box now).
2. ✅ Runtime-verified on .NET 10: app launches, all 8 Infragistics 20.2.14 assemblies + SandDock + WebView2 load and render; Divelements loads lazily with the wizard dialog. No fallback plan needed.
3. ✅ BinaryFormatter sites survive net10 via `System.Runtime.Serialization.Formatters` 10.0.9 + `EnableUnsafeBinaryFormatterSerialization` (proper Phase B replacement still pending); SoapFormatter NuGet package is TFM-safe.
4. ✅ Both COMReferences eliminated so the **pure dotnet CLI builds green** (no VS MSBuild): `stdole` deleted (unused), `Microsoft.Feeds.Interop` replaced by hand-written ComImport interop (`NewsComponents/Interop/MicrosoftFeedsInterop.cs`, derived from Windows SDK `msfeeds.idl`/`msfeedsid.h`), including a self-carried IEnumVARIANT→IEnumerator marshaler and a `ComEventsHelper`-based `IFeedFolderEvents_Event` (the .NET Framework TCE cast pattern is gone on modern .NET).
   - Build the projects, not the .sln (`RssBandit.Package.wapproj` is not CLI-buildable). Repo-scoped `packageSourceMapping` added to `source/NuGet.Config`.
   - Tests: 17/36 pass; all 19 failures pre-existing (17 Cassini/System.Web fixtures → Phase C item 9, 2 stale exception-type asserts in FileStorageDataServiceTests).
   - WFO1000 (WinForms designer-serialization analyzer) suppressed in Directory.Build.props — annotate the custom controls in Phase B/C.
   - Dead satellites (IEControl, ShellLib, plugins) were already absent from the 4-project build; nothing to drop.

**Phase B — make it run reliably** — ✅ **DONE 2026-06-12** (commits `de0f6f45`..`18577881` on develop)
5. ✅ Persistence on System.Text.Json: preferences (`.preferences.json` primary; SOAP/binary chain kept as one-time read shim — sandbox-verified migration incl. encrypted secrets) and the download registry (`*.task` JSON; legacy binary files discarded on load). NewsComponents no longer references BinaryFormatter at all; the Formatters package + unsafe flag remain only in RssBandit.csproj for the shim.
6. ✅ AsyncWebRequest/SyncWebRequest rewritten over HttpClient/SocketsHttpHandler (cached clients keyed by proxy/credentials/cert, HTTP/2 on async path, manual redirect loop preserving 301 new-URL signaling, conditional-GET quirks kept, per-handler cert-trust callback re-activating the dormant TrustSelectedCertificatePolicy). Live-network harness verified fetch/304/301/sync. NNTP + BITS untouched.
7. ✅ Dead sources removed (−8.3k lines): Google Reader, NewsGator, Facebook implementations, their UI chains and the Facebook package; FeedSourceType enum members kept for feedsources.xml compat; live migration shims kept. All commented MSHTML/IEControl corpse code deleted; `CreateAndInitIEControl`→`CreateAndInitWebView`, `ScrollHtmlControl`→`ScrollWebBrowser`.
8. ✅ Lucene.Net 4.8.0-beta00017 (+Analysis.Common, +QueryParser; SharpZipLib explicit at 1.4.2 fixing the 0.86 advisory). Field semantics and date format preserved; old/corrupt index auto-wiped at startup and rebuilt via the existing re-index path (harness + in-app verified).

**Phase C — health**
9. Replace Cassini with WireMock.Net; consolidate tests on NUnit; wire up CI (`dotnet build` + `dotnet test` on the 4-project solution).
10. Mime4Net → MimeKit if NNTP support is kept; otherwise drop NNTP.

## MAUI feasibility (the longer game)

- **UI shell: rewrite, not port.** SandDock docking, Infragistics grids, tray/P-Invoke UX are desktop-WinForms idioms.
- **Engine: extractable.** A `NewsComponents.Core` (BanditFeedSource + RSS/Atom parsing + storage + search) can go cross-platform once BITS, WindowsRssFeedSource, WindowsAPICodePack, and the 9 DllImports are isolated behind interfaces.
- **AppServices is the seam.** Strip the WinForms types from 5 contract files and it becomes the portable boundary both shells share.
- Realistic sequence: finish the WinForms milestone first (Phases A–C), then extract the portable core, then evaluate MAUI/Avalonia against it. Avalonia is worth weighing alongside MAUI for a desktop-first reader.

## Token cost of this exercise

Graph construction: 505 code files via free AST extraction; ~31k Haiku tokens for
readme semantics. Module briefs: ~161k Haiku tokens across 6 scoped agents.
Total agent spend ≈ 192k Haiku tokens (≈ $0.25 API-equivalent) — the structural
work was deterministic, models were spent only on interpretation.
