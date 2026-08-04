# Phase 12: Theme Infrastructure & Live Theme-Following - Context

**Gathered:** 2026-08-02
**Status:** Ready for planning

<domain>
## Phase Boundary

MainForm and SettingsForm visually match the current Windows light/dark theme — title bar, every control (including the multi-monitor `DataGridView`), and flat/modern button-panel styling — and stay in sync live if the user changes the Windows theme setting while the app is running, including while it's hidden in the tray under `--tray` startup. On Windows 11, window corners are rounded with a Mica backdrop; on Windows 10 or when an API is unavailable, the app degrades gracefully (no crash, no visual glitch — best-effort/no-op, not "Windows 11 required"). Does not touch tray icon artwork (Phase 13), README (Phase 14), or add any new user-facing settings (no manual theme-override toggle — THEME-09 is explicitly deferred to v2). This phase only makes the two existing forms *look* modern/theme-aware; it does not change any toggle/monitor/audio/tray/hotkey behavior.

</domain>

<decisions>
## Implementation Decisions

### MessageBox Dialogs
- **D-01:** MainForm's 4 existing `MessageBox.Show()` call sites (toggle-result checklist, warnings) are left as native, unthemed dialogs — they will stay light-colored even when the app is in dark mode. This is an accepted, deliberate tradeoff, not a gap: these are small, infrequent, informational popups, not the main UI surface, and the alternatives (CBT-hook title-bar theming, or a custom replacement dialog) were explicitly rejected as disproportionate effort/risk for this milestone. Do not build a themed MessageBox replacement or apply DWM theming to MessageBox's window handle.

### Windows 11 Rounded Corners / Mica Backdrop (THEME-06)
- **D-02:** Use `DWMWA_SYSTEMBACKDROP_TYPE` with the standard **Mica** value (not Mica Alt, not Acrylic) — matches File Explorer/Settings/most native Windows 11 apps. Apply alongside `DWMWA_WINDOW_CORNER_PREFERENCE` (rounded, the default preference value) on both MainForm and SettingsForm. Both are DWM attribute calls in the same family as the required dark-title-bar call (`DWMWA_USE_IMMERSIVE_DARK_MODE`) — same P/Invoke, same best-effort/non-throwing posture on Windows 10 or unsupported builds (the attribute is simply ignored, not an error).

