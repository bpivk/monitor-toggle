# Project Research Summary

**Project:** Rig Toggle — v2.1 Milestone (Modern UI Redesign & Theme Backlog)
**Domain:** Windows desktop GUI utility (display/audio/process control automation) — UI-redesign milestone on an already-shipped, rig-validated four-project .NET 10 WinForms solution
**Researched:** 2026-08-09
**Confidence:** HIGH

## Executive Summary

v2.1 is a pure UI/UX redesign layered on top of a functionally complete, rig-validated v2.0 product — no monitor/audio/process-control logic changes, no new NuGet dependencies, and every one of the milestone's six target-feature groups (monitor-tile dashboard absorbing `MonitorPanelForm`, SettingsForm layout pass, custom toggle-switch control, accent-color theming, manual light/dark override) is achievable with WinForms/GDI+/P-Invoke techniques the codebase has already proven at least once (owner-drawn status dots, procedurally-generated icons, `dwmapi.dll` P-Invoke for title-bar attributes, an existing live-theme-follow pipeline). All four research passes converge on the same conclusion: extend existing patterns (`ThemeApplier.Theme*` methods, the `IThemeProvider`/`WindowsThemeProvider` event-driven diff pattern, `MonitorPanelForm`'s dumb-presentational-control-plus-owner-mutates pattern) rather than introduce anything new — this is the project's third consecutive milestone with a zero-new-dependency stack conclusion.

The recommended approach: (1) build the monitor-tile dashboard first as a standalone, unwired control, then wire it into `MainForm` porting `MonitorPanelForm`'s mutation/lease/hotplug logic verbatim before deleting the standalone panel and its two entry points — sequencing the replacement to be proven working before the original is removed, since a capability gap here (no way to enable/disable a single monitor) would be a real regression; (2) build THEME-08 (toggle switch) as its own isolated control using the same stroke-then-fill GDI+ compositing this codebase already validated in Phase 13, swapped in for the plain button only after the tile-dashboard work is stable, to avoid compounding two risky UI changes in one uncommitted diff; (3) build THEME-07 (accent color) any time before or alongside THEME-08 if the switch's "on" state should be accent-tinted, treating "which registry key/API is really the accent color" as an open question requiring rig verification against Settings > Colors, not a settled fact — no official Microsoft documentation confirms this; (4) build THEME-09 (manual override) last among the theme work, and use the `MonitorPanelForm` deletion as the natural moment to collapse this codebase's three independently-mirrored `IsDark`/`IsDarkTheme` properties into one shared "effective theme" resolver rather than perpetuating a fourth copy; (5) the SettingsForm layout pass is fully independent of everything else and can slot anywhere in the sequence.

