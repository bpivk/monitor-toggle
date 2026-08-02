# Project Research Summary

**Project:** Rig Toggle — v1.2 milestone (Visual Polish & Documentation)
**Domain:** Windows desktop GUI utility (WinForms) — system-theme-aware UI + tray icon redesign, layered on an existing shipped 4-project solution (Core/Windows/App/Tests)
**Researched:** 2026-08-02
**Confidence:** HIGH

## Executive Summary

This milestone adds two visually-oriented capabilities to an already-shipped, working WinForms utility: (1) live system light/dark theme-following for `MainForm` and `SettingsForm` (title bar + control colors), and (2) a genuinely distinct, multi-resolution rig-mode/normal-mode tray icon pair. The single most important research finding, corroborated independently across all four research files, is that **PROJECT.md's own framing of this milestone is factually outdated**: as of .NET 10 (the project's pinned runtime), WinForms ships first-party, non-experimental dark-mode support (`Application.SetColorMode(SystemColorMode.System)`) that auto-recolors standard controls and manages the title bar's `DWMWA_USE_IMMERSIVE_DARK_MODE` attribute — this was previously assumed to require fully hand-rolled DWM P/Invoke plus manual per-control recoloring from scratch. The correct strategy is therefore "use the built-in feature as the base layer, then patch its two documented, verified gaps" rather than "hand-roll everything."

The two gaps that must be closed with custom code are well-documented and consistent across sources: `SetColorMode` applies theming only once, at startup, and does **not** live-update if the user flips the Windows theme while the tray-resident app keeps running (a very real scenario for this app's hours-long rig sessions) — closed via `Microsoft.Win32.SystemEvents.UserPreferenceChanged` (or raw `WM_SETTINGCHANGE`/`ImmersiveColorSet`) plus a manual `DwmSetWindowAttribute` re-apply; and native `MessageBox`/`OpenFileDialog` remain permanently light-mode regardless of `SetColorMode`, requiring either an audit-and-accept decision or replacement with a themed custom dialog. No new NuGet packages are required for either the theming or icon work — everything is BCL/WinForms-native plus dev-time-only tooling (ImageMagick/Inkscape) for producing the `.ico` assets.

The primary risks are not "will this work" (the API contracts are HIGH confidence, verified against official .NET 10 docs and open dotnet/winforms GitHub issues) but "will it be verified correctly on this app's specific non-standard surfaces": the existing `--tray` hidden-start path (bare `ApplicationContext`, no `MainForm` reference) has already caused divergent-path bugs twice in this project's history (Phase 8, Phase 11), and theming code applied in `Form_Load`/`Shown` will silently no-op on that path unless moved to `OnHandleCreated`. Similarly, the tray icon redesign has a real, previously-reported failure mode (a 2026 GitHub issue against a comparable Windows app) where a custom icon reads fine in isolation but becomes invisible against one of the two taskbar theme backgrounds — mitigated by designing self-contained contrast into each icon rather than building a second theme-driven variant axis. Architecturally, both capabilities slot cleanly into the existing Core-interface/Windows-adapter/App-composition pattern with zero deviation from precedent (`IThemeProvider`/`WindowsThemeProvider` mirrors `IAutostartConfigurator`/`WindowsAutostartConfigurator` exactly), and the icon-redesign work is fully decoupled from the theme-infrastructure work — they can be built in either order or in parallel.

## Key Findings

### Recommended Stack

No new runtime dependencies. The theming work uses `Application.SetColorMode(SystemColorMode.System)` (built into WinForms as of .NET 10, GA/non-experimental) as the base layer, a hand-rolled `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE = 20)` P/Invoke as a live-update backstop (fired via `SystemEvents.UserPreferenceChanged`), and manual `FlatStyle`/`Panel`-based control substitutions (e.g. `GroupBox` → bordered `Panel`+`Label`, since `GroupBox` always renders a beveled 3D border regardless of color mode) for the "modern flat look" requirement. The icon redesign needs no runtime library at all — it's dev-time tooling (Inkscape or Figma for vector source art, ImageMagick's `magick` CLI for packing multi-resolution `.ico` files) producing static resources the existing embedded-icon mechanism already consumes unchanged.

