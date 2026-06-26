# Shakedown follow-ups (2026-06-26)

Findings and tracked follow-ups from the real-usage shakedown of `develop` @ `18a7e3d4`
(all nine modernization PRs of 2026-06-26 merged: podcast B+C, dead ChildProjects,
Enterprise Library, BinaryFormatter prefs shim, abandoned installer, orphaned resx,
WFO1000, nullable beachhead).

## What the shakedown validated

- **Stability.** A continuous ~5-minute run stayed alive past the ~4–5 min delayed-crash
  window, with **0 FATAL, 0 crash events (Application 1000/1026), feeds refreshing
  normally, and `error.log` empty** (until the items below were deliberately provoked).
- **Menus / Preferences.** UIAutomation confirmed the Infragistics menu bar enumerates
  (`File/Edit/View/Tools/Help`), `Tools` opens via `ExpandCollapsePattern` (`Update All
  Feeds`, `Options…`, `Manage Identities…`, …), and invoking `Options…` opened the
  Preferences dialog with **zero trace errors** — the podcast-removal dialog surgery did
  not break the open path.
- **The modernized exception path works end-to-end.** The new `ExceptionPublisher`
  (PR #5, hardened in PR #10) caught and published a real exception to
  `%APPDATA%\RssBandit\error.log` with the full "General Information" block **including the
  `Date/Time` + `Machine Name` fields added in PR #10**.

## Regression found and fixed during the shakedown

**Toolbar/menu state could no longer be saved** — `PlatformNotSupportedException:
BinaryFormatter ... removed` thrown ~3 minutes into every session from
`WinGuiMain.OnSaveConfig` → `StateSerializationHelper.SaveToolbarManager` →
Infragistics `UltraToolbarsManager.SaveAsBinary` (which uses `BinaryFormatter`
internally). PR #6 had removed `EnableUnsafeBinaryFormatterSerialization` + the Formatters
package believing the prefs shim was their only consumer; Infragistics is a second,
grep-invisible consumer. **Fixed in PR #10** (restored the flag + package, scoped to
Infragistics). The 30–60 s per-PR smokes never hit it; only a sustained run did. This is
the headline argument for the shakedown.

---

## Follow-up A — Guard the `OnApplicationExit` shutdown invoke (pre-existing race)

**Severity:** low (handled, non-fatal) · **Regression?** No — pre-existing.

**Symptom.** On shutdown, `error.log` gets an entry:

```
System.InvalidOperationException: Invoke or BeginInvoke cannot be called on a control
until the window handle has been created.
  at System.Windows.Forms.Control.Invoke(Delegate, Object[])
  at RssBandit.GuiInvoker.DoInvoke(...)                      GuiInvoker.cs:123
  at RssBandit.GuiInvoker.Invoke(...)                        GuiInvoker.cs:52
  at RssBandit.RssBanditApplication.<.ctor>b__136_0(Action)  RssBanditApplication.cs:286
  at RssBandit.RssBanditApplication.OnApplicationExit(...)   RssBanditApplication.cs:4617
  at System.Windows.Forms.Application.ThreadContext.DisposeInternal(...)
  at RssBandit.RssBanditApplication.StartMainGui(...)        RssBanditApplication.cs:584
-- Nested: RssBandit.BanditApplicationException: StartMainGui() exiting main event loop on exception.
```

**Root cause.** During application exit, `OnApplicationExit` marshals an action onto the UI
thread through `GuiInvoker.Invoke` → `Control.Invoke`, but by then the control's window
handle has already been destroyed, so `Control.Invoke` throws. The exception propagates out
of the message loop to `StartMainGui`'s catch, which wraps and publishes it.

**Provenance.** `git blame` shows these lines were last changed in old commits
(`ba162927`, `3e7e15df`) — **untouched by any 2026-06-26 PR.** It surfaced now only because
the shakedown closed the app via `Form.CloseMainWindow()` (a `WM_CLOSE` path), not the
normal `File → Exit`.

