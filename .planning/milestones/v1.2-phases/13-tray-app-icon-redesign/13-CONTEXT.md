# Phase 13: Tray & App Icon Redesign - Context

**Gathered:** 2026-08-03
**Status:** Ready for planning

<domain>
## Phase Boundary

The rig-mode and normal-mode tray icons, plus the .exe/taskbar icon, get a genuinely distinct, legible, DPI-sharp redesign to replace the current functional-but-plain monitor-glyph pair (which currently differs only by a small color-accented notch — insufficient to tell modes apart by shape alone, especially at real 16×16 tray size). Does not touch any toggle/monitor/audio/tray-behavior logic, MainForm/SettingsForm theming (Phase 12, already shipped), or the README (Phase 14) — this phase is icon artwork and its embedding only.

</domain>

<decisions>
## Implementation Decisions

### Icon Motif
- **D-01:** Normal mode = desktop monitor silhouette. Rig mode = steering wheel silhouette (Moza-style). Chosen specifically for strong shape contrast that reads correctly at 16×16 without relying on color, and because it maps directly onto the user's actual sim-racing setup (desk monitor vs. Moza wheel/pedals rig) rather than an abstract on/off variation of the same glyph. This replaces the current pair's approach (same monitor glyph, differentiated only by a small orange notch), which does not satisfy ICON-01.

### Visual Style
- **D-02:** Flat, filled Fluent-style icons — solid shapes, minimal internal detail, consistent with native Windows 11 icons (File Explorer, Settings) and with the flat/modern control styling Phase 12 already established for the app's windows. Not outline/line-art.

### Icon Creation Method
- **D-03:** Icons are procedurally drawn in code (GDI+/System.Drawing), the same general approach as the current `normal.ico`/`rig.ico` assets — no external design tool, no new asset pipeline, no image-generation step. The planner/researcher should investigate the concrete drawing approach (e.g., a build-time generation script vs. hand-authored `.ico` files checked into `Resources/`) as an implementation detail, but the icons themselves are code-drawn geometry, not sourced artwork.

### Color Palette (Tray Icons)
- **D-04:** Both tray icons (`normal.ico`, `rig.ico`) are monochrome silhouettes — following the standard Windows system-tray-icon convention (most native tray icons are monochrome so they render consistently against any taskbar/theme). No per-mode accent color; the wheel-vs-monitor silhouette (D-01) is the sole differentiator, satisfying ICON-01's "shape alone, not color" requirement directly rather than as a secondary cue.

### Contrast Strategy (Tray Icons)
- **D-05:** Each tray icon silhouette includes a thin outline/stroke in the opposite tone around the shape (matching how Windows' own tray icons achieve self-contained contrast) — this is the ICON-02 mechanism: one asset per mode that stays legible on both light and dark taskbars, with no theme-detection or asset-swapping logic needed (consistent with REQUIREMENTS.md's existing decision to reject 4-asset theme-swap variants).

### Exe/Taskbar Icon
- **D-06:** The static `.exe`/taskbar icon (ICON-04) reuses the normal-mode monitor motif (D-01) at a larger size — the app launches in normal mode by default, so the taskbar icon matches what the user sees at rest. Not a combined/neutral third design, not the rig-mode wheel.
- **D-07:** Unlike the two monochrome tray icons, the `.exe`/taskbar icon gets a color treatment (e.g. dark-gray body with a subtle accent) — following the Windows convention that taskbar/app icons are typically full-color even when a notification-tray icon from the same app is monochrome (e.g. VS Code's colorful taskbar icon vs. its plain tray glyphs). Still the same monitor silhouette/motif per ICON-04's "reuses the same artwork" requirement — only the color treatment differs by context (tray vs. taskbar), not the shape.

### Claude's Discretion
- Exact stroke width, corner radius, and geometric proportions of the monitor and steering-wheel silhouettes — left to research/planning to determine what actually renders legibly at 16px, tested against the multi-resolution requirement (16/20/24/32px minimum, ICON-03).
- Exact color values (grays, accent color) for the `.exe`/taskbar icon's color treatment.
- Whether icon generation happens via a one-time script producing checked-in `.ico` files, or a build-time step — an implementation detail, not a vision decision.
- File/resource naming and embedding mechanism (current code uses `assembly.GetManifestResourceStream("normal.ico"/"rig.ico")` via `LogicalName` embedded resources in `RigToggle.App.csproj` — reuse or evolve this pattern as appropriate).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & scope decisions
- `.planning/REQUIREMENTS.md` (ICON-01 through ICON-04, plus the "Out of Scope" table entries for color-only differentiation and theme-swap variants — both already decided at project level, not re-litigated in this discussion)
- `.planning/ROADMAP.md` (Phase 13 section — goal, success criteria, "UI hint: yes")

### Existing icon assets and embedding code (current state, being replaced)
- `src/RigToggle.App/Resources/normal.ico` — current normal-mode icon (monitor glyph, gray, 16/32/48px)
- `src/RigToggle.App/Resources/rig.ico` — current rig-mode icon (same monitor glyph + small orange notch, 16/32/48px)
- `src/RigToggle.App/RigToggle.App.csproj` — `EmbeddedResource` items with `LogicalName` for both `.ico` files (tray glyphs embedded in the single-file publish, not Content/CopyToOutput)
- `src/RigToggle.App/MainForm.cs` (~line 239-248) — runtime icon loading via `assembly.GetManifestResourceStream("normal.ico"/"rig.ico")`, assigned to the `NotifyIcon`

No external specs beyond REQUIREMENTS.md/ROADMAP.md — this is a self-contained visual-design phase.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainForm.Designer.cs`'s `notifyIcon` (`System.Windows.Forms.NotifyIcon`) — existing tray icon host, icon swapped at runtime between the two embedded `.ico` resources on mode toggle.
- Embedded-resource + `LogicalName` pattern already established for icon assets in `RigToggle.App.csproj` — the same mechanism should hold for the redesigned icons.

### Established Patterns
- Icons are embedded resources (not loose files copied to output), addressed by `LogicalName` via `Assembly.GetManifestResourceStream` — keeps them inside the single-file self-contained publish per CLAUDE.md's packaging constraint.

### Integration Points
- `MainForm.cs` is the sole runtime consumer of both `.ico` resources (tray icon assignment).
- The `.exe`/taskbar icon is a separate embedding path (typically the project's `<ApplicationIcon>` MSBuild property, not the `NotifyIcon` runtime path) — planner should confirm current `RigToggle.App.csproj` state for this (may not be set yet, since the current pair is tray-only).

</code_context>

<specifics>
## Specific Ideas

- Rig-mode icon should evoke the user's actual Moza-style steering wheel (the physical sim-racing wheel/pedal rig this whole app manages), not a generic/abstract "on" state.
- Normal-mode icon should read as an ordinary desktop monitor — the user's everyday desk setup.
- Reference for taskbar-vs-tray color convention: VS Code's colorful taskbar icon vs. its plain/monochrome tray notification glyphs — same idea should apply here (monochrome tray pair, color-treated exe icon, same motif).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 13-tray-app-icon-redesign*
*Context gathered: 2026-08-03*
