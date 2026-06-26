# Spec: Decompose `FeedSource` (the #1 god-object), characterization-first

**Status:** Approach drafted 2026-06-26 (grounded in a full read of the current code).
Not started. **Owner decision:** Chris ("Captain").
**This is modernization-next-steps Step 3.** It is deliberately a spec, not a task —
per standing guidance: *spec the FeedSource surgery, don't rush it.*

## 1. Summary

`source/NewsComponents/Core/FeedSource.cs` is a **7,002-line abstract class** and the
highest-betweenness node in the code graph — it bridges 11+ communities. It bundles ~26
distinct concerns (factory, static engine state, events, settings accessors, refresh/fetch
orchestration, serialization, search wiring, favicons, categories, …). The base class holds
nearly all the mass; the two remaining concrete subclasses (`BanditFeedSource`,
`FeedlyCloudFeedSource`) override almost nothing, so the decomposition targets the **base
class internals**, not the subclass seam.

This spec establishes the **method** for shrinking it safely and executes the **first
slice**. The method — characterization-tests-first, extract-behind-an-interface, keep the
old public members as thin delegations (zero caller churn) — is reused for each subsequent
concern. It serves the modernization goal directly: a cohesive, independently-testable
engine core is the prerequisite for the eventual MAUI core/UI separation (Spec 2). Note:
`NewsComponents` is **already UI-free** (no `System.Windows.Forms`/`System.Drawing` in
`FeedSource.cs`), so this work is about *internal cohesion*, not stripping WinForms.

## 2. The method (applies to every extraction)

1. **Pin behavior first.** Write characterization tests in `NewsComponents.UnitTests` that
   assert the concern's *current* behavior against the unchanged monolith. They go green
   before any production change and become the contract.
