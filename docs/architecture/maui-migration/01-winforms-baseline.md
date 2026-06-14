# Spec 1 — WinForms .NET 10 baseline & stabilization

*Part of the [MAUI migration handoff](00-README.md). Status: not started.*

## Purpose

Record the achieved .NET 10 WinForms modernization (the starting shape), define what
"stable" means, drive it there, and then cut the preserved `winforms` lineage branch. This
is the foundation the rest of the migration stands on: a WinForms app good enough to keep
shipping as the Windows head through the entire dual-head journey.

## The achieved baseline (document, don't redo)

As of 2026-06-14, on `develop`:

- **Retargeted to `net10.0-windows10.0.19041`**, SDK-style, builds via the pure `dotnet` CLI
  (build the csproj files, not the `.sln`/wapproj).
- **Networking:** HttpClient-native (`Net/HttpClientCache.cs` + `HttpClientResponse` adapter).
- **Persistence:** System.Text.Json (`.preferences.json`); the SOAP/binary read-shim is the
  only remaining legacy formatter use.
- **Search:** Lucene.Net 4.8 (auto-wipe + rebuild of corrupt indexes at startup).
- **Tests:** WireMock.Net + NUnit, **38 passed / 0 failed** (`NewsComponents.UnitTests`).
- **CI:** GitHub Actions (`.github/workflows/ci.yml`, windows-latest).
- **Removed:** NNTP (−19.9k lines), **all COM in the active build** (Windows RSS Platform,
  BITS, iTunes/WMP, ThumbCache), the legacy IE/WinInet cookie+cache bridge.
- **`MimeType`** stripped to its managed surface (no `urlmon`, no new dependency).
- Enclosures open in the default handler (the podcast "step A").

## What "stable" means — the checklist

1. **A real-usage shakedown** *(the gate that turns "launches" into "works")*. Drive it as a
   reader against live feeds: subscribe, refresh, read (HTML render in WebView2), full-text
   search (Lucene), flag/mark-read, manage subscriptions and categories, restart and confirm
   state persists. File every defect found. This is the single most important item — the app
   has been built and launched, never *used*.
2. **Retire the prefs SOAP/binary read-shim.** When it ages out, drop the `Formatters` package
   and `EnableUnsafeBinaryFormatterSerialization` from `RssBandit.csproj`.
3. **Finish zero-COM repo-wide** (per `../modernization-next-steps.md`, step 1): delete the
   build-excluded `ChildProjects/IEControl` (MSHTML COM) and `ShellBasics` (shell COM), and
   replace the 2003 `Microsoft.ApplicationBlocks.ExceptionManagement` block (referenced by
   `RssBandit.csproj`, contained to ~4 files in `Core/`).
4. **Cosmetic/orphan cleanup:** installer `.wxs`/`.ism` references to deleted assets; orphaned
   resx strings (incl. the now-stale `PodcastOptionsDialog` iTunes/WMP captions); WFO1000
   designer-serialization annotations (the `Directory.Build.props` NoWarn is debt).
5. **Confirm the identity-editor dialog** is genuinely restored (it was a no-op stub after the
   NNTP drop; a PR claimed to restore it — verify in the usage shakedown, items 1 + 5 overlap).
6. **Optional hardening:** enable nullable reference types + analyzers, leaning on CI to hold
   the line.

## Done-criteria

- Builds clean (0 errors), `NewsComponents.UnitTests` 38/0, CI green.
- **The usage shakedown passes** — a human can actually read feeds with it end to end.
- The checklist above is cleared (or items explicitly deferred with reasons recorded).

## Then: the preservation branch (for Dare)

When the WinForms head is declared stable, cut a canonical branch off `develop` and tag it:

```bash
git branch winforms develop
git tag winforms-net10-stable
git push origin winforms --tags
```

`develop` continues toward core/UI separation and MAUI (Specs 2–4). `winforms` is the
preserved, maintained resting place of the modernized WinForms app — the lineage Dare
Obasanjo started, kept on purpose. Bug-fixes for the Windows desktop head can land there
and be merged forward; it is not a dead branch, it is the WinForms home.

## TDD for this spec

Per the [strict-TDD discipline](00-README.md#engineering-discipline-strict-tdd):

- **Shakedown defects (item 1) are test-first fixes** — each bug found gets a failing test
  that reproduces it (a `NewsComponents.UnitTests` case where the bug is in the engine; a
  ViewModel/characterization test where it's higher up), then the fix to make it green. The
  shakedown is also the place to *grow* the suite past 38: it's the first time the reader loop
  is exercised, so it's the cheapest time to capture its expected behavior as tests.
- **Refactors (items 2–3: shim retirement, ChildProjects/ExceptionManagement removal) get
  characterization tests first** — pin the current observable behavior, then change code green.
- The COM/ChildProjects deletions are dead-code removals (no behavior to preserve), but the
  `ExceptionManagement` replacement *does* change a code path — lock the exception-publishing
  behavior with a test before swapping it.

## Notes for the executor

- Build the **csproj** files, never the `.sln`/wapproj (the wapproj dies under the CLI on
  `Microsoft.DesktopBridge.props`).
- The runtime gate is: launch, confirm a healthy main window, empty `error.log`, no new
  `FATAL` in `trace.log`. For the usage shakedown, that's necessary but not sufficient —
  actually click through the reader loop.
- This spec is mostly *finishing and verifying*, not large new code — but item 1 (the
  shakedown) may surface real bugs that become their own test-first fixes.
