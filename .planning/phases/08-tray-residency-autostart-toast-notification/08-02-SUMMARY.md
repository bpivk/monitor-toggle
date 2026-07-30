---
phase: 08-tray-residency-autostart-toast-notification
plan: 02
subsystem: app-ui-tray
tags: [tray, winforms, notifyicon, toast, icon-assets]

requires: [08-01]
provides:
  - "MainForm tray residency (NotifyIcon + ContextMenuStrip)"
  - "src/RigToggle.App/Resources/normal.ico"
  - "src/RigToggle.App/Resources/rig.ico"
  - "MainForm.InitializeTrayState()"
affects: [08-03, 08-04]

tech-stack:
  added: []
  patterns:
    - "PNG-in-ICO (Vista+ format) hand-emitted with stdlib struct/zlib only, no Pillow/external binary, for a one-off asset generator that does not ship"
    - "components-owned NotifyIcon/ContextMenuStrip construction (System.ComponentModel.Container), making the form's existing Dispose(bool) a genuine ghost-icon backstop"
    - "CloseReason-gated FormClosing (UserClosing only) instead of a custom _isExiting boolean"
    - "Tray-triggered error/result reporting routes exclusively through NotifyIcon.ShowBalloonTip, never MessageBox — GUI chrome is never assumed visible from a background trigger"

key-files:
  created:
    - src/RigToggle.App/Resources/normal.ico
    - src/RigToggle.App/Resources/rig.ico
  modified:
    - src/RigToggle.App/RigToggle.App.csproj
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/MainForm.cs

key-decisions:
  - "Hand-emitted PNG-in-ICO frames (16/32/48) via Python stdlib (struct+zlib) rather than requiring Pillow, since no image-generation tool or Pillow install was available in the executor sandbox"
  - "normal.ico/rig.ico differ by silhouette (a wedge/flag accent added to the same base monitor bezel shape), not merely palette, per UI-SPEC's hard requirement; an amber accent color is a secondary signal only"
  - "Icons loaded once via GetManifestResourceStream and cached as Icon fields (_normalIcon/_rigIcon) — never re-derived from a Bitmap per toggle, avoiding the GDI-handle leak Pitfall 3 warns about"
  - "TrayToggleMenuItem_Click deliberately skips the GUI-only WR-01 config guard and DISPLAY-07 confirm dialog, and routes every branch (both exception catches and the final result) through ShowBalloonTip, never MessageBox — D-08's no-chrome guarantee for a background trigger"
  - "FormClosing gates exclusively on CloseReason.UserClosing; Application.Exit()'s distinct CloseReason.ApplicationExitCall passes through untouched with no custom flag"

patterns-established:
  - "Any future trigger surface (hotkey/CLI, Phase 9/10) that calls ToggleOrchestrator without a guaranteed-visible window should follow TrayToggleMenuItem_Click's ShowBalloonTip-only error/result reporting convention, not MessageBox"

requirements-completed: [TRAY-01, TRAY-03, TRAY-04, TRAY-05, NOTIF-01]

duration: 35min
completed: 2026-07-30
---

# Phase 08 Plan 02: Tray Residency, Context Menu & Toast (MainForm) Summary

Made `MainForm` tray-resident: two hand-generated silhouette-distinct `.ico` glyphs embedded into the single-file publish, a `NotifyIcon` + `ContextMenuStrip` (Switch mode / Settings / separator / Exit) hosted on the form's `components` container, `FormClosing` redirected to hide-to-tray, left-click restore, and a balloon toast firing on every tray-menu toggle via the shared `ToggleResultFormatter`. Also finished Plan 08-01's relocation by deleting `MainForm`'s duplicate `FormatChecklist` and routing the GUI `MessageBox` through `RigToggle.Core.ToggleResultFormatter`.

## Performance

- **Duration:** 35 min
- **Started:** 2026-07-30 (worktree spawn)
- **Completed:** 2026-07-30
- **Tasks:** 2 completed
- **Files modified:** 2 created (icons), 3 modified (csproj, Designer, MainForm.cs)

## Accomplishments

