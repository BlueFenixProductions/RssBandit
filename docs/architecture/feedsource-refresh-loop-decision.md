# Decision: the FeedSource refresh loop — extract the merge, retain the orchestration

**Status:** Decided 2026-06-26, grounded in a full architect read of the refresh/fetch cluster.
This is the **last** entry in the FeedSource decomposition (`feedsource-decomposition-spec.md`).
It is an ADR as much as a spec: it records *why the decomposition stops here.*

## Context

Five concerns have been extracted from the `FeedSource` god-object behind interfaces, each
characterization-tests-first and proven behavior-neutral: settings, favicon, feed-list save,
feed-list parse, and the search-index sink. The remaining target was the refresh/fetch loop
(Concern S, ~900-1000 lines) — the highest-betweenness, highest-risk code: async,
event-driven, lock-touching, and the **live feed-fetch hot path**.

On inspection, the refresh loop is **not** another cohesive slice. It splits into two
populations with opposite risk/reward.

## The split

### One genuinely pure sub-concern — `MergeAndPurgeItems`  → **extract (S1)**
`MergeAndPurgeItems` (`source/NewsComponents/Core/FeedSource.cs:5365-5473`) reconciles newly
fetched items against the cache (carries over read/flag/date/comment/enclosure state, drops
deleted ids, collects genuinely-new items). It is **already `static`**, has **exactly one
caller** (`FeedSource.cs:4004`), and touches **none** of `itemsTable`/`feedsTable`/events — it
is essentially a pure function over `(old, new, deleted, flags)` that merely lives in the wrong
file with two globally-controlled static dependencies (`RelationCosmos*`,
`ReceivingNewsChannelServices.ProcessItem`). Real cohesion gain, near-zero risk, fully
unit-testable without async or WireMock. **This is the defensible final extraction.**

### The async orchestration core — `RefreshFeeds*` / `OnRequest*` / `OnAllRequestsComplete`  → **DO NOT extract**
This is the **irreducible core** of `FeedSource`. Its reason for existing is to mutate
`itemsTable` in response to async network events and raise the resulting notifications. It is
woven into:
- **two locked dictionaries** — every mutation uses `lock(itemsTable)` (`itemsTable` is
  `readonly`, `FeedSource.cs:392`); the merge-swap `itemsTable.Remove(url)` →
  `itemsTable.Add(url, fi)` under one lock (`4038`/`4048`) *is* "the feed becoming loaded";
- the **public event surface** (8 `On*`/`Update*` events that the GUI subscribes to,
  e.g. `RssBanditApplication.EventHandlers.cs:203`);
- the **live hot path** (`ThreadWorker.cs:133` → `RefreshFeeds(force)` on a background thread);
- two completion paths with a load-bearing asymmetry (`SearchIndexSink.Flush()` runs only on
  the `AsyncWebRequest`-callback path, `FeedSource.cs:4161`, not the `RefreshFeeds` `finally`
  path `4780`/`4896`).

Hiding this behind an `IRefreshEngine` would require a ~20-member inbound surface, would have
to raise `FeedSource`'s public events back across the seam, and would leave **two objects
sharing the same mutable dictionaries and the same lock objects across a method-call boundary
— strictly worse than one object** (lock discipline now spans a seam). **Negative cohesion
gain. Reject.** This is motion, not progress, and it risks the live feed-fetch path with no
deterministic test net beneath it (confirmed: no existing test drives a refresh to completion;
the async tests in `AsyncWebRequestTests.cs:26-180` are commented out; `SearchIndexSinkTests.cs:46-53`
documents the gap).

## The decision

1. **GO — S1:** extract `MergeAndPurgeItems` into a pure `internal static class ItemMerger`
   (`source/NewsComponents/Core/`), leaving a 1-line delegating `FeedSource.MergeAndPurgeItems`
   wrapper (the single call site at `:4004` and the `public static` API unchanged) — the same
   "keep the old member delegating" pattern as the prior five slices. Pair it with a pure-function
   characterization test class (no WireMock, no async, fully deterministic).
2. **GO (optional, standalone) — S2:** add an end-to-end refresh characterization harness — drive
   `RefreshFeeds(true)` against the existing WireMock fixture and await `OnAllAsyncRequestsCompleted`
   (a `ManualResetEventSlim` on the event), asserting items land in `itemsTable` and
   `OnUpdatedFeed` fires. This fills the documented test gap (it would have covered Slice 4's
   `d`/`e` refresh-hot-path sites) and is valuable on its own merits. It is **test-debt repayment,
   not a prerequisite for extracting the orchestration** (which we are not doing). Caveats: small
   fixtures (the loop has `Thread.Sleep(15)` per feed at `:4756`/`:4873`); assert on observable
   end-state, not timing; tolerate either completion path.
3. **NO-GO — the orchestration extraction**, per the reasoning above.
4. **STOP the FeedSource decomposition after S1** (+ optionally S2). This is the correct
   engineering call: there is exactly one safe thing worth extracting in the refresh loop, and
   the rest is the irreducible core that belongs on `FeedSource`. "Spec it, don't rush it"
   resolves, on inspection, to "extract the merge, keep the engine."

## S1 execution plan (characterization-first)

- **Task 1 — pin `MergeAndPurgeItems` as a pure function.** Add `ItemMergerTests` constructing
  `old`/`new`/`deleted` item lists directly (the `NewsItem` ctor is already used in
  `SearchIndexSinkTests.cs:114`), with `buildRelationCosmos=false` (`FeedSource.cs:357`) and no
  registered news channel to neutralize the two statics. Pin: read/flag/date carry-over
  (`:5394`/`:5408`/`:5412`), deleted-id suppression (`:5380`), `receivedNewItems` contents,
  `onlyKeepNewItems` true→returns `new` / false→returns `old` (`:5467-5472`), comment-count and
  enclosure-download merge (`:5419`/`:5442`), and the argument-locking (`lock(oldItems)` at
  `:5373` travels with the function). Green against the monolith.
- **Task 2 — extract + delegate.** Move the body into `ItemMerger.Merge(...)` (same signature;
  the two static dependencies stay as static calls — the green suite proves equivalence);
  `FeedSource.MergeAndPurgeItems` becomes a 1-line delegation. All tests pass UNEDITED.
- **Gate:** build `RssBandit.csproj` 0 errors; the full suite green (115 + new); runtime gate
  (feeds refresh — the live `OnRequestComplete` calls the delegating wrapper).

## Why this is the right place to stop

`FeedSource` started at 7,002 lines. Five slices + S1 carve out every concern that is a
*concern hiding inside* the object. What remains — the async orchestration that mutates the
feed/item tables in response to the network and notifies the UI — is not a concern hiding
inside `FeedSource`; it **is** `FeedSource`. Extracting it would relocate the hardest, most
state-coupled code across a lock-spanning seam for no coupling reduction. The decomposition has
reached its natural, principled endpoint. The portable-core goal (for the eventual MAUI split)
is served by the *interfaces already extracted*, not by gutting the engine.