The dominant risk theme across all four research files is **silent divergence between visually-similar-but-lifecycle-different code paths** being merged without being reconciled: a new custom-drawn control silently falling outside the hand-maintained theming pipeline (this codebase's own doc comment says that pipeline is "deliberately NOT a recursive Controls-tree walk"); `MonitorPanelForm`'s exclusive-access lease looking redundant once its logic lives in the same class as `BtnToggle_Click` and getting "simplified" away, reopening a hotkey-during-confirm-dialog race Phase 17 specifically built the lease to close; and `MonitorPanelForm`'s closable-and-reopenable FormClosed-unsubscribe pattern being copied onto `MainForm`'s actual hide-not-close, app-lifetime pattern, either becoming harmless dead code or an active hotplug-refresh regression depending on exactly how it's copied. None of these require new technology to avoid — every one is caught only by a deliberate, named rig-verification checkpoint (live theme flip while running, hotkey pressed mid-confirm-dialog, hidden-to-tray hotplug test), not a static/compile-time check or a rig glance at startup.

## Key Findings

### Recommended Stack

Zero new NuGet packages. All four v2.1 targets are pure WinForms/GDI+/P-Invoke extensions of existing patterns:

**Core technologies (extensions to already-shipped stack):**
- `DwmGetColorizationColor` (dwmapi.dll P-Invoke) — reads the live Windows accent/colorization color — a second export off the exact same DLL already used for `DwmSetWindowAttribute`, no new dependency surface
- `WM_DWMCOLORIZATIONCOLORCHANGED` message interception via a message-only `NativeWindow` (or `MainForm.WndProc` extension) — live accent-color-change notification, following the same "intercept a window message" pattern already used for `WM_HOTKEY`
- Hand-rolled `ToggleSwitch : Control` with owner-draw `OnPaint` (`GraphicsPath` rounded-rect track + `FillEllipse` thumb, `SmoothingMode.AntiAlias`) — same GDI+ discipline already proven in `MonitorPanelForm.CreateStatusDot` and `RigToggle.IconGen`
- Hand-rolled `MonitorTile : Control`/`UserControl` hosted in a `FlowLayoutPanel` (`WrapContents=true`) — reuses `RigToggle.IconGen`'s existing glyph-drawing code, same approach already scoped for the now-retiring `MonitorPanelForm`
- New nullable `AppTheme? ThemeOverride` on the existing `AppSettings` model, persisted via the existing `System.Text.Json`/`ISettingsStore` path — zero new serialization capability needed

**Critical version/compatibility notes:** `DwmGetColorizationColor`/`WM_DWMCOLORIZATIONCOLORCHANGED` are Windows Vista+ APIs, confirmed still current with no deprecation notice, already proven working on this project's real Windows 11 rig hardware via the sibling `DwmSetWindowAttribute` call. `ControlStyles.ApplyThemingImplicitly` (non-experimental as of .NET 10 GA) is not needed for either new custom control since both derive straight from `Control` and paint 100% of their own pixels — no native theming pipeline to opt in/out of.

### Expected Features

This is a fixed-scope milestone — all target-feature groups are already committed per `PROJECT.md`, so feature research reframes as sequencing risk rather than a trim decision.

**Must have (table stakes — carryover behaviors that must not regress):**
- One tile per monitor (icon+number, status via icon not text), click-to-toggle, `SkipMonitorConfirmation` gate preserved, Identify relocated near tiles, live hotplug refresh, empty-state handling, primary/OS-disabled visual distinction — all direct ports of `MonitorPanelForm`'s already-shipped PANEL-01..05 behavior into `MainForm`
- Both `MonitorPanelForm` entry points (MainForm button, tray menu item) removed together, not just one — a dangling tray item pointing at a retired form is an immediately noticeable regression
- SettingsForm: no overlapping controls, logical grouping preserved (existing GroupBox boundaries are likely already correct — fix is spacing/sizing, not regrouping)
- Manual theme override: exactly a System/Light/Dark three-way choice with System as default (near-universal convention — GitHub Desktop, Windows Terminal, Docker Desktop, Teams), taking effect immediately without restart, and NOT silently overridden by a live OS theme flip once locked to Light/Dark
- Toggle switch reads as two-state without relying on color alone (track+thumb position difference), consistent with this project's existing Phase 13 colorblind-safe convention

**Should have (differentiators):**
- Tile hover/pressed visual feedback (elevation/border highlight)
- Accent-color highlight scoped narrowly to the toggle switch's "on" state and small interactive highlights — NOT a broad recolor of neutral surfaces (Windows 11 itself uses accent color sparingly)
- Live accent-color follow while the app is running (not read-once-at-startup)
- Keyboard operability of tiles (Tab focus, Space/Enter) — flag for roadmap to explicitly accept or defer, don't let it silently vanish in the grid-to-tile migration

**Defer (explicitly out of scope this milestone):**
- Toggle-switch slide animation (polish, defer if time-constrained)
- Drag-to-rearrange tiles to match physical desk layout — explicitly re-rejected (same topology-editing scope already rejected for `MonitorPanelForm`)
- A second, tile-specific confirmation dialog or fast-path bypassing `SkipMonitorConfirmation` — would fragment the safety gate DISPLAY-12 deliberately centralized
- Four-option theme setting (e.g. time-of-day auto-switching) — explicit scope creep beyond THEME-09's "manual override" framing
- SettingsForm tab/wizard restructuring — bigger, riskier change than the requested "layout pass"

### Architecture Approach

v2.1 touches only `RigToggle.App` plus a small, deliberately isolated amount of net-new `RigToggle.Core` (a settings-driven decorator) and `RigToggle.Windows` (an accent-color reader) — it does not touch `ToggleOrchestrator`, `WindowsMonitorController`'s mutation methods, or `WindowsThemeProvider`'s existing OS-signal behavior. The two load-bearing architectural patterns are: (1) dumb, presentational child controls (`MonitorTile`) that never call `IMonitorController` directly, only raise events — `MainForm` remains the sole caller of the controller/orchestrator, preserving DISPLAY-12's single-shared-guard property; (2) a `Decorator` (`OverridableThemeProvider : IThemeProvider`) wrapping `WindowsThemeProvider` to compose the manual override with the live OS signal, with zero changes to the underlying OS-signal reader — this is what keeps THEME-01..06 regression risk near zero.

**Major components:**
1. `MonitorTile` (new `UserControl`) — renders one monitor's icon/number/status, raises `ActionRequested`, owns no controller reference
2. `ToggleSwitch` (new `Control`) — custom-drawn on/off control replacing `btnToggle`, theme- and accent-aware paint
3. `OverridableThemeProvider` (new, `RigToggle.Core`) — decorator resolving effective theme = override ?? live OS signal, re-raises `ThemeChanged` on genuine flips
4. `MainForm` (modified) — absorbs `MonitorPanelForm`'s enumeration/mutation/hotplug/Identify logic; `WindowsThemeProvider`/`NativeMethods` (modified) — adds accent-color read via the existing `SystemEvents.UserPreferenceChanged` handler, not a second subscription

**Recommended build order:** MonitorTile standalone/unwired → tile-strip read-only population → port mutation logic verbatim from `MonitorPanelForm` → port hotplug refresh + Identify → delete `MonitorPanelForm` last → THEME-07 (accent, any time) → THEME-08 (toggle switch, after tile work is stable) → THEME-09 (override, last, decorates whatever `IThemeProvider` looks like after THEME-07).

### Critical Pitfalls

1. **New custom-drawn controls silently fall outside the hand-maintained theming pipeline** — `ThemeApplier`'s pipeline is deliberately NOT a recursive Controls-tree walk; a new control not added to both `OnThemeChanged` AND `InitializeTrayState()` (the `--tray`-safe-startup path) will render correctly at startup but freeze in that mode forever. Avoid by treating "add the new control to both existing call sites" as an acceptance criterion, verified via a live Light↔Dark OS flip while running, in both normal-start and tray-start paths.
2. **GraphicsPath seam artifacts** — combining the toggle switch's track+thumb (or a tile's icon+border) into one `GraphicsPath` before `DrawPath` reproduces the exact seam-artifact bug Phase 13 already hit and fixed. Avoid by reusing the validated stroke-then-fill compositing (separate `GraphicsPath`/`FillPath` calls per shape, back-to-front), verified via zoomed screenshot at overlap boundaries, not a rig glance.
3. **Accent-color source ambiguity** — no single official Win32 API documents "the" Settings > Colors accent swatch; `DwmGetColorizationColor`/`ColorizationColor`/`AccentColor`/`AccentColorMenu` are at least three distinct, possibly-divergent registry/API sources. Avoid by rig-verifying the chosen source with a pixel-level color-picker comparison against the live Settings > Colors panel, including a custom accent color and both states of "Show accent color on title bars."
4. **Lease/race reintroduction during Form absorption** — `MonitorPanelForm`'s explicit `BeginExclusiveMonitorAccess()` lease (acquired before `ShowDialog()`'s nested message pump can dispatch a concurrent `WM_HOTKEY`) looks redundant once tile-click logic lives in the same class as `BtnToggle_Click`, inviting a "cleanup" removal that reopens the exact race Phase 17 built the lease to close. Avoid by porting the lease structure verbatim with an explanatory comment, verified via a hotkey-pressed-during-open-confirm-dialog rig test.
5. **Event-subscription lifecycle mismatch** — copying `MonitorPanelForm`'s subscribe-in-constructor/unsubscribe-in-FormClosed pattern onto `MainForm` (which is hidden, not closed, during normal tray-resident operation) either becomes dead-but-harmless code or an active hotplug-refresh regression depending on exactly how it's copied. Avoid by deciding explicitly whether hidden-to-tray hotplug refresh is in scope, subscribing once at construction for app lifetime if so, verified by unplugging/replugging a monitor while MainForm is hidden.
6. **DPI/AutoScaleMode.Font pixel-math breakage** — hardcoded pixel literals in new `OnPaint` geometry (thumb radius, tile layout) are invisible to `AutoScaleMode.Font`'s control-bounds scaling and will look correct only at the one scale factor tested. Avoid by deriving all paint-time geometry from `ClientSize`/`Font.Height`/`DeviceDpi`, verified at 125%/150% Windows display scale on real hardware (this build environment cannot exercise Windows display scaling at all).

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase A: Monitor-Tile Dashboard + MonitorPanelForm Retirement
**Rationale:** Highest-complexity, highest-regression-risk item (absorbs 5 existing behaviors: status display, click-to-toggle, confirmation gate, Identify, hotplug refresh) and the one other phases (toggle-switch repositioning, Settings de-emphasis) are laid out around. Research's own recommended build order sequences this as: standalone tile control → read-only population in MainForm → mutation logic ported verbatim (lease/confirm-dialog/DISPLAY-12 guard) → hotplug + Identify ported → MonitorPanelForm deleted last, only once the replacement is proven.
**Delivers:** MainForm as the monitor-tile dashboard; `MonitorPanelForm` and both its entry points removed.
**Addresses:** Table-stakes tile features (PANEL-01..05 carryover), mode-toggle repositioning below the tile row, Settings entry point de-emphasized.
**Avoids:** Pitfall 7 (lease/race reintroduction), Pitfall 8 (event-subscription lifecycle mismatch), Pitfall 2 (GraphicsPath seam artifacts in tile rendering), Pitfall 9 (DPI pixel-math in tile layout).

