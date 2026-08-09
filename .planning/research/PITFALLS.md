# Pitfalls Research

**Domain:** WinForms owner-drawn controls, live Windows-accent-color theming, and Form-lifecycle absorption (Rig Toggle v2.1 — Modern UI Redesign & Theme Backlog)
**Researched:** 2026-08-09
**Confidence:** MEDIUM-HIGH (grounded in this codebase's own rig-disproven history — Phase 12 theming, Phase 13 GDI+ seams — plus verified WM_DWMCOLORIZATIONCOLORCHANGED/registry behavior from Microsoft docs and community sources; accent-color specifics are flagged LOW/MEDIUM where only WebSearch-level verification was available and MUST be rig-confirmed)

This document is scoped tightly to the four new surfaces v2.1 adds on top of the existing WinForms app (`RigToggle.App`): a custom-drawn toggle-switch control (THEME-08), live accent-color reading (THEME-07), a manual theme override composed with live theme-follow (THEME-09), and MainForm absorbing `MonitorPanelForm`'s tile grid / hotplug refresh / exclusive-access lease / Identify-overlay wiring, plus the SettingsForm layout rework. It deliberately does NOT restate generic WinForms advice — every pitfall below is anchored to a specific fact already established in this codebase (verified by reading `MainForm.cs`, `MonitorPanelForm.cs`, `ThemeApplier.cs`, `WindowsThemeProvider.cs`, `DwmTitleBar.cs`, `MonitorIdentifyOverlay.cs`, and the Designer files directly, 2026-08-09) or by external verification (WebSearch, Microsoft Learn) done for this milestone specifically.

## Critical Pitfalls

### Pitfall 1: New custom-drawn controls (toggle switch, tiles) silently fall outside the existing per-control theming pipeline

**What goes wrong:**
`MainForm.OnThemeChanged` and `MonitorPanelForm.OnThemeChanged` are both hand-written, fixed sequences of explicit `ThemeApplier.ThemeXxx(control, isDark)` calls (`ThemeButton(btnToggle, ...)`, `ThemeButton(btnSettings, ...)`, `ThemeMonitorGrid(dgvMonitorPanel, ...)`, etc.) — there is no generic "walk the Controls tree and re-theme everything" mechanism, by design (the doc comment on `ThemeApplier` explicitly says it is "deliberately NOT a recursive Controls-tree walk"). A new custom-drawn toggle-switch control or tile control added for THEME-08 will not automatically participate in this pipeline. If the dev builds the control's colors once at construction time and forgets to add an explicit re-theme call to both `MainForm.OnThemeChanged` and `MainForm.InitializeTrayState()` (the `--tray`-safe-startup path, which is a second, separate call site that already themes `btnToggle`/`btnSettings`/`btnMonitors` today), the control will render correctly at startup in whichever mode Windows happened to be in, then silently freeze in that mode forever while every other control flips live.

**Why it happens:**
This is exactly the shape of bug Phase 12 already hit twice (missed `dgvMonitors`, missed `txtHotkey`'s literal `SystemColors.*` assignments, then a second gap-closure round for Button/ComboBox) — the pipeline is a manually maintained list, and manually maintained lists are the failure mode. A brand-new control type is the single easiest thing to leave off that list, because it doesn't exist yet when the list was last correct.

**How to avoid:**
Treat "add the new control's ThemeApplier-equivalent call to every existing theming call site" as a first-class acceptance criterion, not an afterthought. Concretely: grep for every existing call to `ThemeApplier.ThemeButton`/`ThemeMonitorGrid`/etc. across `MainForm.cs` (both `OnThemeChanged` and `InitializeTrayState`) before writing the new control, and add the new control's theming call to the exact same two places, not just one. If THEME-09's manual-override composition (Pitfall 5) changes how "is dark" is resolved, the new control must consume that same resolution, not read `_themeProvider.CurrentTheme` directly.

**Warning signs (rig-verify, not unit test):**
Set Windows to Light mode, launch the app, flip Windows to Dark via Settings > Personalization > Colors while the app is running (not via app restart), and watch the toggle switch / tiles specifically — if they don't recolor while the buttons around them do, this pitfall has landed. Repeat with the app started in `--tray` (autostart) mode, opened from the tray after the flip, since `InitializeTrayState()` is a second, independently-themed code path.

**Phase to address:**
The phase that implements THEME-08 (toggle switch) and the MainForm-absorption phase that builds the tile grid — both must include this rig-verify step as an explicit checkpoint, not just "does it look right at startup."

---

### Pitfall 2: Toggle-switch / tile rendering reproduces the Phase 13 `GraphicsPath.DrawPath` seam-artifact bug

**What goes wrong:**
A toggle-switch control's natural implementation is a rounded "pill" track plus a circular thumb, and per-monitor tiles naturally combine a rounded-rect background with an icon glyph and a status dot — all overlapping shapes. `RigToggle.IconGen/IconGeometry.cs` already documents, in comments, that combining multiple overlapping sub-shapes into one `GraphicsPath` and then calling `FillPath` + `DrawPath` on the combined path produces real seam artifacts at shape-overlap boundaries — `DrawPath` strokes each touching/overlapping sub-shape's boundary independently rather than a merged contour. This was invisible to a human glance in Phase 13's rig pass and was only caught by a pixel-level diagnostic in code review, not the live rig session.

**Why it happens:**
It's a genuine GDI+ behavior (not a coding mistake per se) — `GraphicsPath` doesn't compute a boolean union of overlapping figures before stroking, so any "draw an outline around a combined multi-shape path" approach reproduces the bug whenever two shapes in the toggle switch or tile visually overlap or touch (e.g., thumb resting against the track's rounded end at the "off" position, or an icon glyph touching a tile's border).

**How to avoid:**
Reuse the exact fix this codebase already validated: stroke-then-fill compositing (draw/fill each shape as a separate, non-combined `GraphicsPath`/`FillPath` call in back-to-front order, rather than unioning them into one path before stroking). Do not combine the toggle-switch track and thumb into a single `GraphicsPath` for outline purposes. If a pixel-level diagnostic tool/script exists from Phase 13 (`13-04` gap closure), reuse or adapt it for the new control rather than relying on a rig glance alone.

**Warning signs (rig-verify, not unit test):**
At 100% and at a non-100% Windows display-scale setting (see Pitfall 9), zoom a screenshot of the toggle switch in the "off" position (thumb resting near the track edge) and the tiles at their icon/border overlap points, and look specifically at the overlap boundary for a double-line or notch — a human glance at normal viewing distance is exactly what missed this bug in Phase 13's first rig pass, so this needs a zoomed screenshot check, not just "looks fine."

**Phase to address:**
The phase implementing THEME-08's rendering code, and the phase building the tile visuals — both should budget a pixel-level diagnostic step, not just a rig glance, before calling the visual "done."

---

### Pitfall 3: Owner-drawn control flicker/redraw failure from missing double-buffering and Mica-backdrop interaction

**What goes wrong:**
A hand-rolled `Control` subclass with an `OnPaint` override does not double-buffer by default; every hover/pressed/checked state change (mouse-enter, mouse-down, click) triggers a full repaint, and without `ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint` set, that repaint visibly flickers — especially the toggle switch's checked-state animation (thumb sliding) if one is implemented, and especially on the tiles' hover-highlight state during mouse movement across the tile row. Separately, both MainForm and (currently) MonitorPanelForm apply Mica backdrop (`DwmTitleBar.ApplyRoundedCornersAndMica`, `DWMSBT_MAINWINDOW`) — a custom control painted with a solid, non-matching `BackColor` instead of inheriting the parent's actual rendered background will show as an opaque rectangle "floating" over the translucent Mica surface instead of blending, and if the control's background color is computed once and cached rather than re-sampled, it can visibly desync from the Mica tint after a theme flip.

**Why it happens:**
`Control`'s base painting behavior assumes simple, infrequent repaints; owner-draw controls with frequent state-driven repaints (hover tracking in particular fires on every `MouseMove`) need the double-buffer style flags explicitly, which is easy to skip when a control "looks right" in a static screenshot but hasn't been interacted with live. The Mica interaction is specific to this app because it's one of the only WinForms apps in this codebase's experience that combines Mica backdrop with owner-drawn child controls — the existing custom-drawn surfaces (tray/exe icons) are pre-rendered bitmaps composited at icon-generation time, not live-painted controls sitting on a Mica window, so this exact interaction has no prior art in this codebase to copy from.

**How to avoid:**
Set `SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true)` in the new control's constructor. For the Mica-blend concern, do not paint an opaque background rectangle inside the control's `OnPaint` unless the design intentionally wants a solid card behind the toggle/tile (which is a legitimate design choice) — if the intent is "floats on the Mica surface," leave the control's own background transparent/parent-inherited and only paint the track/thumb/tile-card shapes themselves, matching whatever the tile-card design decision actually is.

**Warning signs (rig-verify, not unit test):**
Drag the mouse slowly across the tile row and over the toggle switch on real Windows 11 hardware and watch for visible tearing/flicker on hover-state transitions — this cannot be seen in a static screenshot or in a build log; it requires live interaction. Also check the toggle switch and tiles immediately after a live theme flip (Pitfall 1's same test) for a visible one-frame "wrong background" flash.

**Phase to address:**
The phase implementing THEME-08's control (constructor-time style flags) and the MainForm-absorption phase building the tile grid on the Mica-backed MainForm.

---

### Pitfall 4: Accent-color source ambiguity — the value read does not match what Settings > Colors shows

**What goes wrong:**
There is no single authoritative, documented Win32 API for "the accent color the user picked in Settings > Personalization > Colors." At least three distinct registry/API sources exist and can disagree: (1) `HKCU\Software\Microsoft\Windows\DWM\ColorizationColor` / the `DwmGetColorizationColor` API — this is the DWM glass/title-bar tint value, which is influenced by (and can differ from) the raw accent swatch, especially when "Show accent color on title bars and windows borders" is toggled off in Settings, in which case this value may not reflect the picked accent at all; (2) `HKCU\Software\Microsoft\Windows\DWM\AccentColor`; (3) `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent\AccentColorMenu` — none of these three are officially documented as "the" accent color, and community sources (used to research this milestone, MEDIUM confidence — not verified against current Microsoft Learn documentation, which does not officially document any of them for this purpose) report they can diverge. A naive implementation that reads whichever key it found in a tutorial can end up recoloring the toggle switch / accent-driven elements a shade that visibly does not match what the user sees in their own Settings > Colors panel.

**Why it happens:**
Accent color has never had a clean public WinRT/Win32 contract for classic Win32 apps the way light/dark theme (`AppsUseLightTheme`) does — this is an area where undocumented registry reads are the norm, and different registry values serve different rendering surfaces (title bar glass tint vs. Start/taskbar accent swatch vs. the raw "AccentColorMenu" palette used by shell surfaces), so "accent color" is not actually one value in Windows' own implementation.

**How to avoid:**
Do not trust a single source without comparing it live against Settings > Colors. Prefer `HKCU\Software\Microsoft\Windows\DWM\AccentColor` (the value name most directly associated with the "accent color" swatch itself, distinct from `ColorizationColor`'s title-bar-tint semantics) as the primary read, falling back to `ColorizationColor`/`DwmGetColorizationColor` only if `AccentColor` is absent — but treat this ordering as a hypothesis to be rig-verified, not a settled fact, since this milestone could not verify it against current Microsoft documentation (none exists) or a live Windows 11 registry dump. Whichever key is chosen, mask/format it correctly: `DwmGetColorizationColor`'s value is `0xAARRGGBB` and the registry `DWORD` values are typically stored in a similar packed format — an implementation that treats the alpha byte as part of the RGB value (rather than stripping it) will produce a visibly wrong (too dark/washed) color.

**Warning signs (rig-verify, not unit test):**
On real Windows 11 hardware, open Settings > Personalization > Colors, note the exact accent swatch shown, then compare it pixel-for-pixel (color picker tool, not eyeballing) against what the app renders for its accent-driven elements. Repeat after picking a custom (non-preset) accent color, and again with "Show accent color on title bars and windows borders" toggled both on and off, since that setting is a plausible source of the ColorizationColor-vs-AccentColor divergence described above. This is exactly the "verify accent-color read against Settings > Colors on real hardware, don't trust registry key presence alone" rig-verify step this milestone must not skip — this codebase has already been burned twice (Phase 12, Phase 13) by an unproven bet standing in for a rig check.

**Phase to address:**
The phase implementing THEME-07 (live accent-color reading) — this pitfall should be the single most heavily rig-verified item in that phase, given the total absence of official documentation to fall back on.

---

### Pitfall 5: Accent-color-change notification is unreliable — live update silently stops working

**What goes wrong:**
`WM_DWMCOLORIZATIONCOLORCHANGED` (the most commonly cited notification for a live accent-color change) is documented by community sources (MEDIUM confidence — not Microsoft Learn primary documentation, which describes the message's payload but not its reliability) to sometimes fire multiple times for a single accent change and sometimes not fire at all on some Windows versions. If THEME-07's live-update path is wired only to this message (or only to `SystemEvents.UserPreferenceChanged` without confirming that category actually covers accent-color changes, as opposed to just light/dark theme, which `WindowsThemeProvider` already narrowly diffs on `AppsUseLightTheme`), a live accent-color change made while the app is running may simply not update the UI until the app is restarted — a subtler, harder-to-notice failure than THEME-01/02's original theme-follow gap, because "the accent color looks a little stale" is much easier to miss on a rig glance than "the title bar is the wrong color."

**Why it happens:**
Same class of problem this codebase already hit with `Application.SetColorMode` — a plausible-sounding API/message that training-data-level knowledge suggests should work, but whose actual behavior on the current OS/runtime is either undocumented or documented-but-flaky.

**How to avoid:**
Wire the live-update path but do not assume it is reliable — pair it with a defensive periodic re-check only if the message-only approach fails rig verification (avoid adding polling preemptively; this codebase's existing convention, per `WindowsThemeProvider`, is event-driven with a diff-against-last-known-value, not polling). If `WM_DWMCOLORIZATIONCOLORCHANGED` proves unreliable on the rig, prefer subscribing via `SystemEvents.UserPreferenceChanged` (already proven reliable in this codebase for theme) and re-reading the accent color on every fire, diffed against the last-known value exactly like `WindowsThemeProvider.OnUserPreferenceChanged` already does for `AppsUseLightTheme` — reuse that pattern rather than inventing a new one.

**Warning signs (rig-verify, not unit test):**
With the app running (not restarted), change the accent color in Settings > Colors several times in a row, including picking the exact same color twice in a row (a no-op change that some notification paths miss entirely), and confirm the app's accent-driven elements update every single time, not just the first time.

**Phase to address:**
The phase implementing THEME-07's live-update path — flag it as needing extra rig-verification rounds (multiple flips in one session, not just one before/after check) given the documented flakiness.

---

### Pitfall 6: Manual theme override (THEME-09) doesn't actually suppress live theme-follow, or vice versa

**What goes wrong:**
This codebase currently has exactly three independent "is dark mode active right now" resolution points that each read `_themeProvider.CurrentTheme` directly and are deliberately "never cached... read fresh every call" (per `MainForm.IsDark`'s own doc comment, which explicitly says this "mirrors SettingsForm.IsDarkTheme" — a second, independently-maintained copy of the same one-line property, and `MonitorPanelForm.IsDarkTheme` makes it a third). Introducing a manual override (THEME-09) means every one of these must change from "read the live OS theme" to "read the override if set, else the live OS theme" — and because the codebase's own pattern is copy-the-property-into-each-form rather than a shared resolver, it's easy to update two of the three and miss the third (especially since v2.1 is simultaneously deleting `MonitorPanelForm` and folding its logic into MainForm — a moving target makes "did I update every copy" harder to audit by inspection). A second, subtler failure: `OnThemeChanged` (the OS-live-theme-flip handler) currently re-derives and re-applies theming unconditionally on every OS flip — if it isn't updated to check the override setting first, a user who has set a manual "Dark" override will see the app snap back to whatever Windows' own live theme is the next time Windows' theme flips, silently discarding their override.

**Why it happens:**
The existing pattern (independent per-form `IsDark`/`IsDarkTheme` properties, each documented as "mirrors" the others rather than sharing an implementation) was a reasonable shortcut for theme-only, but becomes a genuine multi-location consistency risk once a second input (manual override) needs to compose with the first (live OS theme) — "mirror this logic in each form" and "add a new conditional to logic that's mirrored in three places" combine badly.

**How to avoid:**
Introduce one single "effective theme" resolver (a single method/property, ideally on a small shared helper or on `AppSettings`/a theme-resolution service, not copy-pasted per form) that composes override + live theme once, and have every existing `IsDark`/`IsDarkTheme` property and every `OnThemeChanged` handler call through it rather than reading `_themeProvider.CurrentTheme` directly. Since `MonitorPanelForm` is being deleted this same milestone, do not carry its `IsDarkTheme` copy forward into MainForm as a fourth copy — this is the natural moment to collapse to one shared resolver instead of perpetuating the pattern.

**Warning signs (rig-verify, not unit test):**
Set a manual Dark override, then flip Windows' own live theme to Light while the app is running — if the app follows Windows back to Light, the override isn't being honored. Separately, confirm the override is honored identically on MainForm, SettingsForm, and the absorbed tile/monitor-panel surface within MainForm — a divergence between them (one area honors the override, another doesn't) is the direct symptom of the "missed one of three copies" failure mode.

**Phase to address:**
The phase implementing THEME-09 — should explicitly include "collapse per-form IsDark properties into one resolver" as a task, not just "add a setting."

---

### Pitfall 7: MainForm absorbing MonitorPanelForm's lease/event-subscription lifecycle reintroduces the exact race the lease was built to prevent

**What goes wrong:**
`MonitorPanelForm.DisableMonitor`/`EnableMonitor` explicitly acquire `_orchestrator.BeginExclusiveMonitorAccess()` *before* opening `MonitorConfirmDialog.ShowDialog()`, specifically because `ShowDialog()` runs a nested message loop that dispatches `WM_HOTKEY` — without the lease, a hotkey-triggered toggle could start underneath a half-finished panel action. Meanwhile, `MainForm.BtnToggle_Click` does NOT itself acquire this lease explicitly — it calls `_orchestrator.ToggleToRigMode()`/`ToggleToNormalMode()` directly, relying on the orchestrator's own internal `_busy`/`RunGuarded` mechanism (the same flag `BeginExclusiveMonitorAccess()` shares, per this project's own Key Decisions). When the tile grid's click-to-toggle-a-monitor action moves onto MainForm — the same class that already owns `BtnToggle_Click` — a developer "simplifying" the merged class could plausibly remove the explicit lease acquisition around tile actions on the theory that "it's the same form now, the busy flag already covers it" — but the lease's actual purpose (blocking a *concurrent* hotkey-triggered toggle from starting while `MonitorConfirmDialog.ShowDialog()`'s nested message pump is dispatching `WM_HOTKEY` mid-tile-action) has nothing to do with which Form class hosts the code; removing it reopens exactly the race Phase 17 built the lease to close, and it will not be caught by a static/compile-time check — only by a rig test that deliberately triggers the hotkey while a tile's confirm dialog is open.

**Why it happens:**
Folding two Forms' worth of logic into one class creates surface-level redundancy (two `IsDarkTheme`-style properties, two safety mechanisms that both ultimately gate the same `_busy` flag) that looks like duplication ripe for "simplification," but the two call sites' lease usage is not actually redundant — they exist for different reasons (orchestrator-internal guard for the toggle button's own two-step flow; explicit lease for the tile action's need to hold exclusivity across a nested message pump). Merging the classes makes this distinction less visually obvious, not more.

**How to avoid:**
When folding tile-click monitor mutation into MainForm, port `MonitorPanelForm.DisableMonitor`/`EnableMonitor`'s lease-then-`using`-scope structure verbatim (acquire `BeginExclusiveMonitorAccess()` before any `ShowDialog()` call, hold it across the mutation, release via `using`) — do not "unify" it with `BtnToggle_Click`'s different pattern just because both now live in the same class. Leave a comment at the tile-action call site cross-referencing this specific reason (nested-message-loop hotkey race during a confirm dialog), so a future edit doesn't remove it as apparent dead-looking redundancy.

**Warning signs (rig-verify, not unit test):**
With a global hotkey configured, click a tile to disable a monitor, and while the confirmation dialog (`MonitorConfirmDialog`) is open and blocking, press the configured hotkey — the hotkey-triggered toggle should be rejected with the existing "toggle in progress" message, not silently proceed underneath the open dialog. This exact scenario (nested dialog + concurrent hotkey) is what Phase 17 built the lease for and is the only way to actually exercise this pitfall; a click-through without ever triggering the hotkey mid-dialog will not surface it.

**Phase to address:**
The MainForm-absorption phase (tile grid + lease + Identify overlay folded in) — should include this exact hotkey-during-tile-confirm-dialog scenario as a named rig-verify checkpoint, not just "tiles toggle monitors correctly."

---

### Pitfall 8: Event-subscription lifecycle mismatch between an app-lifetime Form (MainForm, hidden not disposed) and a closable-and-reopenable Form (MonitorPanelForm)

**What goes wrong:**
`MonitorPanelForm` subscribes to `_themeProvider.ThemeChanged` and `SystemEvents.DisplaySettingsChanged` in its constructor and explicitly unsubscribes both in a `FormClosed` handler, with an inline comment noting this panel is "non-modal and potentially long-lived... unlike SettingsForm/MonitorConfirmDialog's fresh-per-open... idiom" — i.e., its lifecycle pattern was deliberately built around the fact that it can be closed and reopened (`FormClosed` fires, then `MainForm.OpenMonitorPanel()` recreates a fresh instance on next open, per `_monitorPanelForm.IsDisposed` check). `MainForm` itself never unsubscribes its own `ThemeChanged` handler (subscribed once in the constructor, matching a true app-lifetime singleton: it is `Hide()`'d, not disposed, on close-to-tray, and `FormClosed` for MainForm only fires at real process exit). If the tile grid's hotplug refresh (`SystemEvents.DisplaySettingsChanged`) is wired into MainForm using MonitorPanelForm's exact pattern (subscribe in constructor, unsubscribe in `FormClosed`) without accounting for the fact that MainForm's `FormClosed` essentially never fires during normal tray-resident operation, this is harmless (the subscription simply lives for the app's whole life, matching the existing `ThemeChanged` subscription's own pattern) — but if a developer instead tries to be "more correct" by unsubscribing/resubscribing `DisplaySettingsChanged` around `Hide()`/`Show()` (mirroring how the standalone panel's users open/close it), the hotplug refresh will silently stop working for a monitor plugged/unplugged while MainForm is hidden to tray, and resume only after the user reopens the window — a regression from the current standalone-panel behavior only in the sense that the whole point of absorbing the panel was presumably to make monitor status "just always current," not conditionally current based on visibility.

**Why it happens:**
The two source Forms encode two different, both-correct-for-their-own-Form lifecycle assumptions (closable/reopenable vs. app-lifetime/hide-not-close), and copying one Form's subscribe/unsubscribe pattern onto the other's actual lifecycle without re-deriving it from first principles produces a plausible-looking but wrong result either way (leaving MonitorPanelForm's unsubscribe-on-FormClosed pattern in place literally would just never fire and be harmless-but-dead code; actively adding a Hide/Show-gated unsubscribe would be an active regression).

**How to avoid:**
Decide explicitly, as a first step of the absorption work (not an incidental side effect of copy-pasting code): does the tile grid need to stay live-updated while MainForm is hidden to tray? If yes (matching "hotplug status should always be current" as the presumable goal of absorbing PANEL-03's live-refresh requirement), subscribe once at MainForm construction time and never unsubscribe until real process exit — matching the existing `ThemeChanged` pattern already in `MainForm`, not `MonitorPanelForm`'s closable-Form pattern. Document this decision inline so a future edit doesn't "fix" it by adding a Hide/Show-gated unsubscribe under the mistaken belief that's more correct.

**Warning signs (rig-verify, not unit test):**
Hide MainForm to tray (not close the app), physically unplug/replug a monitor (or use Windows' virtual-display toggle if no spare monitor is available), then restore MainForm from the tray and check whether the tile grid already reflects the hotplug change (it should, if the subscription stayed live while hidden) versus only updating after the restore triggers some other refresh path.

**Phase to address:**
The MainForm-absorption phase — should state explicitly, in its own scope notes, whether hidden-to-tray hotplug refresh is in scope, and rig-verify whichever answer was chosen.

---

### Pitfall 9: DPI/AutoScaleMode.Font pixel-math breakage in new owner-drawn controls and the reworked SettingsForm layout

**What goes wrong:**
All three existing forms (`MainForm`, `SettingsForm`, `MonitorPanelForm`) use `AutoScaleMode.Font`, and `RigToggle.App.csproj` has no explicit `ApplicationHighDpiMode`/app manifest setting — a gap already flagged in this codebase's own prior research (`17-RESEARCH.md` Pitfall 3, re-confirmed by direct file read for this milestone). `AutoScaleMode.Font` correctly rescales standard control positions/sizes as Windows' text-scale factor changes, but any pixel-literal math inside a new owner-drawn `OnPaint` (toggle-switch pill/thumb radii, tile icon/number/status-dot layout coordinates) is invisible to that mechanism entirely — the control's outer bounds will scale (inherited from its parent container's Font-based scaling), but geometry computed from hardcoded pixel constants inside `OnPaint` will not, producing a control whose outer box is correctly sized for the current DPI but whose internal drawing (thumb size, corner radius, icon placement) looks correct only at the one scale factor it was designed/tested at — too large, too small, or clipped at other scales. The `SettingsForm` layout rework compounds this: migrating from the current plain, `SuspendLayout`/`ResumeLayout`-wrapped `Panel`-based absolute positioning (confirmed by direct read — no `TableLayoutPanel`/`FlowLayoutPanel` currently exists anywhere in `SettingsForm.Designer.cs`) to `TableLayoutPanel`/`FlowLayoutPanel` changes how child controls (in particular the two monitor `DataGridView`s, which have their own internal scroll/column-width behavior) grow and shrink under `Dock`/`Anchor` inside the new container type — behavior that is easy to get right at design-time 100% scale and wrong only at a different scale, since the WinForms designer itself typically runs and is checked at 100%.

**Why it happens:**
`AutoScaleMode.Font`-driven scaling is a property-level mechanism (it rescales `Control.Bounds`/`Font` on standard WinForms controls) — it has no visibility into what a custom `OnPaint` override actually draws inside those bounds, so any control whose visual identity depends on hand-computed pixel geometry needs that geometry to be explicitly derived from the control's current size/DPI, not hardcoded. This is new risk surface for this codebase specifically because no existing owner-drawn `Control` (as opposed to pre-rendered icon bitmaps, which are generated once at build time by `RigToggle.IconGen` and don't participate in the app's own live scaling at all) exists to have already hit this.

**How to avoid:**
Compute all `OnPaint` geometry (track/thumb dimensions, tile icon/label/dot positions) relative to `ClientSize`/`Font.Height`/`DeviceDpi` at paint time, never as hardcoded pixel literals — e.g., thumb radius as a fraction of `ClientSize.Height`, not a fixed `12`. For the SettingsForm layout rework, explicitly design each migrated section (monitor grids, audio dropdowns, app path, hotkey box) to behave correctly under `Dock = DockStyle.Fill` inside its new `TableLayoutPanel`/`FlowLayoutPanel` cell at multiple scale factors, not just the one the designer surface shows by default.

**Warning signs (rig-verify, not unit test):**
On real Windows 11 hardware, set display scale to 125% and 150% (Settings > System > Display > Scale), relaunch the app, and visually check both the toggle switch/tiles (thumb/track proportions, icon/label/dot alignment inside tiles) and the reworked SettingsForm (no clipped/overlapping controls, `DataGridView`s still fully usable) at each scale — this is exactly the kind of check a 100%-only build-environment or design-time-only pass cannot perform, consistent with this project's own established pattern of DPI/CCD-topology issues only being provable on real rig hardware.

**Phase to address:**
The phase implementing THEME-08's control (paint-time geometry derivation) and the SettingsForm layout-rework phase (multi-scale-factor layout check) — both should add a non-100%-scale rig pass as an explicit checkpoint, since this project's build/dev environment cannot exercise Windows display scaling at all.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|--------------------|-----------------|------------------|
| Skipping `ControlStyles.OptimizedDoubleBuffer` on the new toggle-switch/tile controls until flicker is actually visible on the rig | Slightly less constructor boilerplate | Guaranteed rig-visible flicker once hover/press states are exercised live — this is not a "might happen at scale" risk, it's near-certain on first live interaction | Never — set the style flags up front; this is a one-line constructor addition, not worth deferring |
| Reading a single accent-color registry key without a fallback/verification path (Pitfall 4) | Faster THEME-07 implementation | Silent mismatch against Settings > Colors that a user notices but a rig glance easily misses (subtle color-shade difference, not a broken/missing control) | Only as a first draft during implementation, never as the shipped state without a rig color-picker comparison |
| Copying `MonitorPanelForm`'s FormClosed-based unsubscribe pattern verbatim onto MainForm without re-deriving it for MainForm's hide-not-close lifecycle (Pitfall 8) | Faster to write (copy-paste) | Either dead-but-harmless code (subscription lives forever anyway, matching intent by accident) or an active regression (hotplug stops refreshing while hidden), depending on exactly how it's copied — the shortcut itself doesn't tell you which outcome you got | Never — this specific decision must be made deliberately, not inherited by copy-paste |
| Leaving three independent `IsDark`/`IsDarkTheme` properties in place and adding override-checking logic to each separately (Pitfall 6) | No shared-helper refactor needed | High chance of missing one location, especially mid-milestone while `MonitorPanelForm` is simultaneously being deleted | Never for this milestone specifically — the MonitorPanelForm deletion is the natural, already-scheduled moment to collapse to one resolver instead |
| Hardcoding `OnPaint` geometry at the one scale factor visible in the WinForms designer / build environment (Pitfall 9) | Faster initial visual pass | Broken proportions/clipping at any non-100% scale, undetectable in this project's build environment (no Windows GUI available), only catchable on the real rig at a non-default scale | Never as shipped state — must derive geometry from control size/DPI, even though it costs a bit more up-front design work |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|------------------|-------------------|
| DWM accent-color registry/API (THEME-07) | Trusting `DwmGetColorizationColor`/`ColorizationColor` as "the" accent color without comparing against Settings > Colors live | Rig-verify against the live Settings > Colors swatch before trusting any single source; treat the "which registry key" question as unresolved until proven on hardware (Pitfall 4) |
| `WM_DWMCOLORIZATIONCOLORCHANGED` (THEME-07 live update) | Assuming the message fires reliably exactly once per accent change, mirroring how `AppsUseLightTheme`'s `UserPreferenceChanged` already behaves in this codebase | Reuse `WindowsThemeProvider`'s diff-against-last-known-value pattern via `SystemEvents.UserPreferenceChanged` if the DWM message proves flaky on the rig (Pitfall 5) |
| `ToggleOrchestrator.BeginExclusiveMonitorAccess()` (Form absorption) | Treating the lease as redundant once tile-click mutation and the toggle button share one class, and removing it during a "cleanup" pass | Keep the lease exactly where `MonitorPanelForm` already proved it necessary — acquired before any `ShowDialog()` call in the tile-click path — regardless of which Form class hosts the code (Pitfall 7) |
| `SystemEvents.DisplaySettingsChanged` (hotplug refresh, Form absorption) | Copying `MonitorPanelForm`'s subscribe-in-constructor/unsubscribe-in-FormClosed pattern onto MainForm, whose `FormClosed` essentially never fires during normal tray-resident use | Subscribe once at MainForm construction and leave subscribed for app lifetime, matching MainForm's own existing `ThemeChanged` subscription pattern, not MonitorPanelForm's closable-Form pattern (Pitfall 8) |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| Allocating fresh `Bitmap`/`Brush`/`Pen`/`GraphicsPath` objects inside a tile control's `OnPaint` on every repaint, instead of caching per-instance fields | GDI handle count climbs slowly over a long tray-resident session (this app is designed to stay running for hours/days); eventually `OutOfMemoryException`-style GDI resource exhaustion, or simply degraded repaint performance | Cache brushes/pens/paths as instance fields created once (constructor or lazily-once), matching `MonitorPanelForm`'s existing `_dotActive`/`_dotInactive` "built ONCE and shared across all rows and all refreshes" pattern — but note tiles are individual `Control` instances, not `DataGridView` cell values, so the "shared across rows" pattern must become "one set of reusable drawing resources per tile instance," not literally copy-pasted | Only surfaces after hours of a tray-resident session with repeated hover/redraw churn — will not show up in a quick rig click-through, so this needs an explicit long-session check, not just a functional pass |
| Dynamically creating/destroying tile `Control` instances on every hotplug event without disposing removed ones | Same GDI/handle leak symptom as above, triggered by repeated monitor connect/disconnect cycles rather than by mouse movement | `Controls.Remove()` does not call `Dispose()` — any tile-repopulation logic that removes and recreates tile controls on hotplug (mirroring `PopulateMonitorGrid`'s `Rows.Clear()`) must explicitly `Dispose()` each removed tile control, not just remove it from the `Controls` collection | Surfaces after repeated hotplug cycles in one long session (unplug/replug testing), not on a single hotplug test |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Persisting a raw accent-color value (or the manual theme override) via a code path that bypasses this app's existing `System.Text.Json`/`%APPDATA%\RigToggle\settings.json` convention (e.g., writing an ad-hoc file or registry key for THEME-09's override state) | Low-severity but real inconsistency: a second, undocumented persistence location a future cleanup pass won't know to look for, and no `IsFullyConfigured`/migration-guard coverage the way `AppSettings` fields already get | Add the override setting as a plain field on the existing `AppSettings` model, persisted through the existing `ISettingsStore`/`JsonSettingsStore` path used by everything else — do not invent a second settings surface for one new field |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-------------------|
| Custom-drawn toggle switch has no visible/functional Tab-focus or Space/Enter keyboard activation, unlike the `Button` it replaces | Keyboard-only operation (already a real usage mode for this single-user rig tool — hotkey-triggered toggling is a core feature) silently loses the ability to trigger the toggle from the GUI via keyboard | Explicitly wire `TabStop = true`, a visible focus-cue in `OnPaint` when focused, and handle `Space`/`Enter` in `OnKeyDown`/`ProcessCmdKey` — a plain `Control` subclass gets none of `Button`'s built-in keyboard affordances for free |
| Tile click target is smaller/fussier than the previous full-row `DataGridView` click target (`colAction.Index` cell click covered a wide, obvious column) | Harder-to-hit click target on the redesigned tiles frustrates the exact interaction (quick per-monitor enable/disable) the panel absorption is supposed to make more convenient, not less | Size the tile's clickable region generously (whole tile, not just an icon/label), and rig-test actual click accuracy, not just visual layout, at the DPI scale the rig monitor actually runs at |
| Manual theme override (THEME-09) with no visible current-state indicator in Settings | User sets an override, then can't tell later whether "Dark" is currently active because of their override or because Windows itself is in Dark mode — confusing when they later change Windows' own theme and the app doesn't visibly react (expected, if override is honored — see Pitfall 6) but looks like a bug without an explanation | Settings UI for the override should make the override's current effect legible (e.g., show what the *live OS* theme currently is alongside the override control, not just the override picker in isolation) |

## "Looks Done But Isn't" Checklist

- [ ] **Toggle-switch control:** Often missing live theme-flip re-theming (Pitfall 1) and keyboard activation (UX table) — verify both a live Light↔Dark OS flip while running AND a Tab+Space keyboard-only toggle, not just a mouse click at startup.
- [ ] **Accent-color reads (THEME-07):** Often "looks plausible" (some color renders) without matching Settings > Colors exactly — verify with a color-picker comparison against the live Settings panel, not an eyeball check (Pitfall 4).
- [ ] **Manual override + live-follow composition (THEME-09):** Often works for the "set override once, restart app" case but not the "override set, then OS theme flips while running" case — verify the live-flip-while-override-set scenario specifically (Pitfall 6), since that's the case most likely to have been untested if only manual restarts were used during development.
- [ ] **Tile-grid hotplug refresh after absorption:** Often works while MainForm is visible but silently stops (or was never actually wired) while MainForm is hidden to tray — verify with the window hidden, not just visible (Pitfall 8).
- [ ] **Lease acquisition around tile monitor-mutation actions:** Often present at first implementation, then quietly removed in a later "cleanup" pass once the code lives in the same class as `BtnToggle_Click` — verify with the hotkey-during-confirm-dialog race test (Pitfall 7), which is the only way this specific regression actually shows itself.
- [ ] **SettingsForm layout rework (TableLayoutPanel/FlowLayoutPanel migration) and new owner-drawn controls:** Often look correct at design-time / 100% scale but break at a non-100% Windows display-scale setting, since `RigToggle.App.csproj` sets no explicit `ApplicationHighDpiMode`/manifest and all three forms use `AutoScaleMode.Font`, not `Dpi` — verify at 125%/150% scale on real hardware, not just 100% (Pitfall 9).

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|-----------------|-------------------|
| New control missed by theming pipeline (Pitfall 1) | LOW | Add the missing `ThemeApplier`-equivalent call to both `OnThemeChanged` and `InitializeTrayState` — small, isolated, no architecture change needed once found |
| Accent-color source proves wrong on rig comparison (Pitfall 4) | MEDIUM | Swap the registry key/API source, re-verify against Settings > Colors; if no single source proves reliable, fall back to a coarser "derive a themed accent from the existing light/dark palette instead of the true Windows accent" as a documented, deliberate scope reduction rather than shipping a visibly-wrong color |
| Lease removed during merge, race reintroduced (Pitfall 7) | LOW | Re-add the `BeginExclusiveMonitorAccess()` acquisition at the tile-action call site; this is a small, mechanical fix once the hotkey-during-dialog rig test surfaces it — the cost is almost entirely in *noticing* it, not fixing it |
| Hidden-to-tray hotplug refresh silently not wired (Pitfall 8) | LOW-MEDIUM | Move the `SystemEvents.DisplaySettingsChanged` subscription to MainForm's constructor (app-lifetime), removing any Hide/Show-gated subscribe/unsubscribe if one was added |
| Manual override not honored on live OS flip (Pitfall 6) | MEDIUM | Introduce the single shared "effective theme" resolver and route every existing `IsDark`/`OnThemeChanged` call site through it — more invasive than the other recoveries here because it touches multiple files, but mechanical once designed |
| Owner-drawn geometry breaks at non-100% scale (Pitfall 9) | MEDIUM | Rework the affected `OnPaint` method's hardcoded literals into size/DPI-relative computations; contained to the one control's paint code, but requires a re-pass at multiple scale factors to confirm the fix, not just a single-scale spot-check |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|--------------------|----------------|
| New control missed by theming pipeline | Phase implementing THEME-08 (toggle switch) and the tile-grid work | Live Light↔Dark OS flip with the app running, both normal-start and `--tray`-start paths, watching the new control specifically |
| GraphicsPath seam artifacts in toggle-switch/tile rendering | Phase implementing THEME-08's rendering and the tile visuals | Zoomed screenshot / pixel-level check at shape-overlap boundaries, not a rig glance alone |
| Owner-drawn flicker / Mica-blend mismatch | Phase implementing THEME-08's control and the tile-grid work | Live mouse-hover interaction test on real Windows 11 hardware, not a static screenshot |
| Accent-color source ambiguity | Phase implementing THEME-07 | Color-picker comparison against live Settings > Colors swatch, including a custom accent color and both states of "Show accent color on title bars" |
| Accent-color change notification unreliability | Phase implementing THEME-07 | Multiple accent-color changes in one running session, including a same-color no-op change |
| Manual override not composing correctly with live theme-follow | Phase implementing THEME-09 | Set override, then flip live OS theme while app is running, on all three theming surfaces (MainForm, SettingsForm, absorbed tile/monitor area) |
| Lease double-registration / race reintroduced during Form absorption | MainForm-absorption phase | Hotkey pressed while a tile's `MonitorConfirmDialog` is open, confirming the hotkey toggle is rejected as busy |
| Event-subscription lifecycle mismatch (hide-not-close vs. close-and-reopen) | MainForm-absorption phase | Hotplug event while MainForm is hidden to tray, confirming the tile grid reflects it live |
| DPI/AutoScaleMode.Font pixel-math breakage in new owner-drawn controls and reworked SettingsForm layout | THEME-08 phase and the SettingsForm layout-rework phase | Real hardware at 125%/150% Windows display scale, not just 100% |

## Sources

- `/home/bpivk/moza/src/RigToggle.App/ThemeApplier.cs`, `MainForm.cs`, `MonitorPanelForm.cs`, `MonitorIdentifyOverlay.cs` — read directly (2026-08-09) to ground every Form-absorption and theming-pipeline pitfall in this codebase's actual current implementation, not assumption
- `/home/bpivk/moza/src/RigToggle.Windows/WindowsThemeProvider.cs`, `DwmTitleBar.cs` — read directly to confirm the existing diff-against-last-known-value pattern and the DWM title-bar/Mica mechanism this milestone's new controls sit on top of
- `/home/bpivk/moza/src/RigToggle.IconGen/IconGeometry.cs` — read directly to confirm the exact Phase 13 stroke-then-fill compositing fix this milestone's owner-drawn rendering should reuse
- `/home/bpivk/moza/.planning/PROJECT.md` — Key Decisions table, Phase 12/13 rig-disproven-assumption entries — HIGH confidence, primary source for this project's own documented theming/GDI+ history
- `/home/bpivk/moza/.planning/debug/knowledge-base.md` — confirms this project's established convention of preserving rig-discovered constraints as durable knowledge, and its precedent for "training-data-plausible API turned out rig-false" failures (`Application.SetColorMode`, `DWMWA_USE_IMMERSIVE_DARK_MODE`)
- DWM accent color registry keys and `DwmGetColorizationColor` — WebSearch, MEDIUM confidence (no official Microsoft Learn documentation found describing "AccentColor" vs "ColorizationColor" vs "AccentColorMenu" semantics or precedence; community sources only) — flagged explicitly in Pitfall 4 as needing rig verification, not treated as settled fact
- https://learn.microsoft.com/en-us/windows/win32/dwm/wm-dwmcolorizationcolorchanged — official message documentation (payload format `0xAARRGGBB`), HIGH confidence for the message's shape, MEDIUM confidence for reliability claims (sourced from community reports, not this Microsoft Learn page)
- WM_DWMCOLORIZATIONCOLORCHANGED reliability reports (multiple/missing fires) — WebSearch, MEDIUM confidence, not independently reproduced on this project's rig hardware yet — this is precisely why Pitfall 5 recommends rig-verifying rather than trusting the message alone
- `/home/bpivk/moza/src/RigToggle.App/SettingsForm.Designer.cs`, `MonitorPanelForm.Designer.cs`, `MainForm.Designer.cs` — read directly to confirm current `AutoScaleMode.Font` usage (all three forms) and the absence of `TableLayoutPanel`/`FlowLayoutPanel` in the current SettingsForm (plain `Panel` + `SuspendLayout`/`ResumeLayout` only) — HIGH confidence, grounds the DPI and layout-migration pitfalls in this codebase's actual current state rather than a generic WinForms assumption

---
*Pitfalls research for: WinForms owner-drawn controls, live accent-color theming, Form-lifecycle absorption (Rig Toggle v2.1)*
*Researched: 2026-08-09*
