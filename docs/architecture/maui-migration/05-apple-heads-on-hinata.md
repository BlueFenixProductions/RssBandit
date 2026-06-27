# Spec 3 (Apple heads) — building `RssBandit.Maui` for macOS + iOS on a Mac (Hinata)

*Part of the [MAUI migration handoff](00-README.md). Companion to [Spec 3 — MAUI UI](03-maui-ui.md).
Status: ready to execute on a Mac.*

## Why this is a separate doc

`RssBandit.Maui` multi-targets four heads. **Windows (WinUI) and Android build on the Windows dev
box** (where the engine, the toolchain, and Pixel deployment already work — PostXING proves it).
**iOS and Mac Catalyst can only build on a Mac** (they need Xcode and the Apple SDKs). This doc is the
recipe for the Mac — referred to here as **Hinata** — to build those two heads. Everything the heads
depend on (the engine, contracts, view-models, the renderer) is portable `net10.0` and builds anywhere.

## The cross-machine TFM split (already wired in the csproj)

`RssBandit.Maui.csproj` selects target frameworks by build-host OS, so **no machine tries to build a
head it can't**:

```xml
<TargetFrameworks>net10.0-android</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('osx'))">$(TargetFrameworks);net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
```

- Android builds on **both** OSes.
- WinUI builds **only on Windows**.
- iOS + Mac Catalyst build **only on macOS** (Hinata).

So on Hinata, `dotnet build` over the project naturally produces `net10.0-android`, `net10.0-ios`, and
`net10.0-maccatalyst` — and never attempts WinUI.

## Prerequisites on Hinata

1. **.NET 10 SDK** (≥ `10.0.300`, matching the repo's `global.json`). Verify: `dotnet --version`.
2. **The MAUI workload:** `dotnet workload install maui` (run from the repo root so it picks up the
   repo-local `nuget.config` — see below). This pulls `maui-ios` + `maui-maccatalyst` + the shared MAUI
   packs.
3. **Xcode** (current stable), then `sudo xcode-select -s /Applications/Xcode.app` and accept the
   license (`sudo xcodebuild -license accept`). The iOS/Mac SDKs come from Xcode.
4. **For device iOS** (not the simulator): an Apple Developer account + a provisioning profile/signing
   identity. **The iOS Simulator and Mac Catalyst need no signing** — start there.
5. **The repo's `nuget.config`** (already committed at the repo root) clears the restrictive global
   package-source mapping and allows nuget.org with a wildcard — required so the MAUI packages + workload
   restore. It's checked in, so Hinata inherits it; no per-machine NuGet setup needed.

## Why the portable core just works on macOS/iOS

The heads reference `NewsComponents` + `RssBandit.AppServices` + `RssBandit.ViewModels` — all
`net10.0`, no `-windows`. Two engine-side things were specifically made non-Windows-safe by the gate
work, so the core **runs** (not just compiles) on Apple platforms:

- **File moves:** `FileHelper.MoveFile` is now behind `IFileMover` (Phase F). Off-Windows it selects
  `PortableFileMover` (`File.Move`), so the cache write + Lucene index commit don't hit the
  `kernel32 MoveFileEx` P/Invoke that would throw `DllNotFoundException`.
- **Registry:** the `MimeType` + `INewsComponentsConfiguration` registry calls are
  `OperatingSystem.IsWindows()`-guarded — they no-op off-Windows instead of throwing.

(The Lucene search index, the `RssParser`, `HttpClient` networking, and the `NewsItemFormatter` XSLT
render are all already cross-platform.)

## Build + run commands (on Hinata)

```bash
# from the repo root
dotnet restore source/RssBandit.Maui/RssBandit.Maui.csproj

# Mac Catalyst (no signing; fastest Apple inner loop)
dotnet build source/RssBandit.Maui/RssBandit.Maui.csproj -f net10.0-maccatalyst -c Debug
dotnet build source/RssBandit.Maui/RssBandit.Maui.csproj -f net10.0-maccatalyst -t:Run   # launches it

# iOS Simulator (no signing)
dotnet build source/RssBandit.Maui/RssBandit.Maui.csproj -f net10.0-ios -c Debug \
  -p:RuntimeIdentifier=iossimulator-arm64 -t:Run      # or -x64 on Intel Macs

# iOS device (needs signing: set the team + bundle id)
dotnet build source/RssBandit.Maui/RssBandit.Maui.csproj -f net10.0-ios -c Release \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:CodesignKey="Apple Development: <you>" -p:CodesignProvision="<profile name>"
```

## Platform services to wire (same gaps as the Android head)

The MAUI heads register portable implementations of the Spec-2 platform interfaces in `MauiProgram`
(`MauiAppBuilder` + `Microsoft.Extensions.DependencyInjection`):

- **`IPersistedSettings`** → a MAUI `Preferences`-backed impl (the existing `SettingStore` is the
  Windows registry impl; it no-ops off-Windows, so a portable settings store is needed for real
  persistence). Shared by Android + Apple.
- **App data paths** → `FileSystem.AppDataDirectory` (instead of `%APPDATA%`).
- **Enclosure / external-link open** → `Launcher.OpenAsync` / `Browser.OpenAsync` (the podcast spec's
  "open in default handler" maps directly).

These are not Apple-specific — they're the cross-platform wiring the Android head needs too, so they
land once and serve all non-Windows heads. The Apple heads add no platform code beyond what the
shared `MauiProgram` already registers, plus the standard `Platforms/iOS/` + `Platforms/MacCatalyst/`
entry points the `dotnet new maui` template generates.

## Done-criteria (Apple heads)

- `dotnet build -f net10.0-maccatalyst` and `-f net10.0-ios` succeed on Hinata against the shared core.
- The Mac Catalyst app launches and renders the first screen (the feed/item view-models bound, per
  Spec 3) — the same view-models the Windows + Android heads run, proving the portable core/VM layer
  is genuinely cross-platform.
- iOS simulator launch verified; device build documented (signing) for when a provisioning profile is
  available.

## Handoff note

The Windows dev box builds + deploys the WinUI and Android heads (and is where the first reader screen
gets built per Spec 3). Hinata's job is purely to prove + run the two Apple heads on the *same commit*
of the shared core — no Apple-specific application code, just the platform build. When both machines
build their heads green off one `develop`, the dual-head (really quad-head) gate is demonstrated end
to end.