**Core technologies:**
- `Application.SetColorMode(SystemColorMode.System)`: base-layer control/title-bar theming, called once in `Program.cs` before any control is constructed — GA in .NET 10, must precede `Application.Run`
- `DwmSetWindowAttribute` (hand-rolled P/Invoke, `dwmapi.dll`): live title-bar re-theme on demand, best-effort/never-throw, mirrors the existing `GlobalHotkey.cs` public-façade pattern
- `Microsoft.Win32.SystemEvents.UserPreferenceChanged`: zero-dependency live theme-change detection (already available via `UseWindowsForms=true`'s shared framework); filter to `UserPreferenceCategory.General` and diff old vs. new resolved value before re-theming
- `Microsoft.Win32.Registry.CurrentUser` reads of `AppsUseLightTheme` (app/window theme) and `SystemUsesLightTheme` (taskbar/tray theme) — two distinct keys under the same `...\Personalize` path; conflating them is a documented pitfall (see below)
- ImageMagick (`magick` CLI) + Inkscape/Figma: dev-time-only, produce the multi-resolution `.ico` files (minimum 16×16 + 32×32 per official Win32 guidance; recommended fuller set 16/20/24/32/40/48/256 for clean DPI-scaled rendering)

### Expected Features

This is a visual-polish milestone on an already-feature-complete app, so "table stakes" here means "the bar a small utility must clear to not look like an unstyled WinForms app with a paint job bolted on," not net-new product features.

**Must have (table stakes):**
- System theme detection (registry read) + live-update subscription — root dependency for everything else
- Dark/light title bar via DWM attribute on both `MainForm` and `SettingsForm`
- Live theme switching without app restart — treated as part of "done," not stretch polish, since a launch-time-only theme desyncs visibly during this app's long tray-resident sessions
- Full, consistent control recoloring across both forms, including the multi-monitor settings grid (`DataGridView`) — the single largest effort item and the one most likely to look unfinished if rushed
- Flat/borderless button and panel styling (no legacy 3D bevel chrome)
- Two shape-distinct (not just color-distinct) mode icons authored specifically for 16×16, embedded as multi-resolution `.ico` files, with self-contained taskbar-background contrast

**Should have (competitive/stretch, if time remains):**
- Rounded corners / Mica-style DWM backdrop on Windows 11 (graceful no-op elsewhere; low incremental cost given DWM plumbing already exists for the title bar)
- Domain-specific icon metaphor refinement (wheel/monitor motif) over a generic status-dot pair
- Coherent icon family reused at larger sizes across tray, exe/taskbar icon, and any About-box branding

**Defer (explicitly out of scope for v1.2):**
- Windows accent-color-aware highlight — a genuinely separate API surface (DWM colorization/`UISettings`) with its own live-update path; don't let it block shipping core theming
- Custom-drawn toggle-switch control replacing the core action button — purely additive polish
- Manual theme override (force light/dark regardless of system) — cheap once detection exists, but not requested
- Full custom-owner-drawn control library, frameless custom-chrome window, theme-swapping 2×2 icon variant matrix, and any WPF/WinUI3 migration — all explicitly flagged as anti-features/disproportionate scope for a 2-form personal utility

### Architecture Approach

This is not a new subsystem — it's two capabilities threaded through the existing four-project solution using the exact composition-root + interface-adapter pattern already established by `IAutostartConfigurator`/`WindowsAutostartConfigurator`. `IThemeProvider`/`AppTheme` live in `RigToggle.Core` with zero Windows-API references (enforced project invariant); `WindowsThemeProvider` (registry read + `SystemEvents` subscription) and `DwmTitleBar` (public static façade over the DWM P/Invoke) live in `RigToggle.Windows`; `ThemeApplier` (the recursive, per-control-type recolor pass encoding this app's specific Designer-generated control layout) lives in `RigToggle.App` only, since WinForms *composition* concerns have never lived in `RigToggle.Windows` in this codebase. All three top-level forms (`MainForm`, `SettingsForm`, `MonitorConfirmDialog`) get constructor-injected `IThemeProvider`, subscribe to `ThemeChanged` while open, and unsubscribe on close/dispose to avoid the classic disposed-form event-leak. The icon redesign is a pure binary-asset swap into the existing `EmbeddedResource`/`LogicalName` wiring with zero code changes, and is architecturally fully independent of the theme work — it can be built in parallel or in either order.