### Phase B: Custom Toggle-Switch Control (THEME-08)
**Rationale:** Replaces the same button being repositioned in Phase A — building the switch against the button's pre-move position and then moving it (or vice versa) is redundant churn, so this should land in the same phase or immediately adjacent to the tile-dashboard/repositioning work, after the dashboard itself is stable (to avoid compounding two risky UI changes in one uncommitted diff).
**Delivers:** `ToggleSwitch : Control` replacing `btnToggle`, theme-aware, keyboard-operable (Tab/Space/Enter).
**Uses:** Stroke-then-fill GDI+ compositing (Phase 13 precedent), `ControlStyles.OptimizedDoubleBuffer`/`AllPaintingInWmPaint`/`UserPaint` for flicker-free repaint.
**Implements:** New `ThemeApplier.ThemeToggleSwitch` method following the existing five-method pattern.
**Avoids:** Pitfall 1 (theming pipeline miss), Pitfall 2 (seam artifacts), Pitfall 3 (flicker/Mica-blend mismatch), the UX pitfall of losing keyboard activation that `Button` provided for free.

### Phase C: Accent-Color Reading + Live-Change Detection (THEME-07)
**Rationale:** No dependency on the tile-dashboard work; can build any time, but should land before or alongside Phase B if the toggle switch's "on" state is meant to be accent-tinted (a Should-Have differentiator). Independent of THEME-09's coexistence question — purely additive.
**Delivers:** `IAccentColorProvider`/`WindowsAccentColorProvider` (or an extension to `WindowsThemeProvider`'s existing `SystemEvents.UserPreferenceChanged` handler) reading `DwmGetColorizationColor`, diffed and re-raised as `AccentColorChanged`.
**Uses:** `dwmapi.dll` P-Invoke sibling to `DwmSetWindowAttribute`; `WM_DWMCOLORIZATIONCOLORCHANGED` or reuse of the existing `SystemEvents` subscription.
**Avoids:** Pitfall 4 (accent-color source ambiguity — needs the heaviest rig verification of any item this milestone, per Pitfalls research), Pitfall 5 (unreliable change notification).

### Phase D: Manual Light/Dark Override (THEME-09)
**Rationale:** Sequenced last among the theme work — decorates whatever `IThemeProvider` looks like after Phase C, and per its own scoping overrides `CurrentTheme` only (passing `AccentColor`/`AccentColorChanged` through untouched), so it has no hard ordering dependency on Phase C beyond "the interface exists." This is also the natural, already-scheduled moment (MonitorPanelForm is being deleted this same milestone) to collapse the codebase's three independently-mirrored `IsDark`/`IsDarkTheme` properties into one shared resolver rather than adding a fourth copy.
**Delivers:** `AppTheme? ThemeOverride` setting, `OverridableThemeProvider` decorator, a System/Light/Dark radio group in SettingsForm, one collapsed "effective theme" resolver used everywhere.
**Implements:** Decorator pattern over `IThemeProvider`, zero changes to `WindowsThemeProvider`.
**Avoids:** Pitfall 6 (override not composing correctly with live theme-follow — the riskiest theme item per Features research, "do not treat as just another settings checkbox").

### Phase E: SettingsForm Layout Pass
**Rationale:** Fully independent of every other phase — touches only existing controls' `Location`/`Size`/`Anchor`/`Dock` and `GroupBox`/`Panel` structure, zero `AppSettings` model changes (aside from Phase D's new radio group, which can slot into this pass or its own). Can be scheduled anywhere in the sequence, including first (directly fixes a named, standalone user complaint) or last (mop-up).
**Delivers:** No overlapping controls at default size, logically grouped sections, `TableLayoutPanel`/`FlowLayoutPanel` migration if needed.
**Avoids:** Pitfall 9 (DPI breakage in the layout migration — must be checked at 125%/150% scale, not just design-time 100%).