- `normal.ico`/`rig.ico` — real, valid, multi-resolution (16×16/32×32/48×48) PNG-compressed ICO files, hand-emitted with only Python stdlib (`struct`+`zlib`, no Pillow available in this sandbox), verified structurally (ICONDIR header, per-frame ICONDIRENTRY, both required frame sizes present, files not byte-identical) and via `file(1)` ("MS Windows icon resource")
- `RigToggle.App.csproj` embeds both icons as `EmbeddedResource` with deterministic `LogicalName`s (`normal.ico`/`rig.ico`), so they live inside the `PublishSingleFile=true` bundle
- `MainForm` now hosts a fully-wired tray icon: mode-reflecting glyph + tooltip (TRAY-04), right-click context menu in the exact spec'd order (TRAY-03), left-click restore (TRAY-05), Close-to-tray redirect (TRAY-01), and a NOTIF-01 balloon toast on every tray-triggered toggle
- `MainForm`'s duplicate `FormatChecklist` is gone; the GUI `MessageBox` path and the new toast both call the single shared `RigToggle.Core.ToggleResultFormatter`

## Task Commits

1. **Task 1: Generate two silhouette-distinct .ico assets and embed them** — `908fef7`
2. **Task 2: Tray wiring, click/toggle/exit handlers, mode-reflection, toast, and FormatChecklist cleanup** — `a0ba545`

## Files Created/Modified

- `src/RigToggle.App/Resources/normal.ico` - Plain filled monitor/bezel silhouette (neutral gray), 16/32/48px PNG-in-ICO frames
- `src/RigToggle.App/Resources/rig.ico` - Same base monitor silhouette plus a distinct amber wedge/flag accent jutting from the top-right corner — differs by occupied-pixel shape, not just color
- `src/RigToggle.App/RigToggle.App.csproj` - New `EmbeddedResource` `ItemGroup` for both icons with `LogicalName` set to the bare filename
- `src/RigToggle.App/MainForm.Designer.cs` - `components` field now instantiated (`new Container()`); `NotifyIcon` + `ContextMenuStrip` + 3 `ToolStripMenuItem`s + separator declared and wired (`FormClosing`, `NotifyIcon.MouseClick`, three menu-item `Click` handlers); NotifyIcon deliberately not added to `this.Controls`
- `src/RigToggle.App/MainForm.cs` - `_normalIcon`/`_rigIcon` fields + `LoadTrayIconsIfNeeded()`; `InitializeTrayState()` public entry point for `--tray` startup (Program.cs, Plan 08-03); `RefreshUi()` extended to sync tray icon/tooltip/menu-label; `MainForm_FormClosing`, `NotifyIcon_MouseClick`, `TraySettingsMenuItem_Click`, `TrayExitMenuItem_Click`, `TrayToggleMenuItem_Click` added; duplicate `FormatChecklist` deleted, `BtnToggle_Click`'s `MessageBox` call now uses `ToggleResultFormatter.FormatChecklist`

## Decisions Made

