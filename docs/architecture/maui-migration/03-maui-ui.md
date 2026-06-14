# Spec 3 — MAUI UI on the shared core

*Part of the [MAUI migration handoff](00-README.md). Status: not started. Depends on Spec 2
(the portable core).*

## Purpose

Build a new .NET MAUI front-end that consumes the shared core from Spec 2 — a **functional
reader first**, not a re-creation of the WinForms UI. There is no MAUI equivalent for the
Infragistics tree/toolbar or SandDock docking, so the MAUI UI is a fresh build using native
MAUI controls; the value reused from the old app is the *engine and the rendering*, not the
widgets.

## Steps (each test-first — see TDD section)

### 1. The project
- New **`RssBandit.Maui`** multi-targeting `net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0-windows`
  (the last is WinUI — the fast dev-loop target), referencing `RssBandit.Core` + `RssBandit.Contracts`.

### 2. MVVM layer
- Grow the existing `ViewModel/BindableBase.cs` (`INotifyPropertyChanged`) or adopt
  **`CommunityToolkit.Mvvm`** (source-generated `ObservableProperty` / `RelayCommand`). Replace
  the app's custom `ICommand` with `RelayCommand`.
- ViewModels wrap the portable domain (`INewsFeed`, `INewsItem`, search, settings). **These are
  plain testable classes — they are the TDD surface for the UI.**

### 3. Core screens (native MAUI controls)
- **Subscription tree** — Shell flyout + grouped `CollectionView` (categories → feeds). (No
  `UltraWinTree`; a grouped/hierarchical `CollectionView` or a community TreeView.)
- **Item list** — `CollectionView` bound to the selected feed's items.
- **Item detail** — MAUI **`WebView`** rendering the HTML from the extracted `NewsItemFormatter`
  (Spec 2, step 3). External links + enclosure-open route through `Launcher.OpenAsync` /
  `Browser.OpenAsync` (the podcast spec's "open in default handler" maps directly).
- **Add-subscription**, **search** (Lucene is already portable), **settings**.

### 4. Composition root & platform services
- Portable DI via **`MauiAppBuilder`** (Microsoft.Extensions.DependencyInjection); register the
  core services from Spec 2. (If Spec 2 migrated the core off Unity to MS.DI, this is a clean
  reuse.)
- Platform services behind the Spec-2 interfaces: `IPersistedSettings` → MAUI `Preferences`;
  paths → `FileSystem.AppDataDirectory`; `IFileMetadataService` → no-op / attributes.

### 5. Dev-loop order
- **WinUI (Windows) target first** — fastest inner loop, no emulator. Get the reader working
  there, then validate on Android (Spec 4 takes it the rest of the way).

## TDD for this spec

The whole point of the Spec-2 separation is that **the MAUI UI logic is now test-first**:

- **TDD the ViewModels, not the XAML.** New project `RssBandit.Maui.ViewModels.Tests` (portable,
  runs on the build host). For each screen, write the ViewModel's tests first against the core
  (or a faked core service): "selecting a feed loads its items," "marking read updates the
  unread count," "search returns the expected item IDs," "the detail VM exposes the formatter's
  HTML for the selected item." Then write the ViewModel to pass.
- **Keep views humble.** XAML/pages hold no logic — just bindings — so there's nothing un-tested
  worth testing in them. Anything tempted to live in code-behind goes into a tested ViewModel.
- **The formatter is already locked** by Spec 2's characterization snapshot — the MAUI detail
  view just hosts that HTML; assert the VM hands the WebView the same HTML the formatter
  produces.
- **CI:** add the ViewModel test project to the suite; it must stay green. Platform/UI smoke is
  manual until Spec 4 wires automated device smoke.

## Done-criteria

- ViewModel tests written first and green.
- The MAUI app **subscribes, refreshes, lists, reads (HTML render), and searches** on at least
  the **Windows + Android** targets, all on the shared core, with no reference to
  Infragistics/SandDock/WebView2.