### Phase Ordering Rationale

- Phase A must precede the deletion half of its own scope (MonitorPanelForm retirement) — sequence the replacement to be proven working before the original is removed, per both Features and Architecture research's explicit warning against a capability-gap regression.
- Phase B (toggle switch) and Phase A's mode-toggle repositioning are the same button — Architecture research explicitly flags this as redundant-churn risk if split too far apart.
- Phase C (accent color) is architecturally independent of A/B/D but has a soft ordering preference before B if the switch is meant to be accent-tinted, and before D since D only touches the light/dark axis, not accent.
- Phase D last among theme work because it decorates the interface Phase C may extend, and because collapsing the three `IsDark` copies is naturally timed with MonitorPanelForm's deletion in Phase A.
- Phase E has no dependency edges to any other phase and is the natural filler/parallel-track phase.

### Research Flags

Phases likely needing deeper research during planning (`/gsd:plan-phase --research-phase <N>`):
- **Phase C (accent color):** No official Microsoft documentation exists for "which registry key/API is the accent color" — Pitfalls research explicitly flags this as needing the heaviest rig verification of anything in this milestone, and the live-notification message's reliability is only community-corroborated, not Microsoft-confirmed.
- **Phase D (manual override):** The "collapse three IsDark properties into one resolver" refactor touches multiple files simultaneously with MonitorPanelForm's deletion — worth a closer look during planning to sequence the refactor correctly relative to the deletion.

