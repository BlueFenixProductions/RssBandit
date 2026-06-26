# Decision: search-index "4b" residual — NO-GO (leave the static SearchHandler)

**Status:** Decided 2026-06-26, grounded in a full architect read of the residual surface.
A coda to the FeedSource decomposition ([spec](feedsource-decomposition-spec.md),
[refresh-loop decision](feedsource-refresh-loop-decision.md)), recorded so 4b isn't re-litigated.

## Context

Slice 4 extracted `FeedSource`'s **instance** search-index call sites behind `ISearchIndexSink`
(a per-call pass-through to the static `FeedSource.SearchHandler`) and deliberately tagged three
things "4b": the static factory registration `SearchHandler.AddNewsHandler(handler)` in the static
`CreateFeedSource` (`FeedSource.cs:242-246`), the public static `SearchHandler` property
(`FeedSource.cs:1442-1457` → `FeedSourceManager.SearchHandler:780-807` → the sealed `LuceneSearch`
singleton), and the app-layer consumers that call `FeedSource.SearchHandler.X` directly.

## The verified residual

The app-layer consumers (the complete set; grep-confirmed in `source/RssBandit/`):

| Site | Call | R/W | Context |
|---|---|---|---|
| `RssBanditApplication.Commands.cs:672` | `IndexRemoveAll()` | W | `CmdDeleteAll` (GUI command) |
| `RssBanditApplication.cs:1092` | `IsIndexRelevantChange(prop)` | R | `HandleIndexRelevantChange` |
| `RssBanditApplication.cs:1096` | `IndexRemove(feed.id)` | W | same |
| `RssBanditApplication.cs:1104` | `ReIndex(feed, items)` | W | same |
| `RssBanditApplication.cs:4887` | `StopIndexer()` | W | `SaveApplicationState` (shutdown) |
| `WinGuiMain.OwnerInteraction.cs:1282` | `CheckIndex(true)` | W | `OnApplicationIdle` idle task |
| `WinGUIMain.cs:1711` | `ValidateSearchCriteria(...)` | R | `OnSearchPanelBeforeNewsItemSearch` (`async void`) |

The actual **query** path is *already* facaded: the GUI calls `owner.FeedSources.SearchNewsItems(...)`
(`FeedSourceManager.cs:818`), which internally uses `SearchHandler.ValidateSearchCriteria`/
`ExecuteSearch`. The residual static reaches are a grab-bag of **lifecycle/admin verbs** (reset, stop,
idle-build, reindex-on-property-change) operating the one process-wide index.

## The decision: NO-GO

Routing this behind a facade is the **same "motion, not progress" trap** as the refresh
orchestration, for three independent reasons:

1. **It doesn't reduce coupling — it relocates a static reach-through.** A production
   `ISearchOperations` impl would still read `FeedSource.SearchHandler` per call. For a genuine
   process-wide singleton, the static access is the *honest* representation; the indirection is
   cosmetic.
2. **No test net, and one can't cheaply be built.** `RssBandit.UnitTests` has zero coverage of
   `RssBanditApplication`/`WinGuiMain` (WinForms-coupled god-objects with no constructible harness),
   and every site lives in a GUI command / idle handler / `async void` / shutdown path. The
   decomposition's core discipline — characterization-tests-first — is **impossible** here.
3. **Highest blast radius for ~zero cohesion gain.** The payoff is "the GUI no longer names a
   static." The single on-disk Lucene index per process is intrinsic by design
   (`FeedSource.cs:1450`); the static `SearchHandler` is the legitimate app-wide search entry point
   and **should stay static.** The factory `AddNewsHandler` site (`:245`) is `FeedSource` touching its
   own static — no layer crossed — and routing it would erode the clean 6-member `ISearchIndexSink`
   for a single call the test suite can't even observe (it's skipped under `NoIndexing`).

## Outcome

**Change no code.** Slice 4 already extracted the part that mattered — `FeedSource`'s own recurring
instance index mutations, now observable and pinned (and, via the [S2 refresh harness](feedsource-refresh-loop-decision.md),
the two refresh-hot-path sites are now tested too). The residual is correctly understood as a static
singleton plus untestable GUI admin verbs — leave it. This mirrors the refresh-loop ADR: there was
"exactly one safe thing worth extracting," and the rest is the irreducible core. The portable-core
goal is served by the interface already extracted, not by relocating the singleton.
