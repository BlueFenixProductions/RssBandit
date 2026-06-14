# RssBandit modernization → MAUI → cross-platform: migration handoff

*Authored 2026-06-14 as a handoff. Nothing here has been executed — these are specs to
pick up, each sized as its own arc with build + test + runtime gates (the way the COM and
podcast specs were run).*

## Why this exists

RssBandit builds on the pure `dotnet` CLI for `net10.0-windows`, launches, passes 38/0
tests with green CI, and as of 2026-06-14 has zero COM in the active build, the legacy
IE/WinInet bridge retired, and a lean managed `MimeType`. **But it has not yet been driven
through a real reading session** — it compiles and launches; it hasn't been *used*. This
handoff defines the road from "it launches" to "it's a cross-platform reader," in four
independently-executable specs plus this index.

## The four specs

| # | Spec | Purpose |
|---|---|---|
| 1 | [`01-winforms-baseline.md`](01-winforms-baseline.md) | Document the achieved .NET 10 WinForms shape, define "stable," drive it there, then cut the preserved `winforms` lineage branch. |
| 2 | [`02-core-ui-separation.md`](02-core-ui-separation.md) | **The linchpin.** Extract a UI-agnostic, cross-platform-ready core both UIs sit on. |
| 3 | [`03-maui-ui.md`](03-maui-ui.md) | A new .NET MAUI front-end on the shared core — a functional reader first. |
| 4 | [`04-xplat-rollout.md`](04-xplat-rollout.md) | Android → macOS Catalyst → iOS, with platform concerns handled. |

## Decisions (the record)

- **Phased dual-head.** Build MAUI on a shared portable core for Android/iOS/macOS Catalyst
  first; keep the mature WinForms app as the Windows desktop head; **defer** any "retire
  WinForms on Windows" decision until MAUI desktop parity is proven. The working app keeps
  shipping while MAUI grows; the Infragistics desktop UX isn't thrown away before its
  replacement exists.
- **Preserve the WinForms lineage.** Once the WinForms head is declared stable (Spec 1), cut
  a `winforms` branch off `develop` "out of respect to Dare" — Dare Obasanjo, the original
  author. A canonical, maintained resting place for the modernized WinForms app.
- **MAUI is the chosen UI framework.** Avalonia / Uno arguably fit a desktop-heavy app better
  than MAUI's mobile-first desktop story — kept as a fallback note here if MAUI desktop parity
  disappoints — but MAUI is the path per the Captain.
- **Strict TDD, everywhere.** Every line of production change in this migration is developed
  test-first. See the discipline section below — it is a hard rule that gates all four specs.

## Engineering discipline: strict TDD

**Every spec in this handoff is developed test-first — strict red → green → refactor.** No
production change lands without a failing test, written first, that it makes pass. This is a
hard rule, not a preference, and each spec's done-criteria includes "tests written first and
green."

- **Red** — write the smallest failing test expressing the next behavior.
- **Green** — write the minimal code to pass it.
- **Refactor** — clean up with tests green; never refactor on red.

What it means per layer:

- **Core engine (`NewsComponents` → `RssBandit.Core`)** — fully unit-testable; TDD is cleanest
  here. Baseline is `NewsComponents.UnitTests` (NUnit + WireMock.Net, 38/0). New behavior and
  **every interface extracted in Spec 2** gets its tests first.
- **Before refactoring existing untested code** (Spec 1 stabilization, Spec 2 extraction) —
  write **characterization tests** first to pin current behavior, then refactor green. You
  cannot safely decompose a god-object you can't lock down with tests; the characterization
  suite *is* the safety net that makes the separation possible.
- **ViewModels (Specs 3–4)** — the MAUI MVVM layer is plain testable classes; TDD the
  ViewModels against the core, never the XAML. This is *why* the separation matters: it makes
  the UI logic test-first.
- **Platform glue / XAML / native controls** — not unit-TDD-able; keep them as thin
  humble-objects (logic in tested ViewModels, a dumb view) plus a manual/automated smoke pass
  per platform.

CI runs the suite on every push and must stay green; a red bar blocks the merge.

## Recommended order & synergies

```
Spec 1 (stabilize + cut winforms branch)
   → Spec 2 (separation — the GATE; everything downstream depends on it)
      → Spec 3 (MAUI on Windows/Android)
         → Spec 4 (iOS/Mac + platform polish)
```

Three synergies an executor should exploit:

1. **The podcast-deletion spec** (`../podcast-simplification-spec.md`) removes **two**
   cross-platform blockers up front — the lone WPF `DownloadManagerWindow` and the NTFS
   `Zone.Identifier` path in `BackgroundDownloadManager`. Doing it before Spec 2 shrinks Spec 2.
2. **The `FeedSource` / `RssBanditApplication` god-object decomposition**
   (`../modernization-next-steps.md`) *is* part of Spec 2's "decouple from `ApplicationContext`."
   Sequence them as one effort, not two.
3. **The residual-COM `ChildProjects` cleanup** (`../modernization-next-steps.md`, step 1)
   belongs in Spec 1's stabilization checklist — it finishes "zero COM repo-wide."

## The shape today (what the specs build on)

- **`NewsComponents` (the engine) is already WinForms-clean** — zero `System.Windows.Forms`.
  Its only cross-platform blockers are few and contained (see Spec 2).
- **The UI seam is the real work.** `RssBandit.AppServices` leaks WinForms; `RssBanditApplication`
  (~9.8k lines) inherits `ApplicationContext`; `WinGuiMain` (~14.9k lines) is bound to
  **Infragistics (≈30 files), SandDock (4), WebView2 (7)** — the hard MAUI blockers, none with
  MAUI equivalents. The MAUI UI is a fresh build, not a port.
- **Rendering ports at the right layer:** `INewsItem` → `NewsItemFormatter` (XSLT→HTML) → host
  WebView. The XSLT step is portable; only the WebView host is platform-specific.

Read the relevant spec for the file-level detail.