Phases with standard, well-documented patterns (skip research-phase):
- **Phase A:** Every sub-behavior (status dots, click-to-toggle, confirm dialog, Identify, hotplug) has a directly portable existing implementation in `MonitorPanelForm.cs` — this is a port-and-adapt, not new-territory work.
- **Phase B:** Stroke-then-fill GDI+ compositing and owner-draw control styles are both already proven in this codebase (Phase 12/13 precedent).
- **Phase E:** Pure layout/spacing work against existing controls, no new logic.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Every API verified against current Microsoft Learn documentation fetched directly this session, cross-checked against this repo's own existing source files, not training-data recall |
| Features | MEDIUM-HIGH | Dashboard/tile conventions verified against official Microsoft docs (Settings > Display) plus multiple independent competitor sources (DisplayFusion, MultiMonitorTool, iCUE, Synapse); theme-override three-way pattern verified against two officially-documented named apps (GitHub Desktop, Windows Terminal) |
| Architecture | HIGH | Based on direct reading of the current `src/` implementation across all affected files, not assumption; the one net-new external API (`DwmGetColorizationColor`) is MEDIUM-HIGH (official docs exist but reliability claims are community-sourced) |
| Pitfalls | MEDIUM-HIGH | Grounded in this codebase's own rig-disproven history (Phase 12 theming, Phase 13 GDI+ seams) for the majority of pitfalls; accent-color-specific pitfalls (4, 5) are explicitly flagged LOW/MEDIUM since only WebSearch-level verification was available and must be rig-confirmed |

