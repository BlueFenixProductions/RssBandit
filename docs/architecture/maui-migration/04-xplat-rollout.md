# Spec 4 — Cross-platform rollout

*Part of the [MAUI migration handoff](00-README.md). Status: not started. Depends on Spec 3
(the MAUI UI running on Windows/Android).*

## Purpose

Take the MAUI reader from Spec 3 to **Android → macOS Catalyst → iOS** as installable apps,
handling the platform-specific concerns each one introduces. The shared core does the work;
this spec is the platform glue and the per-OS plumbing.

## Order

1. **Android first** — emulator + device, cheapest loop, no Apple account needed.
2. **macOS Catalyst + iOS** — need a Mac build host and Apple provisioning; do them together
   since they share the WKWebView / Apple toolchain.

## Platform concerns

- **Persistence / paths** — `FileSystem.AppDataDirectory` for the cache + Lucene index; settings
  via `Preferences` (behind the Spec-2 `IPersistedSettings`). Consider SQLite for the item store
  if the file cache strains mobile storage.
- **Background refresh** — wrap the engine's refresh loop per platform: Android `WorkManager`,
  iOS `BGTaskScheduler`. The core refresh is portable; only the scheduler trigger is per-OS.
- **Networking** — `HttpClient` is universal; proxy/cert config is per-platform. (The WinInet
  cookie bridge being gone is a free win here — nothing Windows-only to port.)
- **WebView** — MAUI `WebView` is Android `WebView` / iOS+Mac `WKWebView`; it renders the
  `NewsItemFormatter` HTML. Intercept navigation so feed links and enclosure-opens go through
  `Launcher.OpenAsync` / `Browser.OpenAsync` rather than navigating inside the reader.
- **Search** — Lucene's file index works on mobile; watch index size and rebuild cost on
  constrained storage.
- **Lifecycle & integration** — app lifecycle (suspend/resume), deep links (a `feed:` / share
  handler to subscribe), share-to-subscribe from other apps.
- **Build / CI / distribution** — add jobs for Android (APK/AAB), iOS (signing + provisioning →
  TestFlight), macOS Catalyst (notarization). The Windows head still ships MSIX from the
  existing `RssBandit.Package.wapproj`.

## TDD for this spec

Most of this spec is platform glue that isn't unit-TDD-able, so the discipline is about keeping
the *testable* part testable and the *untestable* part thin:

- **Platform services stay behind the Spec-2 interfaces, and those get tests first.** The
  Android/iOS implementations of `IPersistedSettings`, `IFileMetadataService`, the
  refresh-scheduler trigger, and the path provider are written against interface tests (with
  fakes on the build host); the real per-platform impls are humble wrappers.
- **No new logic in platform code.** If a behavior is worth testing, it lives in the core or a
  ViewModel (already TDD'd in Specs 2–3), not in an `#if ANDROID` block.
- **Device smoke as the integration gate.** Per-platform, a scripted smoke run (launch →
  subscribe to a known feed → refresh → open an item → search) is the acceptance test that the
  glue is wired right — the unit suite can't reach the device, so this is the per-OS green bar.
- CI keeps the full unit + ViewModel suite green on every push; device smoke runs per release
  candidate.

## Done-criteria

- Interface-test-first for every platform service; unit + ViewModel suites green.
- **Installable builds on Android, iOS (TestFlight), and macOS Catalyst**, each running the core
  reader loop (subscribe / refresh / read / search) on the shared engine, with the device smoke
  passing on each.
