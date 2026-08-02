# Feature Research

**Domain:** Visual polish for an existing WinForms Windows tray utility (theme-following UI + tray-mode-indicator icon pair) — v1.2 milestone
**Researched:** 2026-08-02
**Confidence:** MEDIUM-HIGH (WinForms/DWM mechanics and Microsoft icon guidance are HIGH confidence, sourced directly; "what well-regarded apps actually do" is thinner because most polished theme-following Windows apps are WPF/WinUI, not WinForms, so those are used as aspirational reference points, not literal implementation precedent — flagged per item below)

This supersedes the v1.1 `FEATURES.md` (dated 2026-07-26, which covered tray residency/hotkey/CLI/multi-monitor). This file covers the **v1.2 milestone only**: system light/dark theme-following for MainForm + SettingsForm, and a genuinely distinct rig-mode/normal-mode tray icon pair. It assumes all existing mechanisms (NotifyIcon tray residency, tray context menu, mode-toggle logic, Settings persistence) are already built and working per `.planning/PROJECT.md` — this document only covers what's new to make those existing surfaces *look* modern/native.

## Feature Landscape

### Table Stakes (Users Expect These)

Features that, if missing or half-done, make the "modern/native" claim ring false — the bar a small Windows utility must clear to not look like an unstyled WinForms app with a paint job bolted on.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| System theme detection (read `HKCU\...\Personalize\AppsUseLightTheme`) | Root dependency for every other theming feature — nothing else can happen without knowing which mode is active | LOW | Registry read is the documented, standard technique (no public typed API exists for this in .NET/WinForms). Read once at startup as the baseline. |
| Dark/light title bar via `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)` | This is the single most visible "does this app respect my theme" signal — a white title bar on an otherwise-dark app reads as broken, not stylish | LOW-MEDIUM | Attribute value 20 (post-20H1 builds); works on Windows 10 20H1+ and Windows 11 despite docs listing Windows 11 only. Apply to both MainForm and SettingsForm. |
| Live theme switching without app restart | Windows itself, VS Code, Windows Terminal, and every actively-maintained utility apply a theme change live; requiring a relaunch to pick up a Settings > Personalization toggle reads as an unfinished feature, not a design choice | MEDIUM | Subscribe to `Microsoft.Win32.SystemEvents.UserPreferenceChanged`, re-read the registry value, and re-apply DWM attribute + re-color controls when it actually flips (event fires on many unrelated preference changes too — must diff old vs new value, not react to every firing). |
| Full, consistent control recoloring — not just the title bar | A themed title bar sitting above a stock white/gray WinForms client area looks *more* broken than doing nothing (half-themed is worse than unthemed) — this is the actual majority of the implementation effort | HIGH | WinForms has no supported theming API; every `BackColor`/`ForeColor` on every `Panel`/`Label`/`Button`/`GroupBox`/`ComboBox`/`CheckBox`/`ListView`/`DataGridView` (the multi-monitor settings grid) must be set by hand per theme. `ComboBox`/`ListView` in particular need owner-draw or `FlatStyle=Flat` + explicit color overrides to avoid a native-white dropdown/list surface breaking dark mode. Confirmed pitfall from ShareX's own "Experimental dark theme" changelog notes: naive color-property reassignment leaves several stock controls looking "suboptimal" — budget for per-control verification, not a single blanket pass. |
| Flat, borderless/subtle-border styling (no legacy 3D bevel/gradient button chrome) | Default WinForms `FlatStyle=Standard` buttons and classic sunken group boxes read as Windows-XP/7 era, undermining "modern" regardless of correct dark/light colors | LOW-MEDIUM | Set `FlatStyle=Flat` (or `System` where native look is acceptable) with a 1px border color that itself flips per theme; avoid `FlatStyle=Popup`'s hover-bevel look, which still reads dated. |
| Taskbar-background-agnostic tray icon contrast | A tray glyph designed only against one taskbar background (e.g. pure white icon assuming a dark taskbar) becomes invisible the moment the user's taskbar is the opposite shade — this is a real, currently-open class of bug (see anti-feature #4 for the "fix via variant-swapping" trap) | MEDIUM | Confirmed real-world failure mode: a 2026 GitHub issue against Claude Desktop for Windows describes exactly this — "system-tray icon invisible when app theme differs from the Windows taskbar mode." Cheapest robust fix: design each mode icon with self-contained contrast (saturated fill color + a dark or neutral outline) rather than a pure monochrome glyph that only works on one background. |
| Multi-resolution `.ico` embedding (16, 20, 24, 32px at minimum) | `NotifyIcon` and the taskbar/Alt-Tab surfaces render at different DPI-scaled sizes; a single 32x32 source stretched down looks soft/blurry at the actual 16x16 (100% DPI) tray size, which is the size that matters most for "genuinely distinct" legibility | LOW | Ship one `.ico` containing 16/20/24/32 (and ideally 40/48/64/256 for taskbar/Alt-Tab/shortcut icon reuse) frames, each hand-tuned rather than one auto-scaled master — auto-downscaling loses exactly the fine silhouette control the mode-distinction goal needs at 16px. |
| Two genuinely distinct-at-16x16 mode icons (shape difference, not just a color swap) | This is the explicit ask, and Microsoft's own icon guidance is blunt about the failure mode: "avoid relying on color alone to convey meaning; use shape and metaphor with color to communicate" — a same-silhouette red/green pair fails colorblind users and reads as a generic status dot, not "rig mode" vs "normal mode" | MEDIUM | Needs an actual distinguishable silhouette per state (different shape/motif), reinforced by (optionally) different color families too — color alone is not sufficient per official guidance. |