**Overall confidence:** HIGH

### Gaps to Address

- **Accent-color source of truth (Phase C):** No official Microsoft documentation names which of `ColorizationColor`/`DwmGetColorizationColor`/`AccentColor`/`AccentColorMenu` matches what Settings > Colors displays — must be resolved via rig comparison (color-picker tool, not eyeballing) during Phase C, with a documented fallback ("derive a themed accent from the existing light/dark palette instead") if no source proves reliable.
- **`WM_DWMCOLORIZATIONCOLORCHANGED` reliability:** Community sources report inconsistent firing (multiple fires for one change, or no fire at all on some Windows versions) — Phase C should budget multiple rig-verification rounds (repeated accent changes in one session, including a same-color no-op) before committing to this as the sole live-update mechanism, with `SystemEvents.UserPreferenceChanged` diffing as the proven fallback pattern.
- **Non-100%-scale rig verification:** This build environment has no Windows GUI and cannot exercise Windows display scaling at all — Phases A, B, and E all carry DPI/AutoScaleMode risk (Pitfall 9) that can only be verified on real rig hardware at 125%/150% scale, not during development.

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmgetcolorizationcolor — exact signature, `0xAARRGGBB` format, alpha-is-blend-value remark
- https://learn.microsoft.com/en-us/windows/win32/dwm/wm-dwmcolorizationcolorchanged — message semantics, delivery via WindowProc
- https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100 — .NET 10 dark-mode/`ApplyThemingImplicitly` non-experimental status
- https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/overriding-the-onpaint-method — official custom-painting guidance
- https://learn.microsoft.com/en-us/windows/terminal/customize-settings/themes — Windows Terminal's System/Light/Dark pattern
- Direct reading of current repo source (this session): `ThemeApplier.cs`, `MainForm.cs`, `MonitorPanelForm.cs`, `WindowsThemeProvider.cs`, `DwmTitleBar.cs`, `NativeMethods.cs`, `MonitorIdentifyOverlay.cs`, `SettingsForm.cs`/`.Designer.cs`, `IconGeometry.cs`, `Program.cs`, `AppSettings.cs`, `IThemeProvider.cs`, `IMonitorController.cs`, `ToggleOrchestrator.cs` — used across all four research files to ground findings in actual current implementation

### Secondary (MEDIUM confidence)
- https://support.microsoft.com/en-us/windows/hardware/display-graphics/how-to-use-multiple-monitors-in-windows — Windows Settings > Display numbered-tile/Identify pattern
- DisplayFusion, NirSoft MultiMonitorTool, Corsair iCUE, Razer Synapse dashboard descriptions (WebSearch, cross-referenced) — competitor tile/dashboard conventions
- GitHub Desktop, Docker Desktop, Microsoft 365/Teams theme documentation — System/Light/Dark convention corroboration

### Tertiary (LOW confidence, needs rig validation)
- DWM accent-color registry keys (`ColorizationColor` vs `AccentColor` vs `AccentColorMenu`) — no official Microsoft documentation found for precedence/semantics; community sources only
- `WM_DWMCOLORIZATIONCOLORCHANGED` firing reliability — community reports of multiple/missing fires, not independently reproduced on this project's rig hardware yet

---
*Research completed: 2026-08-09*
*Ready for roadmap: yes*