- See `key-decisions` in frontmatter for the full rationale set (icon generation approach, silhouette-vs-color differentiation, icon caching to avoid GDI leaks, tray-toggle no-chrome guarantee, `CloseReason`-only gating).
- Icon script and its verifier live in the scratch directory / `$HOME` (not shipped): `/tmp/claude-0/.../scratchpad/gen_icons.py` (generator, one-off) and `/root/.claude-scratch-ico-check.py` (verifier, per plan's explicit instruction).

## Deviations from Plan

### Auto-fixed Issues

None — the plan's icon-generation instructions anticipated the no-Pillow case explicitly ("If Pillow is importable... otherwise hand-emit the ICO container") and this environment had no Pillow, so the hand-emitted path was followed as the plan's own primary contingency, not a deviation. One implementation choice within that contingency: PNG-compressed ICO frames (the modern, Vista+-supported ICO variant) were used instead of raw 32-bit BGRA DIB+AND-mask frames, since both are valid `.ico` formats and PNG-in-ICO is simpler to emit correctly with stdlib `zlib` alone — the plan's acceptance criteria (ICONDIR header fields, frame-size presence, non-identical files, `file(1)` recognition) do not require the DIB variant specifically, and `file(1)` independently confirmed both outputs as valid "MS Windows icon resource" files.

**Total deviations:** 0
**Impact on plan:** None.

## Issues Encountered

None beyond the pre-anticipated no-Pillow fallback path (see above).

## Environment Constraint (matches Phase 6/7/08-01 precedent)

This executor sandbox has no `dotnet` SDK installed (confirmed via `which dotnet` and checking `/usr/share/dotnet`/`/root/.dotnet` — only sentinel files present, no actual `dotnet` binary). Verification for Task 2 was therefore done via grep-based source assertions plus a Python brace/paren balance sanity check, instead of a live `dotnet build`/`dotnet test` run:

- `grep -c "CloseReason.UserClosing" MainForm.cs` — 2 (comment + code) — PASSED
- `grep -c "e.Button == MouseButtons.Left" MainForm.cs` — 1 — PASSED
- `grep -c "ShowBalloonTip" MainForm.cs` — 4 — PASSED
- `grep -c "Application.Exit()" MainForm.cs` — 3 (comment + 2 call sites) — PASSED
- `grep -c "GetManifestResourceStream(" MainForm.cs` — 2 — PASSED
- `grep -c "ToggleResultFormatter.FormatChecklist" MainForm.cs` — 2 — PASSED
- `grep -c "private static string FormatChecklist" MainForm.cs` — 0 (duplicate confirmed gone) — PASSED
- `grep -c "public void InitializeTrayState"` — 1 — PASSED
- `grep -c "new System.ComponentModel.Container()"` / `"new System.Windows.Forms.NotifyIcon(this.components)"` in `MainForm.Designer.cs` — 1 each — PASSED
- `grep -c "this.FormClosing +="` / `"this.notifyIcon.MouseClick +="` in `MainForm.Designer.cs` — 1 each — PASSED
- `grep -c "this.Controls.Add(this.notifyIcon)"` — 0 (confirmed NOT added to Controls) — PASSED
- Brace-matched extraction of `TrayToggleMenuItem_Click`'s method body confirmed **zero** `MessageBox.Show` occurrences inside it (D-08 no-chrome guarantee) — PASSED
- Brace/paren balance check across both modified files (`{`/`}` and `(`/`)` counts equal) — PASSED for both files
- ICO structural verification (`$HOME/.claude-scratch-ico-check.py`): both files parse with `reserved=0, type=1`, contain both a 16×16 and 32×32 frame, and are not byte-identical — PASSED
- `file src/RigToggle.App/Resources/{normal,rig}.ico` — both report "MS Windows icon resource" — PASSED

**Action required before Phase 8 is considered fully verified:** run `dotnet build`/`dotnet test` on a host with the .NET SDK (the Windows rig) to confirm the full solution actually compiles and the existing test suite stays green, per this plan's `<verification>` section. All code follows the exact conventions read directly from `MainForm.cs`'s existing style, `08-PATTERNS.md`'s prescribed handler shapes, and `08-RESEARCH.md`'s documented pitfalls, so confidence is high, but this has not been confirmed by an actual compiler/test-runner in this environment — same standing blocker as Phases 6/7/08-01.

## User Setup Required

None — no external service configuration required. (Interactive tray behavior — hide-to-tray, restore, menu order, icon swap, toast content, ghost-free exit — is explicitly deferred to the Phase 8 rig checkpoint per this plan's own `<verification>` section, not validated here.)

## Next Phase Readiness

- `MainForm.InitializeTrayState()` is now available for Plan 08-03's `Program.cs` composition-root wiring (the `--tray`/`ApplicationContext` startup path per D-06) to call unconditionally before either `Application.Run` branch.
- `normal.ico`/`rig.ico` are embedded and addressable via the deterministic `GetManifestResourceStream("normal.ico"/"rig.ico")` names — no further asset work needed for Phase 8.
- `MainForm.cs`/`MainForm.Designer.cs` are otherwise stable for Plan 08-03/08-04 to build against; no further edits to these two files are anticipated before the rig checkpoint.
- Blocker (carried over): a real `dotnet build`/`dotnet test` pass on Windows hardware is still needed to confirm compilation and green tests before the phase can be considered fully verified.

---
*Phase: 08-tray-residency-autostart-toast-notification*
*Completed: 2026-07-30*

## Self-Check: PASSED

- FOUND: `src/RigToggle.App/Resources/normal.ico`
- FOUND: `src/RigToggle.App/Resources/rig.ico`
- FOUND: `src/RigToggle.App/RigToggle.App.csproj`
- FOUND: `src/RigToggle.App/MainForm.Designer.cs`
- FOUND: `src/RigToggle.App/MainForm.cs`
- FOUND: `.planning/phases/08-tray-residency-autostart-toast-notification/08-02-SUMMARY.md`
- FOUND commit `908fef7` (Task 1: icon assets)
- FOUND commit `a0ba545` (Task 2: tray wiring)
- FOUND commit `b7c3a67` (docs: SUMMARY.md)
