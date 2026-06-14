# Spec 2 — Core / UI separation (the linchpin)

*Part of the [MAUI migration handoff](00-README.md). Status: not started. Everything
downstream (Specs 3–4) depends on this.*

## Purpose

Extract a UI-agnostic, cross-platform-ready core that both the WinForms head and the future
MAUI head sit on. The good news from the codebase survey: the engine is *already* almost
clean. This spec is mostly about (a) dropping the few Windows-only bits behind interfaces,
(b) splitting the contract layer's portable parts from its WinForms parts, and (c) lifting the
HTML rendering and the app-coordination logic out of the WinForms shell.

## Starting facts (from the survey)

- **`NewsComponents` has zero `System.Windows.Forms` references** (one *unused* `System.Drawing`
  import in `Utils/SerializationInfoReader.cs`). Cross-platform blockers are few and contained.
- **`RssBandit.AppServices` leaks WinForms**: the portable domain contracts (`INewsFeed`,
  `INewsItem`, `IFeedDetails`, `IUserPreferences`) are clean, but `UI/CommandBarInterfaces.cs` +
  `UI/WindowManagementInterfaces.cs` depend on `System.Windows.Forms`/Infragistics,
  `AddIn/AddInInterfaces.cs` uses `IWin32Window`, and `ICoreApplication` carries dialog methods
  (`ShowOptions`, `ShowPodcastOptionsDialog`).
- **`RssBanditApplication`** (~9.8k lines / 5 partials) inherits `System.Windows.Forms.ApplicationContext`
  and is the mega-facade; **`NewsItemFormatter`** (`WinGui/Utility/RssItemFormatter.cs`) is the
  portable XSLT→HTML renderer living in the UI layer.

## Steps (each test-first — see TDD section)

### 1. De-Windows the engine
- Change `NewsComponents` TFM `net10.0-windows10.0.19041` → `net10.0`. Multi-target only if a
  Windows-specific implementation genuinely needs the `-windows` surface.
- Remove the **unused** `WindowsAPICodePack-Core` / `-Shell` packages and the unused
  `System.Drawing` import in `SerializationInfoReader.cs`.
- Put the few native spots behind interfaces, with a Windows default implementation:
  - **`IFileMetadataService`** for the NTFS `Zone.Identifier` write (`Utils/NTFS.cs`, called from
    `Core/BackgroundDownloadManager.cs` ~L710). **Likely moot** — the podcast-deletion spec
    removes the only caller. Abstract only if the download engine survives.
  - **`IPersistedSettings`** already exists. Keep the Registry-backed `SettingStore`
    (`Core/INewsComponentsConfiguration.cs`) as the *Windows* implementation; add a portable
    file/JSON-backed default for non-Windows.
  - **MIME→extension lookup**: `MimeType.GetFileExtension` reads the registry via the nested
    `WindowsRegistry` class — already isolated. Replace with a static MIME map (or a small
    `IMimeResolver`); the registry path becomes the Windows impl or is dropped.
  - **`FileHelper.MoveFileEx`** (`kernel32`, ~L701) → `System.IO.File.Move`, or behind an
    `IFileSystem` abstraction if delayed-move semantics are actually needed.
- **Outcome:** `NewsComponents` compiles as `net10.0` with a thin Windows platform shim for the
  handful of native bits.

### 2. Split `AppServices`
- New **`RssBandit.Contracts`** (`net10.0`, portable): the domain contracts — `INewsFeed`,
  `INewsItem`, `IFeedDetails`, `IUserPreferences`, and a **slimmed `ICoreApplication`** with the
  dialog methods removed.
- New **`RssBandit.WinForms.Contracts`** (`net10.0-windows`): the WinForms-only surface —
  `UI/CommandBarInterfaces.cs`, `UI/WindowManagementInterfaces.cs`, the `IWin32Window` add-in
  interfaces.
