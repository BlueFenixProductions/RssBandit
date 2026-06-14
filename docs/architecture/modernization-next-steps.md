# Modernization next steps (graph-driven, 2026-06-14)

*Derived from the refreshed graphify code graph (AST over all 492 C# files under
`source/`, rebuilt 2026-06-14 at merge `c6c7824f`). Supersedes the planning notes
in `CODE_GRAPH.md` for "what's next."*

## Graph snapshot

The graph was rebuilt after the COM/WinInet/MIME arc (it had been stale at a
Phase-B-era commit, still full of NNTP/COM ghost nodes):

| | Old (Phase B) | Now (`c6c7824f`) |
|---|---|---|
| nodes | 10,124 | 9,253 |
| edges | 16,287 | 14,826 |
| communities | 613 | 573 |

−871 nodes is the NNTP drop (Phase C) plus today's COM/WinInet/MIME removals showing
up structurally. Extraction is 97% EXTRACTED / 3% INFERRED, AST-only (the 107 images
and 3 videos under `source/` were deliberately skipped — they're icon/resource assets,
not decision-relevant, and would cost ~110 vision subagents). Re-run anytime with
`/graphify source --update` — the manifest was reset to current paths, so incremental
now works (it previously pointed at a deleted worktree).

## What the graph is telling us

**God nodes (most-connected — the structural debt):**

