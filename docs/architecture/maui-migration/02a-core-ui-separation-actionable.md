# Spec 2a — Core / UI separation, made actionable

*Part of the [MAUI migration handoff](00-README.md). **Revises [Spec 2](02-core-ui-separation.md)**
with two things that changed since the 2026-06-14 handoff was written: the FeedSource god-object
decomposition is now **complete**, and a full re-survey shows the engine is **~90% already-portable,
gated by project plumbing rather than code.** Grounded in a fresh read of the project files and the
actual coupling. Status: ready to start.*

## What changed since Spec 2 was written (read this first)

1. **The FeedSource decomposition is done and deliberately stopped** ([decomposition spec](../feedsource-decomposition-spec.md),
   [refresh-loop decision](../feedsource-refresh-loop-decision.md)). Six concerns were extracted
   behind interfaces — `IFeedAndCategorySettings`, `IFaviconStore`, `IFeedListSerializer`,
   `ISearchIndexSink`, `ItemMerger`, `FeedAndCategorySettings` — and the async refresh-orchestration
   core was assessed and **explicitly left on `FeedSource`** (it isn't a concern hiding inside the
   object; it *is* the object). **So Spec 2's Step 4 (the god-object decomposition) is done on the
   engine side, and its remaining half — `RssBanditApplication`'s `ApplicationContext` inheritance —
   is WinForms-head-internal with no portability payoff. Step 4 is re-scoped OUT of the gate** (see
   Non-goals).
2. **The gate is cheaper at compile time than Spec 2 implied — but the cross-platform runtime work is
   not eliminated, only correctly deferred.** The blockers are TFMs, a global `Directory.Build.props`
   setting Spec 2 missed, and the single mixed `AppServices` assembly. The native bits
   (`kernel32` move, registry MIME/settings) **compile fine under `net10.0`** and only fault when run
   off-Windows — so they belong in Specs 3–4 (where a non-Windows head actually exercises them), not
   the compile-time gate.

## 1. Verified layering reality

The `.sln` has five projects (TFMs as of this writing):

| Project | TFM | Role |
|---|---|---|
| `NewsComponents` | `net10.0-windows10.0.19041` | the engine (UI-free) |
| `RssBandit.AppServices` | `net10.0-windows10.0.19041` | contracts |
| `RssBandit` | `net10.0-windows…`, `WinExe` | WinForms head (Infragistics/SandDock/WebView2/Unity) |
| `NewsComponents.UnitTests` | `net10.0-windows…` | engine tests (NUnit + WireMock) |
| `RssBandit.Package.wapproj` | MSIX | packaging (not CLI-buildable) |

CI builds `source/RssBandit/RssBandit.csproj` then builds+tests `NewsComponents.UnitTests`. **Build
the csproj, never the `.sln`** (the wapproj dies under the CLI).

**The engine source is WinForms-clean** — zero `System.Windows.Forms`/`Infragistics` in
`NewsComponents/*.cs`; the only `System.Drawing` is one *unused* import
(`Utils/SerializationInfoReader.cs`). The coupling is at the **project level**:
- `NewsComponents` (`-windows`) references `AppServices` (`-windows`), and `AppServices` imports
  WinForms/Drawing in five files (below) — but `NewsComponents` **consumes none of those tainted
  types**. Dead weight, not a real dependency.
- **`source/Directory.Build.props` sets `<UseWPF>`/`<UseWindowsForms>`/`<RuntimeIdentifiers>win-x86`
  for *every* project in `source/`** — a hidden global blocker Spec 2 missed. There's already an
  unused `IsLegacyProject` flag to gate them on.

## 2. The portable core (the six seams are all type-clean)

All six extracted seams have signatures free of `System.Drawing`/WinForms — verified portable.
Notably `IFaviconStore` returns **`byte[]`**, not `System.Drawing.Image`/`Icon`. The blockers to a
`net10.0`-portable core, each with fix shape and risk:

