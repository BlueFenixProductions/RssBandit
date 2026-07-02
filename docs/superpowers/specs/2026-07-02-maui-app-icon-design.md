# MAUI Android Branding: RssBandit App Icon & Splash

**Date:** 2026-07-02
**Status:** Approved (Chris, in-session)

## Goal

Replace the default .NET MAUI template branding (purple bot icon, purple splash,
`#512BD4`) with the classic RssBandit eye-patch smiley on a Tokyo Night dark
background (`#1a1b26`). Primary target is the Android build; the Windows build
picks up the same `MauiIcon` automatically.

## Source art

The smiley-only mark exists in the repo at:

- `source/RssBandit/Resources/rssbandit.32.png` — 32×32, crisp, the canonical mark
- `source/RssBandit.Package/Assets/Square44x44Logo.targetsize-256.png` — 256×256,
  same design, largest copy, visibly soft (itself an upscale)

No vector or higher-res original exists anywhere in the repo.

## Approach: both tracks, judge on device

1. **Raster track (ships now):** extract the smiley from the 256px packaging
   asset, center it in the adaptive-icon safe zone on a transparent canvas,
   wire it in as the `MauiIcon` foreground PNG.
2. **Vector track (comparison candidate):** a faithful SVG redraw of the glossy
   smiley (radial-gradient ball, eye patch + strap, violet eye, smile, gloss
   highlight) saved alongside but not wired in. Swapping it in is a one-line
   csproj change. The splash uses the SVG directly, since splash renders large
   where the raster is softest.

## Changes

All under `source/RssBandit.Maui/`:

| File | Change |
|---|---|
| `Resources/AppIcon/appicon.svg` | Replace with solid `#1a1b26` square (adaptive background layer) |
| `Resources/AppIcon/appiconfg.svg` | Delete (template bot foreground) |
| `Resources/AppIcon/appiconfg.png` | New — smiley raster, safe-zone padded |
| `Resources/AppIcon/appiconfg-vector.svg` | New — SVG redraw, not wired in |
| `Resources/Splash/splash.svg` | Replace with smiley SVG artwork |
| `RssBandit.Maui.csproj` | `MauiIcon` → `appiconfg.png` foreground, `Color="#1a1b26"`; `MauiSplashScreen` → `Color="#1a1b26"` |

## Adaptive-icon constraints

- Android masks adaptive icons to circle/squircle keeping the central ~66%
  safe zone; the round smiley must fit inside it. Foreground canvas is sized
  so the smiley diameter ≈ 64% of canvas.
- Foreground layers generate up to 432×432 (xxxhdpi); some upscale from the
  256px source is unavoidable — acceptable for the raster track, solved by
  the vector track.

## Verification

- Clean build (`dotnet build -f net10.0-android`) — Resizetizer caches
  generated assets, so clean first.
- Inspect generated `mipmap-*` drawables under `obj/`.
- Deploy to device/emulator after uninstalling the old build (launchers cache
  icons). Chris compares raster vs. vector foreground and locks one in.

## Out of scope

- Android 13+ monochrome themed-icon layer (follow-up once vector is chosen)
- Notification icons (app posts none yet)
- Removing `dotnet_bot.png` (still referenced by template content; separate cleanup)