- Move dialog launching (`ShowOptions`, `ShowPodcastOptionsDialog`, `ShowUserIdentityManagementDialog`)
  off `ICoreApplication` into an **`IDialogService`** that the WinForms head implements (and the
  MAUI head implements differently).

### 3. Extract `NewsItemFormatter`
- Move `RssItemFormatter.cs` from `RssBandit.WinGui.Utility` into the shared core (it's
  `System.Xml.Xsl` XSLT→HTML, fully portable). Both heads call it to produce the item HTML;
  each renders that HTML in its own WebView (WebView2 on Windows, MAUI `WebView` elsewhere).
- The XSLT templates currently live in `RssBandit/Properties/Resources` — move them with the
  formatter (embedded resources in the core).

### 4. Decouple `RssBanditApplication` from `ApplicationContext`
- Lift the domain/coordination logic (feed-source management, refresh orchestration, search
  wiring, event aggregation) into a portable **`IAppCore`** + focused services. Keep the
  WinForms `ApplicationContext`, dialog wiring, and `guiMain` ownership in the WinForms head.
- **This is the same work as the `RssBanditApplication` / `FeedSource` god-object decomposition
  in [`../modernization-next-steps.md`](../modernization-next-steps.md)** — sequence them as one
  effort. Extract one concern at a time, behind a tested interface.
- Consider migrating the composition root from **Unity** (`Core/ServiceInfrastructure/UnityDependencyResolver.cs`)
  to **Microsoft.Extensions.DependencyInjection** so both heads share one DI story (MAUI uses
  MS.DI natively). Not required for Spec 2, but cheaper to do here than later.

### 5. Target project layout
```
RssBandit.Core         (= portable NewsComponents @ net10.0; engine + NewsItemFormatter)
RssBandit.Contracts    (portable domain interfaces)
RssBandit.WinForms     (the existing UI; references Core + Contracts + WinForms.Contracts)
RssBandit.Maui         (added in Spec 3)
*.Tests                (per assembly — see TDD)
```

## TDD for this spec

This is the spec where strict TDD is most load-bearing — **you are decomposing god-objects, and
you cannot do that safely without tests pinning the behavior first.**

- **Characterization-first.** Before extracting any interface or moving any method, write
  characterization tests that capture the *current* observable behavior (feed parsing results,
  refresh/dedup outcomes, the exact HTML `NewsItemFormatter` produces for a known item, the
  settings round-trip). Run them green against the old code, then refactor and keep them green.
  The `NewsItemFormatter` extraction (step 3) is ideal: snapshot its HTML output for a fixture
  item *before* the move, assert byte-equality *after*.
- **Each extracted interface gets its tests first** (step 1's `IFileMetadataService`,
  `IPersistedSettings` portable impl, `IMimeResolver`; step 2's slimmed `ICoreApplication` /
  `IDialogService`; step 4's `IAppCore` services). Write the interface's test against a fake,
  then the real implementation against it.
- **The portable-TFM flip is itself test-driven:** the gate for "did `NewsComponents` become
  portable" is that `NewsComponents.UnitTests` (retargeted to `net10.0`) still runs green with
  zero `-windows` references — make that a CI assertion.
- New test projects: `RssBandit.Core.Tests`, `RssBandit.Contracts.Tests` (portable, run on the
  build host). Keep `NewsComponents.UnitTests`' 38 green throughout; grow the count as
  interfaces land.

## Risks & done-criteria

**Risks.** The god-object decomposition (step 4) is the large, risky part; do it incrementally,
one tested concern at a time, runtime-gating the WinForms head after each. The portable
composition-root change ripples through registrations.

**Done-criteria.**
- `RssBandit.Core` + `RssBandit.Contracts` compile as `net10.0` (no `-windows`), proven by a CI
  check.
- Characterization + interface tests written first and green; `NewsComponents.UnitTests` still
  38+/0.
- **The WinForms head still builds and launches healthy** on the refactored core (runtime gate),
  and the usage shakedown from Spec 1 still passes.