| # | Blocker | Fix | Risk |
|---|---|---|---|
| B1 | `AppServices` is one mixed `-windows` assembly; 5 files import WinForms/Drawing | Split: move the 5 files to a new `RssBandit.WinForms.Contracts` (`-windows`), **same root namespace**; retarget the rest to `net10.0` | Low (interfaces only; all tainted-type consumers are in the WinForms head) |
| B2 | `Directory.Build.props` forces `UseWPF`/`UseWindowsForms`/`win-x86` on all | Gate those three on `IsWindowsHead`; engine/contracts/viewmodels opt out | Low |
| B3 | `NewsComponents` TFM `-windows`; two *unused* `WindowsAPICodePack-*` pkgs | Flip TFM → `net10.0`; move the two pkgs to `RssBandit` (WinGui uses them); add `Microsoft.Win32.Registry` so `MimeType`/`SettingStore` still compile | Low–Med (restore + clean WinForms rebuild) |
| B4 | `Utils/NTFS.cs` `kernel32` (now **dead** — its only caller, `BackgroundDownloadManager`, was deleted by the podcast cut) | **Delete** as dead code | Low |
| B5 | `FileHelper.MoveFileEx` (live), `MimeType` registry, `SettingStore` registry | **Compile-time: none** (build under `net10.0`). **Runtime (DEFER to Spec 3/4):** behind `IFileMover`/`IMimeResolver`/`IPersistedSettings` — Windows impl (current) + portable impl | Deferred |
| B6 | unused `using System.Drawing;` in `SerializationInfoReader.cs` | Delete the import | Trivial |

The five WinForms-tainted `AppServices` files (B1), all consumed **only** by the WinForms head:
`UI/CommandBarInterfaces.cs`, `UI/WindowManagementInterfaces.cs`, `AddIn/AddInInterfaces.cs`
(`IWin32Window`), `Core/ICoreApplication.cs` (`IWin32Window` dialog methods), `Core/IUserPreferences.cs`
(carries `Font`/`Color` across ~12 members — the MAUI head themes separately).

After B1–B4 + B6, `NewsComponents` + the portable `AppServices` compile as `net10.0` with zero
`-windows`, the WinForms head still builds/runs on Windows (native calls unchanged), and a portable
ViewModels project becomes referenceable. B5 is the runtime tail, paid down when a non-Windows head
runs it.

## 3. Target topology (minimize renames — pure churn is a trap)

```
NewsComponents              net10.0           ENGINE (conceptually "RssBandit.Core"; KEEP the name)
RssBandit.AppServices       net10.0           PORTABLE CONTRACTS (retargeted; 5 files removed)
RssBandit.WinForms.Contracts net10.0-windows  NEW: the 5 WinForms-tainted interfaces (same namespace)
RssBandit.ViewModels        net10.0           NEW: presentation logic (CommunityToolkit.Mvvm); the TDD surface
RssBandit                   net10.0-windows   WinForms HEAD (unchanged behavior)
RssBandit.Maui              net10.0-android;-ios;-maccatalyst;-windows   (Spec 3)
NewsComponents.UnitTests    net10.0           (retarget)
RssBandit.ViewModels.Tests  net10.0           NEW: the red-green presentation surface
```

- **Keep `NewsComponents` and `RssBandit.AppServices` as the names** — referenced everywhere;
  renaming to `RssBandit.Core`/`.Contracts` is churn with no functional gain.
- **Introduce `RssBandit.ViewModels` NOW, not in Spec 3.** The whole justification for the gate is a
  test-first presentation layer; standing it up now lets the **WinForms head consume shared
  ViewModels via adapters**, which is the only way to prove the seam fits *both* heads. The existing
  `source/RssBandit/ViewModel/BindableBase.cs` (pure `INotifyPropertyChanged`, zero UI deps) is a
  ready seed; adopt `CommunityToolkit.Mvvm` so it carries into MAUI unchanged.

## 4. Slices (each TDD'd, WinForms head behavior-preserving)

- **Slice 0 — contracts go portable.** Carve the 5 WinForms files out of `AppServices` into
  `RssBandit.WinForms.Contracts`; gate `Directory.Build.props`; flip **only `AppServices`** →
  `net10.0`. Proof is a build + a **CI assertion that `AppServices` resolves no `-windows`
  reference**. Lowest-risk move in the whole plan.
- **Slice 0b — engine goes portable.** Flip `NewsComponents` → `net10.0`; delete dead `NTFS.cs`;
  move `WindowsAPICodePack-*` to `RssBandit`; add `Microsoft.Win32.Registry`; drop the unused
  `System.Drawing` import; retarget `NewsComponents.UnitTests` → `net10.0`. **Gate:** the engine
  suite stays green on the portable TFM (CI no-`-windows` assertion) **and** the WinForms head still
  builds + launches healthy (feeds refresh, empty `error.log`, no new `FATAL`).