2. **Extract behind an interface.** Define the interface in `RssBandit.AppServices`
   (the portable contracts layer — already `<Nullable>enable</Nullable>` per PR #9); put
   the implementation in `NewsComponents`; move the private engine + helpers in.
3. **Delegate, don't churn.** Keep the existing public `FeedSource` members as one-line
   delegations to the new component, so the ~hundreds of `entry.Source.GetXxx(...)` call
   sites across `RssBandit` are untouched. No signature changes.
4. **Verify unchanged.** Re-run the new characterization tests *and* the full suite
   (baseline **36/0**). All green = behavior preserved. Build the csproj files, not the
   `.sln`. Runtime-gate per the project rhythm (launch, feeds refresh, no new FATAL).
5. One concern per commit/PR. Stop between slices; this is not a single sweep.

## 3. First slice — extract the per-feed/category settings accessors (Concern Q)

### Why this concern first
- **Lowest risk:** pure in-memory get/set over `feedsTable`/`categories`; **no network, no
  async, no serialization format, and — critically — no static-global state** (the file's
  static singletons `SearchHandler`, `relationCosmos`, `TopStoryTitles`, `globalProxy`,
  `Offline` are *not* touched by this concern).
- **Cleanest boundary:** a ~510-line cohesive cluster — `FeedSource.cs:3267-3695` plus the
  reflection helpers `5885-5967`.
- **Highest clarity-per-risk:** removes the most reflection-heavy, stringly-typed code into
  a named, independently testable unit, and proves the extract-behind-interface-with-
  delegation pattern for the riskier later cuts.
- **Testable in isolation** (no WireMock) and **zero caller churn** (delegation).

### Members that move into the component
- Private engine: `IsPropertyValueSet` (3267), `GetFeedProperty` ×2 (3299, 3311),
  `SetFeedProperty` (3372), `GetCategoryProperty` (3509), `SetCategoryProperty` (3554),
  and the static `GetSharedPropertyValue` (5885) / `SetSharedPropertyValue` (5920).
- Public typed accessors (kept on `FeedSource` as **delegations**):
  `Get/SetMaxItemAge`, `Get/SetRefreshRate`, `Get/SetStyleSheet`,
  `Get/SetFeedColumnLayoutID`, `Get/SetMarkItemsReadOnExit` (3404-3501) and the
  `Category` variants (3598-3695). Consider also the `ResetAll*Settings` trio
  (1951, 2165, 2198).

### The constructor seam (load-bearing)
`GetFeedProperty` seeds each property's default from `GetSharedPropertyValue(this, …)`
(3313) — the owning `FeedSource`'s *own* `ISharedProperty` implementation supplies the
top-level fallback (MaxItemAge, static Stylesheet, static MarkItemsReadOnExit, config
RefreshRate; 1780-1906). So the component takes three injected references plus the static
separator:

```csharp
new FeedAndCategorySettings(
    ISharedProperty topLevelDefaults,                  // the owning FeedSource
    IDictionary<string, INewsFeed> feeds,              // feedsTable
    IDictionary<string, INewsFeedCategory> categories) // categories
// + FeedSource.CategorySeparator (static, 362)
```

No other state is required.

### New interface
`IFeedAndCategorySettings` in `source/RssBandit.AppServices/Core/` — the typed get/set
surface for the five shared properties at feed and category level, with category-chain
inheritance. Implementation `FeedAndCategorySettings` in `NewsComponents`. `FeedSource`
holds a lazily-constructed instance; the 16 public accessors become one-liners.

### Characterization tests to write FIRST (before any production change)
Add `FeedAndCategorySettingsTests` to `NewsComponents.UnitTests` (reuse
`WebServerTestFixture`, `SearchIndexBehavior.NoIndexing`, and `FeedList04Feeds.xml`'s
nested categories). Pin, at minimum:
- per-feed override sets and auto-flags `...Specified` (`SetMaxItemAge`→`GetMaxItemAge`),
  including the `TimeSpan` ↔ `XmlConvert` string round-trip (3380-3382);
- **category-chain inheritance**: `GetMaxItemAge(..., inheritCategory: true)` walks
  `category.parent` until a value "is set" (3335-3358);
- the **`maxitemage == TimeSpan.MaxValue` "unset" sentinel** in `IsPropertyValueSet`
  (3278-3281, 3315-3317) — the single most fragile piece; cover it explicitly;
- config/static fallback when nothing is specified (`GetRefreshRate`→
  `p_configuration.RefreshRate`; `GetStyleSheet`→ static `Stylesheet`);
- `SetCategoryProperty`'s prefix-match over `category + CategorySeparator` (3566);
- the **stringly-typed keys** (`"maxitemage"`, `"refreshrate"`, `"stylesheet"`,
  `"listviewlayout"`, `"markitemsreadonexit"`) and the `name + "Specified"` convention
  (3389) routed through the `GetSharedPropertyValue` switch (5887) — a typo silently falls
  into the `default` branch, so pin the mapping.

### Gate for the slice
`dotnet build source/RssBandit/RssBandit.csproj -c Release` 0 errors; new tests green;
full `NewsComponents.UnitTests` **36/0** (plus the new tests); runtime gate (launch, feeds
refresh, no new FATAL, empty `error.log`).

## 4. Subsequent targets (priority order, same method)

1. **Favicon storage (Concern N, `FeedSource.cs:2683-2825`)** behind `IFaviconStore` —
   smallest clean win; depends only on `feedsTable` + `UserCacheDataService`. (The favicon
   *download* half, Concern T, stays for now — it needs WireMock + image sniffing.)
2. **Feed-list serialization (Concerns O + V, `2832-3008` + `5272-5605`)** behind
   `IFeedListSerializer` (`SaveFeedList`/`ImportFeedlist`/`ConvertFeedList`). Attractive
   because it **already has the best existing test coverage** (`ImportTwoFeedlists`,
   `ImportFeedListCategoryItemsAreImported`, the `SaveFeedList` round-trip), so
   characterization is largely in place — but it is **back-compat sensitive** (see Risks).
3. **Search-index wiring** behind an `ISearchIndexSink` — decouples the Lucene static
   singleton (`SearchHandler`, 1338-1353) from the engine and the factory. Do this **after**
   the pattern is proven: it touches the refresh hot path (`OnRequestComplete` 4417,
   `OnAllRequestsComplete` 4512) and `CreateFeedSource` (242-246).

The **refresh/fetch orchestration (Concern S, ~900 lines, the betweenness hot-spot)** is the
ultimate prize but the highest risk — async, event-driven, `Thread.Sleep`, static-singleton
flush. Leave it until several lower-risk slices have proven the method and shrunk the
surrounding surface.

## 5. Risks & landmines (carry into every slice)

- **Static / global mutable state** — `SearchHandler` (1338), static `relationCosmos`
  (352), `receivingNewsChannel` (347), static mutable `TopStoryTitles` (1590),
  `globalProxy` (1098), `Offline`/`SetCookies`/`BuildRelationCosmos` (1359-1391). Any
  concern touching these must preserve singleton identity, and they wreck parallel
  test-isolation. **Concern Q touches none of them** — the reason it is the safe first cut.
- **Refresh-loop hot path** — `GetRefreshRate` is read per-feed inside `RefreshFeeds`
  (5059, 5084). The extraction must stay a thin synchronous in-memory read so this path is
  byte-for-byte identical (delegation guarantees it). Do **not** "tidy" the loop's
  `Thread.Sleep(15)` (5107) or its swallowed `InvalidOperationException` from concurrent
  `feedsTable` mutation (5122).
- **Mixed threading model** — the class doc says "NOT thread-safe" (158) yet
  `feedsTable`/`categories`/`certCache` are `ConcurrentDictionary` while `itemsTable` is a
  plain `Dictionary` under `lock(itemsTable)`. The settings cluster reads
  `feedsTable`/`categories` **without** locks today — preserve that exactly; do not add
  locking that changes timing.
- **Serialization / back-compat** — the native list is `XmlSerializer` over the `feeds`
  type with XSD validation. The inert `ISharedProperty` enclosure stubs (1816-1875) and the
  `feedlist.*Specified=false` writes in `SaveFeedList` (2940-2946) exist **only** so legacy
  `feedsources.xml`/subscription files round-trip — they must survive any J/O extraction
  untouched. `feedsources.xml` itself is `FeedSourceManager`'s concern, not FeedSource's.
- **`maxitemage` representation duality** — stored as an `XmlConvert` string but surfaced
  as `TimeSpan`, with `TimeSpan.MaxValue` overloaded as the "unset" sentinel. The most
  fragile logic in Concern Q; pin it in characterization tests before moving it.

## 6. What this is NOT

- Not a single sweep — one concern per PR, gated, with the option to stop after any slice.
- Not a rewrite — behavior is preserved (characterization tests are the proof); this is
  cohesion/testability, not new features.
- Not the MAUI migration — but it is the groundwork Spec 2 depends on.

## 7. Suggested first execution

Slice 1 (Concern Q) is well-suited to superpowers subagent-driven-development: Task 1 =
write the characterization tests (green against the monolith); Task 2 = extract
`IFeedAndCategorySettings` + delegate; with a review gate and runtime gate after each. The
two existing harnesses to extend are `BanditFeedSourceTests.cs` and `WebServerTestFixture`.