### Live-Flip Stale-Color Edge Case (ToolStrip/ContextMenuStrip)
- **D-03:** Accept the known, unfixed WinForms bug (`dotnet/winforms#12027`) where `ToolStrip`/`ContextMenuStrip` separators and dropdown arrows keep their pre-flip color after a live theme change, as a documented limitation — it affects only the tray right-click menu's separator line, a glance-and-dismiss surface. Do **not** build a rebuild-the-menu-on-theme-change workaround; that's real new code (menu reconstruction + event wiring) for a cosmetic nit on a menu the user sees for under a second. Note this explicitly in code comments (this codebase's established convention) so a future reader doesn't "fix" it into unplanned scope.

### Theme Detection & Application Strategy
- **D-04:** Base layer is .NET 10's built-in `Application.SetColorMode(SystemColorMode.System)` (confirmed non-experimental, GA per research) called once at startup in `Program.cs` — do NOT hand-roll the full recolor pass PROJECT.md originally assumed was necessary; that assumption is now known-outdated (see canonical refs). `SetColorMode` handles launch-time control coloring and the title bar for free.
- **D-05:** The one confirmed gap in the built-in feature is **live theme-following** — `SetColorMode` does not react to the user flipping Windows' theme setting mid-session. Close this gap with a small hand-rolled patch: `Microsoft.Win32.SystemEvents.UserPreferenceChanged` subscription (BCL, `RigToggle.Windows` already has `UseWindowsForms=true` so this needs zero new packages) that diffs old-vs-new theme state (the event fires on many unrelated preference changes too) and re-applies theming when it actually changed.
- **D-06:** Registry key scoping: use `HKCU\...\Personalize\AppsUseLightTheme` for app/control chrome (title bar, controls) — this is a different, independently-settable Windows 11 value from `SystemUsesLightTheme` (taskbar). Do not conflate the two; `AppsUseLightTheme` is the correct key for everything this phase touches (Phase 13's tray icon contrast work is the one that cares about the taskbar-facing value, not this phase).
- **D-07:** Windows 10 / API-unavailable fallback (per the user's earlier "not sure/mixed, design for graceful degradation" call): every DWM attribute call (dark title bar, rounded corners, Mica) must be wrapped so a failed/no-op `DwmSetWindowAttribute` call never throws or crashes the app — treat these as best-effort visual enhancement, not a hard requirement. Flat control styling and control recoloring (THEME-04/05) are NOT Windows-11-gated and should still apply on Windows 10. Confirming the DWM dark-mode attribute value (20, post-20H1) and whether it actually applies on the target Windows 10 build is left as a rig-verification item (this phase should carry a human-verify checkpoint given the "only catchable on real Windows" history from Phases 8/9/11).

### `--tray` Hidden-Start Timing
- **D-08:** Because `Program.cs`'s `--tray` startup path never calls `Form.Show()`/`Form.Load()` (established Phase 8 pattern — `ApplicationContext()` with no `MainForm`), theme-application code for MainForm must NOT live in `Form_Load`/`OnShown` — it must run in a place that fires regardless of the startup path, e.g. `OnHandleCreated` (fires as soon as the native window handle exists, both visible and `--tray` paths) or explicitly from the same `InitializeTrayState()`-style unconditional-priming call Phase 8 already established for exactly this class of problem. This mirrors a bug class this project has hit twice already (Phase 8's `--tray` Show() suppression bug, Phase 11's lockout bug) — do not repeat it a third time by wiring theme application only into `Load`.

### Claude's Discretion
- Exact shape of the theme-provider abstraction (`IThemeProvider`/`WindowsThemeProvider` in Core/Windows per the architecture research, vs. a simpler static helper) — left to planner; research recommends the `IThemeProvider` (Core) / `WindowsThemeProvider` (Windows) split mirroring the existing `IAutostartConfigurator`/`WindowsAutostartConfigurator` pattern, but this is an implementation detail, not a user-facing decision.
- Where the per-control recolor pass (`ThemeApplier` or similar) lives — research recommends `RigToggle.App` (it encodes Designer-generated-form-specific control knowledge), consistent with the existing rule that WinForms composition code stays in the App layer.
- `UserPreferenceCategory` filter used for the `SystemEvents.UserPreferenceChanged` subscription, and whether `SystemEvents` events need explicit UI-thread marshaling — left to planner/executor to verify against actual runtime behavior (flagged MEDIUM confidence in research, not independently re-verified).
- Exact `FlatStyle` value per control (`Flat` vs `System`) — research flagged an open `dotnet/winforms#13897` bug where `FlatStyle.Flat` buttons don't color correctly in dark mode; planner should use `FlatStyle.System` for buttons specifically to route around this, `Flat` elsewhere is fine.
- Whether `SettingsForm` is instantiated fresh per open or reused/hidden across opens — unconfirmed from PROJECT.md, needs a quick codebase check before planning the live-update event subscription/unsubscription lifecycle (a transient dialog leaking a subscription to a `WindowsThemeProvider.ThemeChanged` event is a flagged pitfall).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level
- `.planning/PROJECT.md` — core value, v1.2 milestone goal ("Visual Polish & Documentation")
- `.planning/REQUIREMENTS.md` — THEME-01 through THEME-06 (mapped to this phase)
- `.planning/ROADMAP.md` — Phase 12 section: goal, success criteria, depends on Phase 11

### Research (this milestone — read all before planning; STACK.md and PITFALLS.md correct an outdated assumption in PROJECT.md's original milestone framing)
- `.planning/research/SUMMARY.md` — synthesized findings and suggested phase risk ordering
- `.planning/research/STACK.md` — confirms .NET 10 ships `Application.SetColorMode(SystemColorMode.System)` as a built-in, GA (non-experimental) API; corrects PROJECT.md's "no built-in support, requires manual DWM calls" framing — the real scope is base-layer-plus-two-patches (live-update, MessageBox), not full hand-rolled theming
- `.planning/research/ARCHITECTURE.md` — `IThemeProvider`/`WindowsThemeProvider` Core/Windows placement, `ThemeApplier` in App layer, cross-thread marshaling and transient-dialog event-unsubscription pitfalls, verified integration points against the real source tree
- `.planning/research/PITFALLS.md` — 10 critical pitfalls incl. the live-update gap, `--tray`/`OnHandleCreated` timing risk (D-08 above), toolstrip stale-color bug (D-03 above), `AppsUseLightTheme` vs `SystemUsesLightTheme` key scoping (D-06 above), `FlatStyle.Flat` dark-mode bug
- `.planning/research/FEATURES.md` — table-stakes vs. differentiator vs. anti-feature framing that shaped THEME-01..06's scope; explicitly rules out full custom-owner-drawn control libraries and frameless chrome

### Prior phases (tray/startup precedent — this phase must not regress these)
- `.planning/milestones/v1.1-phases/08-tray-residency-autostart-toast-notification/08-CONTEXT.md` — D-06: the `--tray` hidden-start mechanism and why `Form.Load` never fires under it (direct precedent for D-08 above)
- `.planning/milestones/v1.1-phases/11-configurable-tray-close-minimize-behavior-user-selectable-op/11-CONTEXT.md` — D-11 and the lockout-bug history: precedent for why startup-path-conditional code (like theme application) needs careful handle-creation-timing verification, not just `Load`/`Shown`

### Existing code (this phase's actual surface area)
- `src/RigToggle.App/Program.cs` — composition root; `Main()`'s `--tray`/`ApplicationContext` branching (lines ~119-150) is where `SetColorMode` and any startup theme-application call must be sequenced correctly relative to both startup paths
- `src/RigToggle.App/MainForm.cs` — `LoadTrayIconsIfNeeded()`/`InitializeTrayState()` (the established "runs regardless of startup path" pattern to mirror for theme application), 4 `MessageBox.Show()` call sites (left untouched per D-01)
- `src/RigToggle.App/MainForm.Designer.cs` — small control set: `lblMode`, `btnToggle`, `btnSettings`, `notifyIcon`, `trayContextMenu` + 3 `ToolStripMenuItem`s + 1 `ToolStripSeparator`
- `src/RigToggle.App/SettingsForm.Designer.cs` — much larger control set: 3 `GroupBox`es, `DataGridView` (3 columns, incl. 2 `DataGridViewCheckBoxColumn`), 2 `ComboBox`es, `TextBox`es, 5 `CheckBox`es, 5 `ErrorProvider`s — the `DataGridView` is the single largest recoloring effort per research (owner-draw/explicit color overrides needed to avoid a native-white grid surface)
- `src/RigToggle.App/RigToggle.App.csproj` — confirms tray icons are `EmbeddedResource` with `LogicalName` (not `<ApplicationIcon>`) — relevant context for Phase 13, not this phase's scope, but confirms no `.exe` icon is currently wired at all

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainForm.InitializeTrayState()` / `LoadTrayIconsIfNeeded()` — the established "must run before either `Application.Run` branch, idempotent" pattern (Phase 8) that theme-application startup logic should mirror, per D-08.
- `IAutostartConfigurator`/`WindowsAutostartConfigurator` (Phase 8) — direct structural precedent for a new `IThemeProvider`/`WindowsThemeProvider` pair (Core interface, Windows implementation), per architecture research.

### Established Patterns
- XML-doc rationale comments explaining *why*, not *what* — continue this convention for D-01 (why MessageBox stays native), D-03 (why the stale-color bug isn't worked around), and D-08 (why theme application can't live in `Form_Load`).
- Hand-rolled P/Invoke kept minimal (`user32.dll`/`dwmapi.dll` `DllImport`s), no third-party UI/theming library — consistent with this codebase's established "hand-roll only what's needed" posture (CLAUDE.md, prior phases' Windows interop).

### Integration Points
- `Program.cs` `Main()` — gains the `SetColorMode` call and startup theme-application sequencing.
- `MainForm` and `SettingsForm` — both need the DWM title-bar/corner/backdrop calls applied via `OnHandleCreated` (per D-08), and both need a per-control recolor pass (`ThemeApplier`, per architecture research) for anything `SetColorMode` doesn't reach (custom `FlatStyle`, `DataGridView` cell styles).
- A new `WindowsThemeProvider.ThemeChanged` event (or equivalent) that `MainForm` and any open `SettingsForm` subscribe to for live-update — subscription/unsubscription lifecycle on `SettingsForm` (transient dialog) needs explicit `FormClosed` cleanup to avoid a leaked event handler.

</code_context>

<specifics>
## Specific Ideas

- MessageBox: leave native/light, no themed replacement.
- Mica (not Mica Alt or Acrylic) as the Windows 11 backdrop.
- ToolStrip/ContextMenuStrip stale-color-after-live-flip: documented known limitation, no workaround built.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. Manual theme override (THEME-09), accent-color-aware highlight (THEME-07), and a custom-drawn toggle-switch control (THEME-08) remain correctly deferred to the v2 backlog per REQUIREMENTS.md, not raised as in-scope asks during this discussion.

</deferred>

---

*Phase: 12-Theme-Infrastructure-Live-Theme-Following*
*Context gathered: 2026-08-02*