### Differentiators (Competitive Advantage)

Features that push past "acceptable modern WinForms app" into "feels like it was designed on purpose" — valuable, not required, and worth doing only after table stakes are solid.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Windows accent-color-aware highlight on the core toggle control/status indicator | Ties the app's single most-used control into the user's actual system personalization rather than a hardcoded blue/accent, which is what separates "themed" from "feels native" in apps like Windows Terminal/PowerToys (WinUI-based — cited here only as the visual bar these set, not as literal WinForms precedent) | MEDIUM-HIGH | Read via DWM colorization API or `UISettings.GetColorValue`; needs the same live-update handling as theme (`WM_DWMCOLORIZATIONCOLORCHANGED`). Real but bounded scope — one accent color, applied to one or two controls, not a full palette-generation system. |
| Domain-specific icon metaphor (e.g., a wheel/monitor motif) instead of a generic on/off or power-symbol pair | A metaphor tied to the actual product concept (rig vs desk) reads immediately and memorably vs. a generic colored-dot pair that any utility could use | LOW-MEDIUM | Almost entirely a design-time cost (icon authoring), not engineering — reuses the same multi-resolution `.ico`/NotifyIcon mechanism already required as table stakes. Directly satisfies Microsoft's "single clear metaphor, no more than two elements" guidance. |
| Rounded window corners / Mica-style DWM backdrop (Windows 11 only) | Matches the native Windows 11 app shell aesthetic more closely than a theme-colored-but-still-square window | MEDIUM | `DWMWA_WINDOW_CORNER_PREFERENCE` / `DWMWA_SYSTEMBACKDROP_TYPE` — additional DWM attribute calls, same mechanism family as the required dark-title-bar call, so incremental cost given that's already being built. No-ops gracefully pre-Win11 (attribute simply ignored), so safe to attempt unconditionally. |
| Custom-drawn "big toggle" control for MainForm's single core action, replacing the default `Button` | The core value of this app is one click; a purpose-built toggle-switch-style control (rather than a generic themed button) visually elevates the app's single most important interaction | MEDIUM | Owner-draw (`OnPaint` override) on top of the already-established theme-color palette — bounded to one control, not a full control-library rewrite (see anti-feature #1 for why a full rewrite is out of scope). |
| Manual theme override (force light/dark regardless of system setting) | Small, cheap addition once "follow system theme" exists; some users want to pin a mode | LOW | Settings persistence layer (`System.Text.Json` to `%APPDATA%`) already exists per current stack — this is one more enum field, no new infrastructure. |
| Coherent icon family across tray, `.exe`/taskbar icon, and any About-box branding, sharing one motif in both mode variants | Reinforces "this was designed as a product" rather than "the tray icon got attention but the rest didn't" | LOW-MEDIUM | Reuses the same authored artwork at larger sizes (the `.exe` icon already needs 256x256+ frames per the multi-resolution `.ico` table-stakes item) — largely a "don't stop at 16x16" scoping decision, not new mechanism. |

### Anti-Features (Commonly Requested, Often Problematic)

Things that look like the "obviously more polished" choice but create disproportionate risk or scope for this specific app.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|------------------|-------------|
| Full custom-owner-drawn control library (rounded buttons, ripple/hover animations, restyled scrollbars, etc. across every control) | "If we're already recoloring everything, why not make it look pixel-perfect Fluent" | WinForms has no supported theming hook for this; hand-building a mini design system for a 2-window personal utility is open-ended effort with high risk of an inconsistent, half-finished patchwork (exactly what ShareX's own maintainers flagged as the outcome of naive control restyling) | Do the table-stakes work (DWM title bar + per-control color properties + `FlatStyle=Flat`) and stop there; accept native control silhouettes with correct theme colors rather than reshaping them |
| Frameless custom-chrome window with hand-built minimize/close buttons | Mimics native Windows 11 app chrome more closely than a themed-but-still-native title bar | Reintroduces Aero Snap, drag-resize, and multi-monitor/DPI edge cases that the native title bar + DWM dark-mode call sidesteps entirely for free; disproportionate risk for a 2-window utility whose core value has nothing to do with window chrome | Keep the native title bar, theme it via `DWMWA_USE_IMMERSIVE_DARK_MODE` (already table stakes) |
| Color-only mode differentiation on the tray icon (identical glyph, red vs. green tint only) | Fast to produce, "traffic light" mental model feels intuitive | Fails Microsoft's own icon guidance (color alone is explicitly called out as insufficient), fails at 16x16 where fine color differences compress, and fails colorblind users; reads as a generic status dot rather than communicating "rig mode" specifically | Distinct silhouette per mode (different shape/motif), color as reinforcement not the sole signal |
| Theme-swapping tray icon variants — a separate icon asset per taskbar light/dark state, multiplying the required set to 2 (mode) × 2 (taskbar theme) = 4 icons plus swap-detection logic | Seems like the "correct" fix for the taskbar-contrast pitfall (table stakes item above) | Microsoft's own guidance explicitly frames light/dark theme-sensitive icon assets as *optional*, not required — building the 2×2 variant matrix plus a second theme-detection/swap system doubles the asset and logic surface for a problem that a single self-contained-contrast design solves more cheaply | Design each of the 2 mode icons with built-in contrast (saturated fill + outline) that reads on both taskbar backgrounds; keep the icon set at exactly 2 |
| Detailed/gradient/photographic icon artwork carried straight into the 16x16 tray asset | The larger app-icon artwork (256x256, About box) looks good, so reusing it everywhere seems efficient | Fine detail and gradients that read well at 256x256 collapse into a shapeless blur at 16x16 — this directly undermines the "reads clearly in the tray" goal that is the actual point of this feature | Author a deliberately simplified silhouette specifically for the 16x16 tray frame (and its DPI siblings), separate from the more detailed larger-size artwork within the same `.ico` |
| Migrating the app (or just these two windows) to WPF/WinUI3 to get theming "for free" | Both frameworks have far better native theming support than WinForms, which makes this the technically easiest path to the same visual result | A framework migration is a rewrite of a ~6,900 LOC, 4-project solution to solve a styling problem — wildly disproportionate scope for a visual-polish milestone, and explicitly not what `.planning/PROJECT.md` scoped ("System-theme-following in WinForms has no built-in support... accepted as worthwhile complexity per explicit user call") | DWM API calls + manual per-control color properties within the existing WinForms app, as already decided |

## Feature Dependencies

```
System theme detection (registry read)
    ├──requires (root dependency for)──> Dark/light title bar (DWM call)
    ├──requires (root dependency for)──> Full control recoloring
    └──requires (root dependency for)──> Live theme switching (SystemEvents subscription)

Live theme switching ──enhances──> Dark/light title bar
Live theme switching ──enhances──> Full control recoloring
    (without live switching, both are launch-time-only — reads as an
    unfinished feature per current expectations, see table stakes notes)

Multi-resolution .ico embedding ──requires──> Two distinct mode icons
    (the existing NotifyIcon swap mechanism from Phase 8 already
    selects an icon per mode; this only replaces/expands the icon
    assets it points at — no new tray infrastructure needed)

Taskbar-background-agnostic icon contrast ──conflicts──> Theme-swapping icon
    variant anti-feature (both solve the same problem; pick the
    self-contained-contrast design, not the 2x2 variant matrix)

Domain-specific icon metaphor ──enhances──> Two distinct mode icons
    (a well-chosen metaphor makes the shape-differentiation
    requirement easier to satisfy well)

Windows accent-color-aware highlight ──requires──> a separate small
    API surface (DWM colorization / UISettings), not the same
    registry read as light/dark detection — can be built independently
    of the light/dark theming work, just shares the "live update" pattern

Manual theme override ──requires──> System theme detection
    (override is just "ignore the registry read, use a stored
    preference instead" — trivial once detection exists)
```

### Dependency Notes

- **Everything theme-related requires system theme detection first.** This is the one piece of new plumbing (a registry read + a `SystemEvents.UserPreferenceChanged` subscription) that every other theming feature sits on top of — build and verify it in isolation before touching title bars or control colors.
- **Live theme switching is not optional polish on top of the other two — treat it as part of "done."** A title bar and control set that only reflect the theme active at process launch will visibly desync the moment the user flips Windows' light/dark toggle while the app is open, which is a worse look than not attempting theming at all.
- **The tray icon swap mechanism itself already exists** (Phase 8 shipped mode-reflecting tray icons, "functional but plain" per PROJECT.md) — this milestone's icon work is scoped to replacing/expanding the *assets*, not building new selection logic, except for the optional taskbar-contrast decision noted above.
- **Taskbar-contrast handling and mode-distinction are two different axes** — don't conflate them. Mode distinction (rig vs. normal) is the explicit ask; taskbar-background contrast is a robustness concern that applies regardless of which mode icon is showing. Solving both with one well-designed, self-contained-contrast icon pair (rather than a 2×2 variant matrix) keeps scope bounded.

## MVP Definition

Framed for this milestone specifically (not a from-scratch product MVP).

### Launch With (v1.2 core)

- [ ] System theme detection + live-update subscription — nothing else works without this
- [ ] Dark/light title bar (DWM) on MainForm and SettingsForm
- [ ] Full control recoloring across both forms, including the multi-monitor settings `DataGridView`/`ListView` — the single largest effort item, and the one most likely to look unfinished if rushed
- [ ] Flat button/panel styling (no legacy 3D bevel)
- [ ] Two shape-distinct mode icons authored specifically for 16x16, embedded as multi-resolution `.ico` files, with self-contained taskbar-background contrast

### Add After Validation (stretch within v1.2, if time remains)

- [ ] Rounded corners / Mica backdrop on Windows 11 (graceful no-op elsewhere, low incremental cost given DWM plumbing already exists)
- [ ] Domain-specific icon metaphor refinement (if first-pass icon design reads as generic, invest further design time here before adding new mechanism elsewhere)
- [ ] Coherent icon family (exe/taskbar icon, About box) reusing the same authored motif at larger sizes

### Future Consideration (defer past v1.2)

- [ ] Accent-color-aware highlight — real value, but a genuinely separate API surface and live-update path from the light/dark work; don't let it block shipping the core theming
- [ ] Custom-drawn toggle-switch control replacing the core action button — valuable but purely additive polish on a feature that will already work correctly once table stakes ship
- [ ] Manual theme override (force light/dark) — cheap but not requested; only worth adding if "follow system" alone proves insufficient in practice

## Sources

- [Support Dark and Light themes in Win32 apps — Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-windows-themes) — DWM dark-mode mechanism — HIGH confidence, official docs
- [Design guidelines for Windows app icons — Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-design) — silhouette/legibility/color guidance, "color alone insufficient," optional light/dark theme-sensitive assets — HIGH confidence, official docs, fetched directly
- [dotnet/winforms#12014 — title bar color regression discussion](https://github.com/dotnet/winforms/issues/12014) — confirms WinForms-specific `DwmSetWindowAttribute` behavior nuances — MEDIUM confidence, GitHub issue
- [ShareX#4304 — Dark theme poor visibility](https://github.com/ShareX/ShareX/issues/4304) and [ShareX#4310 — follow system theme setting](https://github.com/ShareX/ShareX/issues/4310) — real-world evidence that naive WinForms control-color reassignment produces suboptimal results in a comparable actively-maintained WinForms utility — MEDIUM confidence, corroborated by ShareX's own release notes language ("most WinForms controls look suboptimal when their color properties are modified")
- [anthropics/claude-code#72622 — tray icon invisible on taskbar/app theme mismatch](https://github.com/anthropics/claude-code/issues/72622) — concrete, recent (2026) real-world instance of the taskbar-vs-app-theme contrast pitfall — MEDIUM-HIGH confidence, first-party GitHub issue
- [SystemEvents.UserPreferenceChanged — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.userpreferencechanged) — live theme-change detection mechanism — HIGH confidence, official API docs
- WebSearch aggregation on `AppsUseLightTheme` registry key and DPI-scaled icon sizing (16/20/24/32/40/48/64/256) conventions — MEDIUM confidence, community-sourced but internally consistent and matches documented Windows icon/DPI scale steps

---
*Feature research for: Windows desktop utility visual-polish milestone (theme-following WinForms UI + tray icon pair)*
*Researched: 2026-08-02*