**Open question.** Does it reproduce on a normal `File → Exit`, or only on the `WM_CLOSE`
path? Needs a human/interactive check (see checklist #6).

**Proposed fix.** In `GuiInvoker.DoInvoke` (or at the `OnApplicationExit` call site), short-
circuit when the target control can't be invoked:

```csharp
if (control == null || control.IsDisposed || !control.IsHandleCreated)
{
    // We're tearing down; run inline on the current thread, or skip.
    action();          // or: return; if the action is UI-only and pointless post-teardown
    return;
}
```

Then verify a normal `File → Exit` produces no `error.log` entry. Keep it minimal — this is
defensive hardening of an exit path, not a behavior change.

---

## Follow-up B — Drop `BinaryFormatter` for real (Infragistics toolbar state → XML)

**Severity:** medium (completes PR #6's intent) · **Effort:** moderate, needs runtime verification.

**Why.** PR #10 had to restore `EnableUnsafeBinaryFormatterSerialization` and the
`System.Runtime.Serialization.Formatters` package because Infragistics
`UltraToolbarsManager.SaveAsBinary` / `LoadFromBinary` serialize the toolbar/menu state
with `BinaryFormatter` internally. That is now the **only** reason the unsafe flag remains.
Moving toolbar-state persistence off binary lets us re-remove the flag + package and finish
the "no `BinaryFormatter` in the build" goal.

**Approach.** In `WinGui/Controls/StateSerializationHelper.cs`, switch the toolbar
serialization from binary to XML:

- `UltraToolbarsManager.SaveAsBinary(stream, …)` → `SaveAsXml(stream, …)`
- the matching restore (`LoadFromBinary`) → `LoadFromXml`
- **Verify these APIs exist in Infragistics WinForms 20.2.14** before committing to this
  (decompile `Infragistics.Win.UltraWinToolbars` if needed). The persisted value in
  `UiStateSettings` (`…/Toolbars`, currently base64 of the binary blob) becomes XML text.

**Audit the neighbours.** `OnSaveConfig` also persists other Infragistics/SandDock state —
`SaveExplorerBar` (Navigator), `sandDockManager.GetLayout()`, and (under
`USE_USE_UltraDockManager`, currently off) the dock manager. Confirm whether any of those
also route through `BinaryFormatter`; if so they need the same XML treatment, or the flag
can't be dropped.

**Migration.** Existing saved toolbar state (base64 binary) will not load with
`LoadFromXml`. This is low-cost — it's window/toolbar *layout*, not user data, and it resets
to the code-defined default on first run after upgrade. Handle a failed/old load gracefully
(catch → fall back to defaults), which the load path largely already does. Note: there is no
`.settings.xml` for this build in the wild yet, so the migration window is effectively empty.

**Done when.** Toolbar state saves and restores across a restart with the unsafe flag +
Formatters package **removed again** (re-applying PR #6's csproj change), build 0 errors,
and a sustained run shows no `PlatformNotSupportedException`.

---

## Coverage gap — human last-mile verification checklist (~2 minutes)

UIAutomation could not reliably drive the deep interactive paths on this Infragistics app:
`InvokePattern.Invoke` on a menu item that opens a **modal** dialog blocks until the dialog
closes (times out the automation), and `FindAll(Descendants)` over the main window's UIA
tree (feed tree + list + WebView2) times out. These last-mile checks are a quick human
click-through against a build of `develop` (with PR #10):

1. **Tools → Options (Preferences):** opens; tabs render; **no Enclosure/Podcast tab**;
   OK and Cancel both work.
2. **Right-click a feed → Properties (`FeedProperties`):** opens; **no enclosure/attachment
   tab**; Save works.
3. **Right-click a category → Properties (`CategoryProperties`):** same.
4. **An item with an enclosure:** the "Download/Open Attachment" context menu **shell-opens
   the URL** in the default handler (sub-step A behaviour from the podcast work).
5. **Search:** returns results, no error.
6. **File → Exit (normal exit):** app closes cleanly; check `error.log` for the
   `OnApplicationExit` race (Follow-up A) — its presence/absence here answers the open
   question above.

---

## Bottom line

Base is shakedown-validated and the one real regression is fixed (PR #10 — merge to
un-break toolbar save). Follow-ups A and B are tracked here; neither blocks the next
strategic step (FeedSource decomposition vs. the MAUI core/UI split). The checklist closes
the last 5% of interactive confidence that autonomous UI automation can't reach here.