1. `FeedSource` — 186 edges, **betweenness 0.110 (the #1 cross-community bridge)**
2. `RssBanditApplication` — 181 edges
3. `WinGuiMain` — 162 edges, betweenness 0.105
4. `IHTMLDocument2` — 110 edges
5. `HtmlControl` — 91
6. `IHTMLElement` — 89
10. `IHTMLElement2` — 82

Two findings fall straight out of that list:

- **The three real god-objects are `FeedSource`, `RssBanditApplication`, `WinGuiMain`** — the feed engine, the app coordinator, and the WinForms main form. They're the cross-community bridges (high betweenness), which is the textbook decomposition signal.
- **Four of the top ten god nodes are MSHTML COM interfaces** (`IHTMLDocument2`, `HtmlControl`, `IHTMLElement`, `IHTMLElement2`). They live in `source/ChildProjects/IEControl`, which is **excluded from the active build and superseded by WebView2** — yet still pollutes the graph and the tree. "Zero COM" is true for the active build; it is *not* true for `source/` as a whole.

## Proposed steps (in recommended order)

### Step 1 — Retire the dead ChildProjects (cheapest, finishes zero-COM repo-wide)

`source/ChildProjects/` and `source/AddIn/` are excluded from the build (per
`Directory.Build.props`) but carry COM and dead-tech the graph keeps surfacing.
Confirmed **unreferenced by the active csproj** (`RssBandit`, `NewsComponents`,
`RssBandit.AppServices`):

- **`IEControl` + `Test_IEControl` + `IE.Control.2010.sln`** — MSHTML/`SHDocVw` COM, the old IE-embedding control. WebView2 replaced it. Delete.
- **`ShellBasics`** — shell COM (`IShellFolder`, `IAutoComplete`, `IACList`). Verify no use, then delete.
- **`MemeTracker`, `FireFox Feed Context Menu`, `Jumplist`, `ShortcutsEditor`, `DiffPatchResources`, `ChildProjects/RelationCosmos`** (the live `RelationCosmos` lives under `NewsComponents/`, not here), **`AddIn/AdsBlocker`** — assess each; most are dead tools/experiments. Confirm-then-delete.
- **Handle carefully:** `BanditBuildTasks` may be wired into the build via `UsingTask` (custom MSBuild tasks). Check before touching.

Payoff: removes the four MSHTML god nodes and a pile of weakly-connected nodes (the report flags ~2,370 weakly-connected nodes, largely these), making every future graph query and god-node list reflect only live code. Plus it makes "no COM anywhere in `source/`" literally true.

**Verification:** the active build already ignores these, so `dotnet build` of the
csproj files is the gate; the graph should drop the MSHTML god nodes on the next
`--update`.

### Step 2 — Replace the Enterprise Library exception block (last ancient-MS-framework dep)

The active build still `<Reference>`s `Microsoft.ApplicationBlocks.ExceptionManagement`
(+ `.Interfaces`) — the 2003 Enterprise Library 1.0 Exception Management Application
Block, sourced from `ChildProjects/ExceptionManagement` and shipped as a binary in
`Common/Libraries/`. Usage is contained:

- `Core/BanditApplicationException.cs` — `inherits BaseApplicationException`
- `Core/BanditExceptionPublisher.cs` — `implements IExceptionPublisher`
- `Core/RssBanditApplication.Statics.cs`, `Core/RssBanditApplication.EventHandlers.cs` — `ExceptionManager.Publish` calls + the `using`

Replace `BaseApplicationException` with a plain `Exception` subclass and the
`ExceptionManager.Publish` / `IExceptionPublisher` pattern with the existing log4net
path (or a thin local publisher), then drop both references and delete the
`ChildProjects/ExceptionManagement` source. This is the same shape as the COM
removals: a dead Microsoft framework with a small, contained call surface and a
managed replacement already present in the app.

### Step 3 — Decompose `FeedSource` (the #1 god-object — and the podcast spec is its first slice)

`FeedSource` is the highest-betweenness node in the graph: a ~7,000-line static-and-
instance grab-bag that bridges 11+ communities. It does feed fetching, parsing
orchestration, the source-type factory, per-feed/category settings, search-index
wiring, **and** the enclosure/download surface. Decompose it one concern at a time,
behind the interfaces that already exist:

- **First slice = the podcast/enclosure engine deletion** (`docs/architecture/podcast-simplification-spec.md`, sub-steps B+C). Removing the enclosure surface from `FeedSource` is both a feature decision the captain has already approved *and* the lowest-risk way to start shrinking the god-object — it cuts a whole concern out of the refresh loop.
- **Then:** extract the per-feed/category settings accessors (the `Set/Get*` cluster) into a settings type; isolate the search-index wiring behind `SearchHandler`; consider splitting the `FeedSource` static configuration from the instance behavior.

`RssBanditApplication` (181 edges) and `WinGuiMain` (162) are the same shape — already
partial classes spread across ~15 and ~10 files respectively — but they're riskier and
lower-urgency than `FeedSource`. Treat them as later passes; the graph will show the
betweenness drop as `FeedSource` sheds concerns.

### Step 4 — The carried-over backlog (not graph-driven, but real)

From the prior planning notes, still open:
- Prefs **SOAP/binary read-shim retirement** — when it ages out, `RssBandit.csproj` drops `SoapFormatter`, the `Formatters` package, and `EnableUnsafeBinaryFormatterSerialization`.
- **Installer `.wxs`/`.ism` orphans** — references to deleted Facebook/Mime4Net assets, now plus the COM/WinInet deletions.
- **Orphaned resx strings** — incl. the now-stale `PodcastOptionsDialog` iTunes/WMP captions and wizard NNTP strings.
- **WFO1000** designer-serialization annotations (the `Directory.Build.props` NoWarn is debt).
- **Nullable + analyzer enablement** — natural now that CI exists to hold the line.
- Then the **MAUI/Avalonia** portable-core question.

## Recommended sequence

1 → 2 first (both are bounded, low-risk deletions/replacements that finish the
"retire dead Microsoft tech" arc the COM work started, and clean the graph). Then the
**podcast slice of Step 3** (already spec'd). Then weigh the deeper `FeedSource`
decomposition against the backlog and the MAUI question — that's the next real fork,
and it's worth one focused discussion rather than guessing.

## Keeping the graph honest

- `/graphify source --update` after a wave of changes (incremental, AST-only for code, no LLM).
- `graphify hook install` would auto-rebuild on every commit if you want it always-current.
- The `git`-stamped `built_at_commit` in `graph.json` tells you how stale it is at a glance.