**Major components:**
1. `IThemeProvider` (Core, new) — pure contract: current theme + change event, no registry/DWM knowledge
2. `WindowsThemeProvider` (Windows, new) — registry read + `SystemEvents.UserPreferenceChanged` subscription, deduped `ThemeChanged` event
3. `DwmTitleBar` (Windows, new) — best-effort static façade over `DwmSetWindowAttribute`
4. `ThemeApplier` (App, new) — recursive control-tree recolor pass keyed by concrete control type, applied on load and on every theme change
5. Icon asset pipeline (App/Resources, binary-only) — replaces `normal.ico`/`rig.ico` in place, no change to `LoadTrayIconsIfNeeded`

### Critical Pitfalls

1. **Double DWM attribute set (manual call fighting `SetColorMode`'s own internal call)** — causes a visible title-bar color flash/animation on form show. Avoid by deciding explicitly which mechanism owns the title bar (recommendation: `SetColorMode` as base + manual re-apply only as the live-update path, never both firing redundantly).
2. **Assuming `SetColorMode` live-updates on its own** — it is applied once at startup only; `dotnet/winforms#13935` confirms this is not implemented framework behavior. Must explicitly wire `SystemEvents.UserPreferenceChanged`/`WM_SETTINGCHANGE` and test by flipping Windows theme *while the app is already running*, not just via restart.
3. **Theming calls issued before the window handle exists** — silently no-ops on this app's proven-fragile `--tray` hidden-start path (`ApplicationContext` with no `MainForm`, already the source of two prior divergent-path bugs). Apply theming in `OnHandleCreated`, not `Form_Load`/`Shown`, and explicitly test the hidden-start-then-restore sequence.
4. **Wrong registry key drives tray icon theme selection** — `AppsUseLightTheme` (app/window chrome) vs. `SystemUsesLightTheme` (taskbar/tray) are independent keys; using the former for icon-variant selection produces a mismatched icon against the actual taskbar. Read `SystemUsesLightTheme` specifically for any taskbar-facing decision.
5. **Icon-swap GDI handle leak** — extending the existing 2-state (rig/normal) icon swap risks `new Icon(...)` per swap without disposal, which is a real risk specifically because this app stays tray-resident for multi-hour sessions with repeated toggles. Pre-load and cache all icon instances once at startup; never construct per-swap.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Theme Infrastructure
**Rationale:** Higher-risk, more novel piece of this milestone (cross-thread event marshaling, per-control-type recolor correctness, DWM best-effort handling, and the app's own non-standard `--tray` startup path). Sequencing it first means rig-verification cycles for these specifics happen early, and any per-control recoloring bugs get discovered while the forms are already being actively touched.
**Delivers:** `IThemeProvider`/`AppTheme` in Core; `WindowsThemeProvider`/`DwmTitleBar`/`NativeMethods` additions in Windows; `ThemeApplier` + wiring into `MainForm`/`SettingsForm`/`MonitorConfirmDialog` + `Program.cs` composition in App. Live theme-following (not just launch-time theming). Includes an audit-and-replace or audit-and-accept pass over existing `MessageBox.Show` call sites.
**Addresses:** System theme detection, dark/light title bar, live theme switching, full control recoloring, flat control styling (FEATURES.md table stakes)
**Avoids:** Pitfalls 1, 2, 3, 6, 7, 8 (double DWM set, no live-update, handle-timing no-op on `--tray` path, Win10 mismatch scoping, stale toolstrip cache, unthemed MessageBox)

### Phase 2: Tray Icon Redesign
**Rationale:** Fully independent of Phase 1 architecturally (no shared files, interfaces, or ordering dependency per ARCHITECTURE.md) — can run in parallel with or even before Phase 1 if design art is ready sooner. Grouped as its own phase because it has a distinct workflow (design tooling, asset iteration) rather than a code-architecture reason for separation.
**Delivers:** Two shape-distinct (not just color-distinct) mode icons, authored as SVG and hand-simplified at 16×16, packed into multi-resolution `.ico` files (minimum 16/32, recommended 16/20/24/32/40/48/256), dropped into `RigToggle.App\Resources\` with unchanged filenames/LogicalNames.
**Uses:** ImageMagick/Inkscape dev tooling (STACK.md); existing `EmbeddedResource`/`LoadTrayIconsIfNeeded` mechanism (ARCHITECTURE.md)
**Implements:** Icon asset pipeline component (ARCHITECTURE.md Component 5)

### Phase 3: Polish & Verification
**Rationale:** Several pitfalls in this milestone are explicitly *not* catchable by code review — they require real-rig, real-Windows testing against scenarios (`--tray` hidden-start, live theme flip while running, both taskbar theme backgrounds, extended-session GDI monitoring, actual DPI scaling on rig vs. desk monitor) that a sandboxed/non-Windows build environment cannot surface. This must be a dedicated late phase, not folded into Phase 1/2's "looks done" moment.
**Delivers:** Verified live theme-following (visible + tray-hidden), verified `--tray` startup-then-restore theming, verified tray icon contrast against both real taskbar themes and DPI sharpness on rig hardware, verified GDI handle stability over an extended session, documented decision on Windows 10 fallback scope (contingent on confirming actual rig PC Windows version), and README screenshot deliverable.
**Addresses:** PITFALLS.md's full "Looks Done But Isn't" checklist

### Phase Ordering Rationale

- Theme infrastructure precedes icon redesign only by convention/risk-priority, not by hard dependency — the two are provably decoupled (ARCHITECTURE.md Integration Points), so the roadmapper has real flexibility to reorder or parallelize if that suits available design-asset timing.
- Verification is deliberately its own late phase rather than embedded in Phase 1/2 because this project's own history (Phase 8, Phase 11) shows that its most consequential bugs are startup-path-divergence and live-update bugs invisible to a normal launch/relaunch test cycle — grouping all "must verify on real rig, can't verify in sandbox" checks into one late phase matches how this project has actually been debugged before.
- Documentation (README screenshots) is scoped last since it depends on the finished visual result, per ARCHITECTURE.md's explicit build-order note.

### Research Flags

Phases likely needing deeper research during planning:
- None flagged as needing *additional* research-phase investment — all four research files for this milestone are HIGH confidence on the core API contracts (verified directly against official .NET 10 docs and primary-source dotnet/winforms GitHub issues), and the codebase-specific integration points were verified by direct source-tree reads, not inference.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Theme Infrastructure):** Standard, well-documented WinForms/.NET 10 API + established Core/Windows/App adapter pattern already used four times in this codebase (`IAutostartConfigurator`, `IMonitorController`, `IAudioController`, `GlobalHotkey`) — implementation guidance is already concrete enough (see ARCHITECTURE.md Patterns 1-4 with working code examples) to plan directly.
- **Phase 2 (Tray Icon Redesign):** Standard `.ico` packing/DPI-sizing conventions, official Microsoft icon design guidance already cited directly (FEATURES.md, STACK.md).
- **Phase 3 (Polish & Verification):** Not a research question — a checklist-driven rig-verification phase; the checklist itself is already fully enumerated in PITFALLS.md.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Core API contract (`Application.SetColorMode`, DWM attribute value, registry keys) verified directly against official Microsoft Learn docs and .NET 10 release notes; a few runtime-behavior caveats (Windows 10 title-bar-only community reports, `UserPreferenceCategory.General` filtering) are MEDIUM |
| Features | MEDIUM-HIGH | WinForms/DWM mechanics and Microsoft icon guidance are HIGH; "what well-regarded apps actually do" is thinner since comparable polished theme-following apps are mostly WPF/WinUI, used as aspirational reference only, not literal WinForms precedent |
| Architecture | HIGH | Integration points verified directly against the real source tree (not inference), reusing an established, four-times-precedented pattern in this codebase; a couple of WinForms-runtime specifics (SystemEvents threading guarantees, UserPreferenceCategory filtering) are MEDIUM |
| Pitfalls | HIGH for framework-API behavior (verified against official .NET 10 docs and primary-source dotnet/winforms open GitHub issues #13935, #12027, #12014); MEDIUM for icon-design/DPI specifics (WebSearch-aggregated, cross-checked but not project-verified) |

**Overall confidence:** HIGH

### Gaps to Address

- **Actual Windows version of the rig PC is unconfirmed** (Windows 10 vs. 11) — `Application.SetColorMode` dark mode and full control theming are Windows-11-only by documentation; this is a scoping question that must be resolved as a prerequisite check in the theme-infrastructure phase, not assumed. If Windows 11 (likely given project context), Pitfall 6 (Win10 dark-title/light-body mismatch) is moot and can be explicitly scoped out.
- **Whether `SettingsForm` is instantiated fresh per open or reused/hidden** is not confirmed from research alone (flagged as an Integration Gotcha in PITFALLS.md) — verify against the actual codebase before writing theme-application/subscription code, since a reused-instance assumption vs. fresh-instance assumption changes where `ThemeChanged` subscribe/unsubscribe must live.
- **`WM_SETTINGCHANGE`/`ImmersiveColorSet` category filtering** (`UserPreferenceCategory.General`) is MEDIUM confidence, community-pattern-sourced rather than pinned to one official doc — validate on the rig; fall back to unconditional re-read-and-compare (already the recommended safer default in ARCHITECTURE.md's `WindowsThemeProvider` example) if the category proves too narrow.
- **Toolstrip stale-brush-cache bug (`dotnet/winforms#12027`)** has no clean first-party fix as of this research — must be explicitly accepted as a documented known limitation or worked around by rebuilding the `ContextMenuStrip` on live theme change; this is a product/scope decision for planning, not an open technical question.

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100 — confirmed `Application.SetColorMode` non-experimental in .NET 10, Windows-11-only scope, MessageBox/Designer limitations
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setcolormode?view=windowsdesktop-10.0 — method signature, `SystemColorMode` values, live-update gap stated explicitly
- https://learn.microsoft.com/en-us/windows/win32/shell/notification-area — official 16×16/32×32 minimum tray icon size guidance, `LoadIconMetric`
- https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-design — silhouette/legibility/"color alone insufficient" guidance
- https://github.com/dotnet/winforms/issues/13935, #12027, #12014 — primary-source, open/unresolved GitHub issues confirming no built-in live theme-following, stale toolstrip brush cache, title-bar regression
- Direct source-tree reads of `Program.cs`, `MainForm.cs`, `SettingsForm.cs`, `MonitorConfirmDialog.cs`, `NativeMethods.cs`, `GlobalHotkey.cs`, `IAutostartConfigurator.cs`, and related `.csproj` files — ground truth for existing architecture and integration points

### Secondary (MEDIUM confidence)
- https://ironsoftware.com/academy/csharp-framework/dotnet10-dark-mode-winforms/ — practical caveats corroborating the official live-update gap and Windows-11-only scope
- https://github.com/ShareX/ShareX/issues/4304, #4310 — real-world evidence of naive WinForms control-color reassignment producing suboptimal results in a comparable actively-maintained app
- https://github.com/anthropics/claude-code/issues/72622 — concrete 2026 real-world instance of the taskbar-vs-app-theme icon contrast pitfall
- https://github.com/Aldaviva/DarkNet — alternative library considered and deliberately not adopted (hand-rolling preferred, consistent with this project's established dependency-minimization bias)
- WebSearch aggregation on `AppsUseLightTheme`/`SystemUsesLightTheme` registry key semantics and multi-resolution `.ico` DPI-sizing conventions — consistent across multiple independent sources

### Tertiary (LOW confidence)
- None flagged — all findings in this research pass were corroborated by at least one MEDIUM-or-higher source; no single-source/pure-inference claims were carried into this summary.

---
*Research completed: 2026-08-02*
*Ready for roadmap: yes*