- **Slice 1 — the first presentation seam (the "prove it" slice).** Pick the smallest leaf concern
  the WinForms head shows imperatively and can re-bind cheaply: **item read-state / per-feed unread
  counts.** Create `FeedItemViewModel`/`FeedNodeViewModel` in `RssBandit.ViewModels` (`Title`,
  `Date`, `IsRead`, `UnreadCount`, `ToggleReadCommand`). **Red-green-refactor against a faked core**
  in `RssBandit.ViewModels.Tests` ("marking read decrements unread"; "selecting a feed exposes its
  items"; "read-state round-trips"), then bind the existing list/tree's read/unread *display* to the
  VM via a thin adapter (humble-object: the Infragistics control stays; only the value source moves).
  No MAUI code yet — the seam is validated against the *current* head. Chosen because read-state is
  pure in-memory state over already-portable types, with no async/serialization/singleton
  entanglement (same property that made `IFeedAndCategorySettings` the safe first decomposition cut).
- **Parallel — extract `NewsItemFormatter`** (`WinGui/Utility/RssItemFormatter.cs`, XSLT→HTML, body
  portable; its `using System.Windows.Forms;` is unused). Move it + its XSLT templates (to embedded
  core resources) and relocate the `*ExceptionEventArgs` types to a shared namespace. The **ideal
  characterization slice**: snapshot the exact HTML for a fixture `INewsItem` *before*, assert
  byte-equality *after*. Directly unblocks the MAUI detail view; the WinForms head keeps calling it.

## 5. Phases

```
A  Contracts portable     Slice 0   (AppServices split; Directory.Build.props gate; CI no-windows assert)
B  Engine portable        Slice 0b  (NewsComponents → net10.0; delete dead NTFS; pkg cleanup; retarget engine tests)
C  Formatter to core      NewsItemFormatter extraction (HTML byte-snapshot characterization)  [‖ with B]
D  ViewModels stood up     Slice 1   (RssBandit.ViewModels + Tests; first leaf VM consumed by WinForms via adapter)
E  Grow the VM surface     feed-tree / item-list / search / settings VMs — each red-green, adopted by WinForms where cheap
F  Native runtime shims    B5: IFileMover / IMimeResolver / IPersistedSettings portable impls — only when a non-Windows run needs them
   → Spec 3: RssBandit.Maui head — lists feeds + reads items on the shared core/VMs
```

## 6. Risks & non-goals

**Non-goals (do NOT do):**
- **Don't port Infragistics / SandDock / WebView2.** Fresh MAUI UI; reuse the engine + the formatter,
  not the widgets. The biggest Phase D/E trap is trying to make the Infragistics tree consume
  ViewModels — that's a WinForms rewrite. Keep WinForms adoption to leaf concerns it can bind cheaply.
- **Don't rewrite or re-decompose the engine.** `FeedSource` reached its principled endpoint; the
  refresh orchestration is the irreducible core (NO-GO on record).
- **Don't chase the `RssBanditApplication`/`ApplicationContext` decoupling as gate work** — it's
  UI-internal, off the engine's dependency path (the dependency is one-way: UI → engine), and yields
  no portability. The coordination logic the MAUI head needs (refresh, feed-source management) already
  lives in the portable `FeedSource`/`FeedSourceManager`. An `IAppCore` extraction is optional and
  demand-driven by Spec 3, not a gate item.
- **Don't rename `NewsComponents`/`AppServices`**, and **don't migrate Unity → MS.DI as gate work**
  (the MAUI head brings MS.DI natively; converge then). Don't fully solve cross-platform native before
  any non-Windows head exists.

**Real residual risks:** the `Directory.Build.props` flip can surface latent framework-reference
assumptions in the WinForms rebuild (mitigate: gate, then full clean rebuild + launch); the
`WindowsAPICodePack` move from engine to head must be exact (7 WinGui files); `NewsComponents.UnitTests`
retargeting must keep the WireMock + NUnit harness green (the current 129-test baseline is the safety
net).

## Done-criteria (revised)

- `NewsComponents` + `RssBandit.AppServices` compile as `net10.0` (no `-windows`), proven by a CI
  assertion; `RssBandit.WinForms.Contracts` holds the WinForms-only interfaces.
- `RssBandit.ViewModels` exists with a first leaf VM, TDD'd, **consumed by the WinForms head via an
  adapter** (the seam is proven against the head that ships today).
- `NewsItemFormatter` lives in the core with byte-snapshot characterization tests.
- **The WinForms head still builds and launches healthy** on the portable core (runtime gate), and the
  Spec 1 usage shakedown still passes. The engine test baseline stays green throughout.
