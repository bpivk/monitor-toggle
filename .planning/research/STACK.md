# Stack Research

**Domain:** Windows-only desktop GUI utility (display/audio/process control automation), extended with tray/hotkey/CLI/IPC/multi-monitor automation, now extended again with system-theme-aware visual polish + tray icon redesign
**Researched:** 2026-07-24 (v1.0 foundation) — updated 2026-07-26 (v1.1 additions) — updated 2026-08-02 (v1.2 additions)
**Confidence:** HIGH (v1.0 stack, shipped and validated) / MEDIUM-HIGH (v1.1 additions, shipped and validated) / HIGH (v1.2 theming API contract, MEDIUM for a few runtime-behavior caveats — see below)

---

# v1.2 Milestone Additions (2026-08-02)

Scope: making `MainForm` and `SettingsForm` follow the Windows OS light/dark theme setting live (title bar + control colors) with a modern flat look, and redesigning the pair of embedded tray-mode `.ico` icons for real visual distinction. This section covers **only** what's new for v1.2 — the toggle/tray/hotkey mechanisms below (v1.0/v1.1 sections) are already shipped and validated; they are not re-researched here.

## Headline Finding

**No new NuGet packages are required for the theming work.** The single most important finding of this research: **as of .NET 10 (already the project's target), WinForms ships first-party dark-mode support** — `Application.SetColorMode(SystemColorMode.System)` — that repaints standard controls and the form's non-client title bar to match Windows' theme setting, with zero new dependencies. This is a meaningfully smaller lift than PROJECT.md's milestone framing implies ("requires manual DWM API calls for the title bar plus re-coloring every control by hand") — that framing predates confirming .NET 10's built-in support is now GA (it shipped experimental in .NET 9, non-experimental in .NET 10). The built-in feature should be the *primary* mechanism; hand-rolled DWM P/Invoke is still needed, but only as a narrow backstop for the one documented gap (live theme-change following — see below), not for the whole feature. The tray icon redesign needs no new runtime dependency either — it's dev-time tooling (a vector editor + `.ico` packer) producing static resource files the existing embedded-icon mechanism already consumes unchanged.

## Recommended Stack Additions

### Core Technologies (mechanisms, not packages — all already available in the existing `net10.0-windows` + WinForms project)

| Technology | Version/Source | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `Application.SetColorMode(SystemColorMode)` | Built into WinForms, GA/non-experimental as of .NET 10 (experimental behind compiler warning `WFO5001` in .NET 9) | Sets the app's color mode (`Classic`/`Dark`/`System`) at startup; auto-themes standard controls (Button, Label, TextBox, ComboBox, CheckBox, GroupBox, ToolStrip — everything both forms already use) and internally calls `DwmSetWindowAttribute`/`DWMWA_USE_IMMERSIVE_DARK_MODE` on the form's title bar for you | Call `Application.SetColorMode(SystemColorMode.System)` once in `Program.cs`, **before** any control is constructed (must precede `Application.Run`/`Application.Initialize` and precede any `new Form(...)` — setting it after controls are created leaves some controls stuck in light mode, per Microsoft's own guidance and corroborating community testing). This single call does most of the "follow the OS theme" and "themed title bar" work with no P/Invoke of your own. |
| Hand-rolled `DwmSetWindowAttribute` P/Invoke (`DWMWA_USE_IMMERSIVE_DARK_MODE` = 20) | N/A — ~10-line `DllImport`, no package | Explicit, callable-anytime control over one form's title-bar dark/light state | Needed as a **backstop, not a replacement**, for a gap `Application.SetColorMode`'s own Microsoft Learn documentation states explicitly: *"If the system setting is changed, the application will not automatically adapt to the new setting."* The built-in feature only themes controls/title bar once, at startup. To make the title bar re-theme **live** (this milestone's explicit goal), call this P/Invoke directly on each open `Form.Handle` whenever a theme change is detected at runtime (see next row) — independent of, and in addition to, the one-time `SetColorMode` call. |
| `Microsoft.Win32.SystemEvents.UserPreferenceChanged` | Included in the `Microsoft.WindowsDesktop.App` shared framework referenced by any `UseWindowsForms=true` net10.0-windows project — no separate `PackageReference` needed | Live detection of the OS theme toggling while the app is running | The managed, WinForms-idiomatic way to detect a live Windows theme change (the zero-dependency alternative is handling raw `WM_SETTINGCHANGE` with `lParam == "ImmersiveColorSet"` in a form's `WndProc`, which also works — but neither `MainForm` nor `SettingsForm` currently overrides `WndProc`, so `SystemEvents` avoids adding one). On `UserPreferenceChanged` (category `General`), re-run: `Application.SetColorMode(SystemColorMode.System)` again → the `DwmSetWindowAttribute` call on each open form → `Refresh()`/re-walk visible controls. Accept the documented WinForms limitation (see Version Compatibility) that a handful of control colors may not fully repaint without a full `Show()`/recreate, rather than engineering around it for a 2-form personal utility. |
| Manual `FlatStyle`/`Panel`-based control redesign | Built into WinForms | "Modern flat look" replacing default WinForms chrome | No theming library needed for this: set `FlatStyle.System` on buttons (see the `FlatStyle.Flat` dark-mode bug warning below), replace `GroupBox` (which always draws a beveled 3D border regardless of color mode) with a `Panel` + `Label` pairing using a thin flat 1px border color drawn manually or via `Panel.Paint`, and rely on `SystemColorMode.System`'s own palette for background/foreground colors rather than hardcoding `SystemColors.*`. This is a per-control, hand-authored pass across two forms — small enough that no component library is worth the dependency. |

### Supporting Libraries

None required at runtime. Dev-time tooling only (not project/NuGet dependencies):

| Tool | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| ImageMagick (`magick` CLI) | 7.x current stable | Pack PNG frames into a single multi-resolution `.ico` | One-time (or per design-iteration) step to build the two tray `.ico` files. Free, scriptable (fits the project's existing CLI-driven `dotnet publish` discipline), cross-platform — works in a non-Windows dev sandbox too, not just on the Windows rig. |
| Inkscape (or Figma, web-based) | Inkscape 1.4.x current stable | Design the two tray icons as vector art (SVG) before rasterizing to each target pixel size | Vector source lets every target size be regenerated cleanly if the design changes; Inkscape's scriptable CLI export chains naturally into the ImageMagick packing step. Figma is an equally valid free-tier alternative if a web-based tool is preferred — pick on tooling comfort, not capability. |

## Feature-by-Feature Detail

### 1. System light/dark theme following (title bar + controls)

- **Baseline (startup theming):** `Application.SetColorMode(SystemColorMode.System)` in `Program.cs`'s `Main`, before `Application.Run`. This alone gets both forms into the correct light/dark palette and a correctly-colored title bar *at launch time*, on Windows 11.
- **Live-follow gap and fix:** Microsoft's own docs are explicit that a running app will not auto-adapt if the user flips the OS theme mid-session. Close this gap with `Microsoft.Win32.SystemEvents.UserPreferenceChanged`, wired up once at composition-root time (same lifetime scope as the existing hotkey/tray components), which on a `General`-category change re-applies `SetColorMode` + calls `DwmSetWindowAttribute` on every currently-open form + triggers a `Refresh()` pass. Document (don't silently hide) that a few specific control colors are a known WinForms limitation and may lag until the form is next shown/recreated — this is consistent with what other current (2026) sources report as an accepted WinForms constraint, not a bug in this app's own code.
- **Windows-version caveat:** dark mode (`SystemColorMode.Dark`/`System`) is documented as **Windows 11 only** — on Windows 10 it silently falls back to light/classic. The raw `DwmSetWindowAttribute` call has been community-reported (not officially documented) to also work on some Windows 10 builds (1903+/20H1+) for the title bar specifically, but that's unverified — don't rely on it as guaranteed. Given this project's context (personal rig, no stated Windows 10 requirement), this is a non-issue unless the app is later run on an older machine.
- **High contrast:** `SystemColorMode.Dark` is automatically disabled by the platform when Windows is in High Contrast mode — no extra detection code needed for that case.

### 2. Modern flat control look

- Set `Button.FlatStyle = FlatStyle.System` rather than `FlatStyle.Flat` — a tracked WinForms bug (`dotnet/winforms#13897`) means `FlatAppearance` border/hover/pressed-color properties don't reliably apply once dark mode is active, producing visually broken buttons. `FlatStyle.System` lets the platform draw a themed, flatter button correctly in both light and dark automatically.
- `GroupBox` always renders a beveled 3D border regardless of color mode (there's no flat variant) — replace with a `Panel` (flat 1px border, themed color) + a `Label` acting as the group caption; this is a standard WinForms UX substitution pattern, not a new dependency.
- Prefer letting `SystemColorMode.System`'s own color resolution drive backgrounds/foregrounds over hardcoded `SystemColors.Control`/`SystemColors.ControlText` references left over from the original default-styled forms — those hardcoded references are exactly what will *not* re-theme.

### 3. Tray icon redesign (rig mode vs. normal mode pair)

- **Design workflow:** author each icon as SVG vector art (Inkscape or Figma) so it can be regenerated cleanly at every target size, then export/rasterize to individual PNGs per size rather than relying purely on one auto-downscaled source — icon-design best practice is to hand-simplify the smallest (16×16) frame specifically, since blind downscaling from a large source tends to blur or muddy fine detail exactly at tray size, which directly undercuts this milestone's "real visual distinction" goal.
- **Minimum required sizes (official Win32 guidance, confirmed directly against Microsoft's Notification Area docs):** provide both a **16×16** and a **32×32** frame in the `.ico`; the shell uses `LoadIconMetric` to pick/scale the right one for the current DPI.
- **Recommended fuller size set (general Windows icon-design convention, not a single official spec — MEDIUM confidence):** also include 20×20, 24×24, 40×40, 48×48, and a 256×256 (PNG-compressed, supported since Vista) frame — the mid-range sizes cover 125%/150%/175%/200% DPI scale steps common on modern displays without the shell falling back to a blurry upscale of the 16px frame; 256×256 keeps the same icon looking correct anywhere Windows surfaces it larger (Alt-Tab, Explorer "Large icons" view).
- **Packing command:** `magick icon-16.png icon-20.png icon-24.png icon-32.png icon-40.png icon-48.png icon-256.png rig-mode.ico` (list pre-rendered PNGs directly — best quality) or, for a faster single-source pass, `magick source.png -define icon:auto-resize="256,48,40,32,24,20,16" rig-mode.ico` (auto-downscales from one large PNG — acceptable but review/hand-touch the 16px result).
- **Integration:** replace the two existing embedded `.ico` files' contents in place — no change to the existing packaging/embedding mechanism (`<ApplicationIcon>`/embedded resource, whichever the app currently uses) is needed; this is a resource-content swap, not a new build step.

## Alternatives Considered (v1.2 additions)

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| Built-in `Application.SetColorMode(SystemColorMode.System)` + hand-rolled `SystemEvents`/DWM P/Invoke for live title-bar updates | [DarkNet](https://github.com/Aldaviva/DarkNet) (NuGet, MIT) | DarkNet is a small library purpose-built for exactly the one gap Microsoft's own API admits it doesn't solve: it exposes an `EffectiveCurrentProcessThemeIsDarkChanged` event and handles title-bar-only live updates for you, including on some older Windows 10 builds. Worth reaching for **only if** the hand-rolled combination proves flakier in practice than expected — for a 2-form app, hand-rolling first matches this project's established "no heavy third-party dependency" bias (v1.0/v1.1 both chose hand-rolled P/Invoke over wrapper packages for the same reason), and the amount of code is genuinely small. |
| Manual `FlatStyle`/`Panel`-based redesign (no theming suite) | Krypton Toolkit (`Krypton.Toolkit`, actively maintained fork supporting .NET 8–10, free) or MetroFramework (Windows-8-era "Metro" look, appears unmaintained) | Only if the visual-polish ambition grows well beyond "modern flat + theme-aware" into a fully componentized custom design system — these suites replace `Button`/`GroupBox`/etc. with their own control types across every form, a much larger integration change (retrofit two existing forms' designer files, new control-library dependency, a separate theming API from `SystemColorMode`) than this milestone's scope calls for. Not recommended here. |
| ImageMagick CLI for `.ico` packing | Online converters (icoconvert.com, redketchup.io/icons8, convertio.co) | Fine as a one-off manual fallback if installing ImageMagick isn't convenient, but not scriptable/repeatable — worse fit if the icon design goes through a few iterations before landing. |

## What NOT to Use (v1.2 additions)

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Relying on `Application.SetColorMode` alone for "follows the theme live" | Its own Microsoft Learn documentation states the app will **not** automatically adapt if the OS theme changes while running — fails this milestone's explicit "live" requirement if left as the only mechanism | Add the `SystemEvents.UserPreferenceChanged` + re-apply (`SetColorMode` + `DwmSetWindowAttribute` + `Refresh()`) pattern described above |
| `Button.FlatStyle = FlatStyle.Flat` as the default under dark mode | Tracked WinForms bug (`dotnet/winforms#13897`): `FlatAppearance` colors don't reliably apply once dark mode is active, producing visually broken buttons rather than a clean flat look | `FlatStyle.System`, or `FlatStyle.Flat` only with colors set explicitly per theme state and verified visually in both modes |
| A full WPF or MAUI rewrite for nicer theming/data-binding | The app is a small, working 4-project WinForms solution with a self-contained single-file publish pipeline proven across three shipped milestones; .NET 10's built-in WinForms dark mode closes the main capability gap that would have motivated a framework switch. A rewrite is far outside a "visual polish" milestone's scope | Stay in WinForms; use the built-in dark-mode feature |
| Krypton Toolkit / MetroFramework / other full WinForms theming suites as the *primary* mechanism | Forces every existing control on `MainForm`/`SettingsForm` to be swapped for suite-specific types, adds nontrivial dependency surface for a 2-form app, and duplicates capability .NET 10 already provides natively | Built-in `Application.SetColorMode` + targeted `FlatStyle`/`Panel`-based manual redesign of the specific controls (GroupBox → bordered Panel+Label, etc.) that need a flatter look than stock WinForms gives them |
| Bundling NirSoft-style or shelling out to third-party icon-conversion binaries | Same class of problem already flagged elsewhere in this file for other domains (licensing friction, not scriptable/repeatable, no benefit over an in-toolchain solution) | ImageMagick CLI — free, scriptable, standard tool for this exact `.ico`-packing job |

## Stack Patterns by Variant (v1.2 additions)

**If the app must still look correct on Windows 10 (not just Windows 11):**
- `Application.SetColorMode`'s dark mode is Windows-11-only by documentation; budget for a "title bar goes dark (via the raw DWM call, community-reported to work on some Win10 builds), controls stay light" visual inconsistency on Windows 10 rather than trying to fully backport control theming — not worth the effort unless Windows 10 support becomes an explicit requirement (it isn't, per this project's context: personal rig, effectively Windows 11).

**If DPI scaling matters for the tray icons (it does):**
- Minimum: 16×16 + 32×32 frames (official Win32 guidance). Recommended: also 20/24/40/48/256 for clean rendering across 125–200% DPI scale steps and non-tray surfaces (Alt-Tab, Explorer).

## Version Compatibility (v1.2 additions)

| Package/API | Compatible With | Notes |
|-----------|------------------|-------|
| `Application.SetColorMode` (`System.Windows.Forms`, .NET 10) | net10.0-windows, `UseWindowsForms=true` (already the project's TFM) | No project-file changes needed — part of the WinForms assembly already referenced. Confirmed non-experimental (no `WFO5001` opt-in required) as of .NET 10 GA per the official "What's new in WinForms for .NET 10" doc. |
| `Application.SetColorMode(SystemColorMode.Dark/System)` | Windows 11 only (falls back to light on Windows 10 and earlier); automatically disabled in High Contrast mode | Confirmed directly in the official API remarks (`learn.microsoft.com/dotnet/api/system.windows.forms.application.setcolormode`). |
| `DwmSetWindowAttribute` + `DWMWA_USE_IMMERSIVE_DARK_MODE` (value `20`) | Windows 10 20H1+ (undocumented value `19` on earlier Win10 builds) / Windows 11 (documented) | Only the modern value (`20`) is worth supporting given this project's Windows 10/11-only, no-legacy-OS context. |
| ImageMagick `.ico` packing | Any OS (Windows, Linux, macOS) | Works fine off the Windows rig for producing the `.ico` files even though the app itself only runs on Windows. |

## Sources (v1.2 additions)

- https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100 — confirmed dark mode and `Application.SetColorMode` are non-experimental/GA in .NET 10 (fetched directly) — HIGH confidence
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setcolormode?view=windowsdesktop-10.0 — confirmed method signature, `SystemColorMode` values, Windows-11-only limitation, High Contrast interaction, and the explicit "will not automatically adapt to the new setting" live-update gap (fetched directly) — HIGH confidence
- https://learn.microsoft.com/en-us/windows/win32/shell/notification-area — confirmed official 16×16/32×32 minimum tray icon size guidance and `LoadIconMetric` usage (fetched directly) — HIGH confidence
- https://ironsoftware.com/academy/csharp-framework/dotnet10-dark-mode-winforms/ — practical caveats (call `SetColorMode` before control creation; MessageBox stays light; VS designer doesn't preview dark mode; restart/live-update gap) — MEDIUM confidence, corroborates the official docs' live-update gap
- https://github.com/dotnet/winforms/issues/13897 — confirmed `FlatStyle.Flat` + dark mode `FlatAppearance` bug — MEDIUM confidence (open GitHub issue, not yet fixed per available information)
- https://github.com/Aldaviva/DarkNet — confirmed scope (title-bar-only live theming, MIT, supports WinForms/WPF/raw HWND) as the alternative to hand-rolling live title-bar updates — MEDIUM confidence (README-level fetch, not independently tested)
- https://github.com/Krypton-Suite/Standard-Toolkit — confirmed Krypton Toolkit is actively maintained and supports .NET 8–10, used to justify the "what not to use as primary mechanism" rationale rather than dismissing it as abandoned — MEDIUM confidence
- ImageMagick `icon:auto-resize` define (community docs/gists, cross-checked against ImageMagick's own discourse/GitHub issue threads) — confirmed CLI syntax for multi-resolution `.ico` packing — MEDIUM confidence (community-sourced, but consistent across multiple independent threads)
- `.planning/PROJECT.md` — milestone framing for v1.2 (theming + icon redesign scope) and current architecture (4-project solution, net10.0-windows, self-contained single-file publish) — used as ground truth for integration context

---

# v1.1 Milestone Additions (2026-07-26)

Scope: tray residency + autostart, global hotkey trigger, CLI trigger + single-instance IPC signaling, toast/status notification, and multi-monitor independent enable/disable (including enabling a monitor that's been OS-disabled for an extended period). This section covers **only** what's new for v1.1 — the audio and single-monitor disable/restore mechanisms below (v1.0 Original Stack) are already shipped and validated; they are not re-researched here.

## Headline Finding

**No new third-party NuGet packages are required for this milestone.** Every one of the five target features is achievable with: (a) WinForms built-ins already available via `UseWindowsForms=true`, (b) BCL types already available on `net10.0-windows` (`System.IO.Pipes`, `Microsoft.Win32.Registry`, `System.Threading.Mutex`), (c) hand-rolled `user32.dll` P/Invoke extending the pattern this project already uses for window control, and (d) the **already-referenced** `WindowsDisplayAPI` 1.3.0.13 package used in a broader mode (querying and applying inactive paths, not just active ones). This is consistent with the project's established preference (see CLAUDE.md Stack Patterns) for hand-rolled Win32 interop over heavier SDKs — v1.1 adds zero new dependency-risk surface.

## Recommended Stack Additions

### Core Technologies (mechanisms, not packages — all already available in the existing `net10.0-windows` + WinForms project)

| Technology | Version/Source | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `System.Windows.Forms.NotifyIcon` | Built into WinForms (net10.0-windows) | Tray icon, context menu, minimize-to-tray, balloon/toast notification | Already the framework's native tray primitive — one component does triple duty for TRAY-01 (icon+menu), the minimize-to-tray behavior (hide `Form`, keep `NotifyIcon.Visible=true`), and NOTIF-01 (`ShowBalloonTip`, see below). No package needed. |
| `user32.dll` P/Invoke: `RegisterHotKey`/`UnregisterHotKey` + `WM_HOTKEY` | Win32 API (winuser.h), stable since Windows 2000 | Global hotkey trigger that works with no visible window | Same hand-rolled P/Invoke pattern this project already uses for `ShowWindow`/`IsIconic` (Phase 3). Requires a window handle to receive `WM_HOTKEY` — since v1.1 makes the app tray-resident (no window shown most of the time), you need a **message-only or hidden `NativeWindow`** purely to own that handle; this is a new small component but zero new dependency. Must be registered/unregistered on the same thread that owns the window (the UI thread's message loop, which a tray-resident WinForms app already keeps alive via `Application.Run`). |
| `Microsoft.Win32.Registry` (`Registry.CurrentUser`) | BCL, available directly on `net10.0-windows` (the separate `Microsoft.Win32.Registry` NuGet package is only required for netstandard/non-Windows-TFM projects, not needed here) | Autostart-on-boot registration | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` is the correct mechanism for this app's profile — see Autostart Mechanism Comparison below. |
| `System.Threading.Mutex` (named) + `System.IO.Pipes` (`NamedPipeServerStream`/`NamedPipeClientStream`) | BCL | Single-instance detection + CLI-to-running-instance IPC signaling | Standard, current (.NET 6+ through .NET 10) pattern for exactly this scenario: a CLI invocation (new process) needs to detect a tray-resident instance is already running and hand it a command. No dependency; async-friendly; testable without a real named pipe by abstracting the transport behind an interface for `RigToggle.Tests`. |
| `WindowsDisplayAPI` 1.3.0.13 (falahati) — **already referenced**, used in a new mode | NuGet (LGPLv3) | Query full topology including OS-disabled (inactive) targets, and (re-)apply arbitrary subsets active/inactive | `PathInfo.GetAllPaths(onlyActivePaths: false)` (wraps `QueryDisplayConfig` with `QDC_ALL_PATHS`) is confirmed (Microsoft's own docs, verified directly, see Sources) to return inactive paths alongside active ones — this is the mechanism v1.1's multi-monitor enable/disable sets need. See the dedicated risk section below — this is the one area needing empirical hardware validation, not just doc-confirmation. |

No new Supporting Libraries or Development Tools — everything above uses BCL or the existing `WindowsDisplayAPI` package, and the existing `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true` profile is unchanged (see v1.0 Packaging section below, still fully in effect).

## Feature-by-Feature Detail

### 1. Tray residency + autostart

- **Tray + minimize-to-tray**: `NotifyIcon` with a `ContextMenuStrip` (Switch to Rig/Normal Mode, Settings, Exit). On `MainForm`'s `FormClosing`, if the close reason is user-initiated (not `Application.Exit()`/Exit menu item), set `e.Cancel = true` and `Hide()` instead — standard WinForms idiom, no library needed.
- **Autostart mechanism comparison** (confirmed via howtoguides.org + Microsoft Learn cross-reference, MEDIUM-HIGH confidence, corroborated by multiple sources):

  | Mechanism | Elevation | Toggle complexity | Fit for this app |
  |---|---|---|---|
  | **Registry Run key** (`HKCU\...\Run`) | None — runs at the same non-elevated (asInvoker) token as the logged-in user, matching this app's existing execution model | Trivial: `SetValue`/`DeleteValue` of one string value, read-back to drive a Settings checkbox | **Recommended.** Matches "personal single-user tool," matches existing non-elevated execution, and the value (a single path string) is exactly what Windows' own Settings → Startup Apps page also manages for Run-key entries, so the user's mental model stays consistent. |
  | Task Scheduler ("At log on" trigger) | Can run elevated or as SYSTEM if configured that way — adds an elevation-consistency risk this app doesn't otherwise have | Higher: requires the Task Scheduler COM API or `schtasks.exe`, plus a task name to create/query/delete | Only worth it for conditional triggers (network availability, delay-on-boot) this app doesn't need. Not recommended as primary. |
  | Startup folder shortcut | None | Requires writing a `.lnk` (COM `IShellLink`, no BCL wrapper) rather than a plain value | Functionally equivalent to the Registry Run key but more code (shortcut creation) for no added benefit here. Not recommended. |

  Use `Environment.ProcessPath` (or `Process.GetCurrentProcess().MainModule!.FileName`) to get the absolute path of the running single-file .exe when writing the Run value — this resolves correctly for a self-contained single-file publish (confirmed pattern; no separate host/dll path to worry about since v1.0's Phase 5 packaging already produces one file).

### 2. Global hotkey trigger

- Confirmed pattern (Microsoft docs + community examples, HIGH confidence for the mechanism): a class inheriting `NativeWindow`, created via `CreateHandle(new CreateParams())` (no visible UI needed — a message-only window, or simply the existing hidden `MainForm`'s handle, works equally well), overrides `WndProc` to intercept `WM_HOTKEY` (`0x0312`), and calls `RegisterHotKey(handle, id, modifiers, vk)` / `UnregisterHotKey(handle, id)` from `user32.dll`.
- **Threading constraint** (Microsoft docs, HIGH confidence): `RegisterHotKey` must be called from the thread that owns the window and that is pumping messages — for a WinForms app this is the UI thread, which a tray-resident app keeps alive regardless of whether `MainForm` is visible (as long as `Application.Run` is still executing, e.g. via `ApplicationContext`). No thread-marshalling concerns beyond what a normal WinForms event handler already has.
- Integration point: house this in a small `HotkeyWindow : NativeWindow` component (new, in `RigToggle.Windows` alongside the existing user32 P/Invoke code) that the App composition root creates once at startup and keeps alive for the process lifetime, wiring its hotkey-fired event to the same toggle-invocation path the tray menu and CLI trigger use.

### 3. CLI trigger + single-instance IPC signaling

- **Pattern** (confirmed via multiple independent sources — AutoItConsulting, CodeProject, and a 2023–2026-updated .NET 6/7+ writeup, MEDIUM-HIGH confidence, cross-verified): a named `Mutex` (e.g. `Local\RigToggle.SingleInstance` — use `Local\` since this is a per-user desktop tool, not `Global\` which is for cross-session/service scenarios) determines whether this is the first (tray-resident, long-lived) instance or a subsequent (CLI, short-lived) one.
  - First instance: acquires the mutex, starts a `NamedPipeServerStream` listener on a background thread/`Task`, and runs its normal tray-resident life cycle.
  - Subsequent instance (e.g. `RigToggle.exe --rig`): fails to acquire the mutex, opens a `NamedPipeClientStream` to the same pipe name, writes the parsed command (a small JSON payload — reuse `System.Text.Json`, already a dependency), and exits immediately.
- **Critical gotcha** (confirmed directly from a maintainer's dedicated write-up, MEDIUM-HIGH confidence): set `PipeOptions.CurrentUserOnly` on **both** the server and client streams. Without it, message delivery can silently fail cross-session/cross-token even when both processes run as the same interactive user — this is a real, previously-hit gotcha, not a theoretical one, so treat it as a required flag, not an optional hardening step.
- Listen on a background thread and marshal the received command back to the UI thread (`SynchronizationContext.Post` or `Control.Invoke`) before invoking `ToggleService` — never block the UI thread waiting on the pipe.
- Integration point: lives in `RigToggle.App` (process/CLI-argument concerns belong at the composition-root/entry-point level, not in `RigToggle.Core`'s domain logic or `RigToggle.Windows`'s device adapters). Abstract the transport behind a small interface (e.g. `IToggleCommandListener`/`IToggleCommandSender`) so `RigToggle.Tests` can exercise the command-parsing/dispatch logic without spinning up real OS pipes.

### 4. Toast/status notification

- **Recommendation: `NotifyIcon.ShowBalloonTip(timeout, title, text, ToolTipIcon)`.** Confirmed (Microsoft Learn API docs, HIGH confidence) as a zero-dependency call on the same `NotifyIcon` component already required for tray residency — no new package, no AUMID/shortcut/COM-activator setup.
  - Confirmed behavior on modern Windows (MEDIUM confidence, multiple corroborating community/Microsoft Q&A sources): since Windows 10, the shell renders `ShowBalloonTip` output using the modern toast-notification visual style automatically — you get toast-like presentation without writing toast-specific code. One caveat worth noting: on Windows 11, the balloon appears but its text has been reported not to persist into Notification Center history — acceptable here since this is a transient "did it work" confirmation, not a message the user needs to retrieve later.
  - `timeout` is a legacy parameter now ignored by the OS (Microsoft Learn: "Notification display times are now based on system accessibility settings") — pass any value, don't rely on it.
- **Explicitly NOT recommended for this project: Windows App SDK `AppNotificationManager`.** This is what current (2026) Microsoft guidance points to for *new, rich, interactive* toast work, and it does work from unpackaged apps — but it requires taking a dependency on `Microsoft.WindowsAppSDK`, which pulls in the Windows App Runtime (either as an installed prerequisite or a much larger self-contained bundle) and a `Bootstrap.Initialize` step. That directly conflicts with this project's "standalone single .exe, no separate runtime" constraint and its stated preference for minimal dependencies over heavier SDKs (the same reasoning that ruled out NirSoft external processes and unmaintained `AudioSwitcher.AudioApi` in the v1.0 stack below applies here). For a "lightweight on-toggle confirmation," it is disproportionate.
- **Also not recommended: `CommunityToolkit.WinUI.Notifications`** (formerly `Microsoft.Toolkit.Uwp.Notifications`) — functionally would have worked for an unpackaged WinForms app (build toast XML, no COM activator needed for non-interactive toasts), but the package is now archived (confirmed via NuGet/GitHub, MEDIUM confidence) — don't start a new dependency on an archived package when `ShowBalloonTip` already covers the requirement with zero dependencies.

### 5. Multi-monitor independent enable/disable (the genuinely new-risk item)

**What's proven (v1.0 Phase 4, do not re-verify):** disabling one specific already-active monitor via `ApplyPathInfos` (topology-path-removal), and restoring it within the same app session from a full-topology snapshot captured immediately before disabling.

**What's new in v1.1:** enabling a monitor that may have been OS-disabled for an extended period — potentially since before the current app session, possibly since before the last reboot — using only what `WindowsDisplayAPI`/CCD can tell you about it *now*, not a same-session snapshot.

**Findings** (verified directly against Microsoft's official `QueryDisplayConfig`/`SetDisplayConfig` reference docs, HIGH confidence for the API contract itself):

- `QDC_ALL_PATHS` (what `PathInfo.GetAllPaths(onlyActivePaths: false)` wraps, confirmed via direct source read of `PathInfo.cs`) **does** return inactive paths — "If QDC_ALL_PATHS is set ... QueryDisplayConfig returns all the inactive paths after the active paths" (Microsoft Learn, verbatim).
- However: **"For inactive paths, returned source and target mode information is not available; therefore, the target information in the path structure is set to default values, and the source and target mode indexes are marked as invalid"** (Microsoft Learn, verbatim). This means a long-disabled monitor's `PathInfo` is real and enumerable, but you do **not** get its previous resolution/position/orientation back from this query alone — that information either has to come from Windows' own CCD *persistence database* (a separate, registry-backed store keyed by adapter+target identity, distinct from this app's own JSON snapshot) or be recomputed by "best mode logic."
- `SetDisplayConfig`'s own documentation describes exactly this recovery path: when mode indexes are invalid and `SDC_ALLOW_CHANGES` is set (which is what this project's `ApplyPathInfos` already uses, per source-verified flag usage: `SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES`), the system "uses best mode logic to determine the source mode information" rather than failing. This is the mechanism that should make "mark an inactive path active and re-apply" work in principle.
- Separately, `SDC_TOPOLOGY_SUPPLIED` mode explicitly documents a two-call fallback pattern: try with just path data (queries the persistence database for that path's last-known mode); if that path has no database entry, the documented remedy is to retry with `SDC_USE_SUPPLIED_DISPLAY_CONFIG` instead, which forces best-mode-logic computation. `WindowsDisplayAPI`'s existing flag choice already lands on the side that always uses best-mode logic, so it should not hit the "no database entry" failure mode at all — it just may compute a *different* mode than the monitor's own previous one if best-mode logic guesses differently than what's in the (separate, OS-level, not app-level) persistence database.

**Confidence assessment — MEDIUM, not HIGH, and here's why it isn't higher:**
1. The API contract clearly supports re-activating an inactive path without physically-supplied mode info, via best-mode logic. This part is HIGH confidence (verified against two official reference pages).
2. What is **not** verifiable from documentation alone: (a) whether "best mode logic" reliably reconstructs the monitor's actual native resolution/refresh/position rather than some fallback default, particularly for a monitor that's been inactive across a full Windows reboot (not just app-session inactive) — this depends on graphics-driver behavior, exactly the kind of variable that made v1.0's Phase 1 a dedicated hardware spike rather than a docs-only decision; (b) whether the physical monitor being "disabled to save power" means OS-CCD-disabled-but-still-connected-via-cable (EDID stays readable, target stays enumerable) versus the monitor's own physical power button being off (EDID/DDC probing can fail, and the target may not even enumerate as a valid re-activatable path) — these require genuinely different handling, and the milestone's own framing ("normally kept OS-disabled to save power") suggests the former, but this should be confirmed empirically, not assumed.
3. Recommendation: **treat this exactly like Phase 1 of v1.0** — a small, throwaway verification step (or an early plan within whichever phase handles multi-monitor) that calls `GetAllPaths(onlyActivePaths: false)` on the rig hardware after an actual reboot with the rig monitor OS-disabled, re-applies it via `ApplyPathInfos`, and confirms both (a) it comes back at all and (b) it comes back at the expected resolution/position — before committing the full multi-monitor UI/settings work on top of an unverified assumption.

## Alternatives Considered (v1.1 additions)

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| Registry Run key for autostart | Task Scheduler ("At log on" trigger) | If you later need conditional triggers (start delay, "only if on AC power," run whether or not a user is logged in) — none of which this personal single-user tray app needs today. |
| `NotifyIcon.ShowBalloonTip` for toast | Windows App SDK `AppNotificationManager` | Only if this app later needs rich, interactive, actionable toast buttons (e.g. "Undo" inline) or must guarantee Notification Center history — accept the Windows App Runtime dependency and the departure from "zero new dependencies" only if that becomes an explicit requirement. |
| Named `Mutex` + `System.IO.Pipes` for single-instance IPC | `FindWindow` + `SendMessage(WM_COPYDATA)` on the app's own hidden window | Viable since this project already does hand-rolled `user32.dll` P/Invoke and will already have a hidden `NativeWindow` for the hotkey — reusing it for `WM_COPYDATA` avoids introducing pipes at all. Slightly less testable (depends on a stable window class name you control, which is fine here) and less idiomatic in current .NET; named pipes are recommended as the primary choice for cleaner test isolation via `RigToggle.Tests`, but this is a legitimate, equally low-dependency alternative if it's preferred for architectural consistency with the existing NativeWindow/hotkey component. |
| `WindowsDisplayAPI.PathInfo.GetAllPaths(onlyActivePaths: false)` + `ApplyPathInfos` (`SDC_ALLOW_CHANGES`) for enabling a long-disabled monitor | Raw P/Invoke of `SDC_TOPOLOGY_SUPPLIED` two-call fallback pattern | Only if the `WindowsDisplayAPI` wrapper's fixed flag choice turns out (via the recommended hardware spike) to not reliably restore the correct mode — in that case, dropping to raw `SetDisplayConfig` calls gives finer control over which of the two documented recovery paths (persistence-database lookup vs. best-mode-logic) is attempted first. Only pursue this if the spike shows a real problem; don't pre-emptively add ~300+ lines of hand-rolled CCD struct marshalling without evidence it's needed. |

## What NOT to Use (v1.1 additions)

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Windows App SDK / MSIX packaging for toast notifications | Adds a large runtime dependency (Windows App Runtime) and a packaging model shift that directly conflicts with this project's standalone-single-.exe, no-separate-runtime constraint | `NotifyIcon.ShowBalloonTip` |
| `CommunityToolkit.WinUI.Notifications` | Package is archived — don't start a new dependency on an unmaintained/archived package (same reasoning this project already applied to ruling out `AudioSwitcher.AudioApi`) | `NotifyIcon.ShowBalloonTip` |
| Task Scheduler as the *primary* autostart mechanism | Adds elevation-consistency risk and API complexity (COM Task Scheduler API or `schtasks.exe` shelling out) this personal single-user non-elevated tool doesn't need | Registry `HKCU\...\Run` key |
| Assuming a long-OS-disabled monitor's mode info is fully recoverable from `GetAllPaths(onlyActivePaths:false)` alone | Confirmed by Microsoft's own docs: inactive paths return with mode indexes marked invalid — the resolution/position is NOT included in that query's result | Rely on `ApplyPathInfos`'s existing `SDC_ALLOW_CHANGES` best-mode-logic fallback, and validate on real hardware via a dedicated spike before shipping, exactly as Phase 1 did for the disable direction |
| Treating "OS-disabled to save power" and "physically powered off via the monitor's own button" as the same scenario | EDID/DDC probing can behave differently (target may not even enumerate as re-activatable) when a monitor is truly powered off vs. merely CCD-disabled while still cabled/powered | Confirm which scenario the user actually means during the hardware spike, and handle "target not available" as an explicit, reported failure mode rather than assuming success |

## Stack Patterns by Variant (v1.1 additions)

**If the hotkey window and the IPC/pipe listener both need a live message pump:**
- A single hidden/message-only `NativeWindow` (or the existing `MainForm`, kept alive but hidden) can serve double duty as the `WM_HOTKEY` receiver, since both concerns just need "a window handle that's alive for the process lifetime on the UI thread" — no need for two separate windows.

**If CLI arguments are passed with no other instance running:**
- Skip the pipe entirely and invoke `ToggleService` directly from `Program.cs` before entering `Application.Run` — only fall back to the mutex-check + pipe-client path when the mutex acquisition fails (i.e., another instance is confirmed already running).

## Version Compatibility (v1.1 additions)

| Package/API | Compatible With | Notes |
|-----------|------------------|-------|
| `WindowsDisplayAPI` 1.3.0.13 | `GetAllPaths(onlyActivePaths: false)` on net10.0-windows | Same package/version already in use; no version bump needed — the inactive-path query capability is present in the already-referenced version (confirmed by reading `PathInfo.cs` directly). |
| `System.IO.Pipes` (`PipeOptions.CurrentUserOnly`) | .NET 6.0+ through .NET 10 | Flag has been available since the .NET Core 3.0/.NET 6+ era API consolidation; no compatibility concern on .NET 10. |
| `NotifyIcon.ShowBalloonTip` | All WinForms versions through the `windowsdesktop-11.0` moniker (per Microsoft Learn's version-range metadata) | Stable, unchanged API surface; safe against future .NET/WinForms updates within this project's timeline. |

## Sources (v1.1 additions)

- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig — official CCD reference; confirmed verbatim language on `QDC_ALL_PATHS` returning inactive paths and their mode-index-invalid limitation — HIGH confidence (fetched directly, dated 2026-05-15 update)
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig — official CCD reference; confirmed `SDC_ALLOW_CHANGES`/`SDC_USE_SUPPLIED_DISPLAY_CONFIG`/`SDC_TOPOLOGY_SUPPLIED` best-mode-logic recovery behavior for paths with invalid mode indexes — HIGH confidence (fetched directly)
- https://github.com/falahati/WindowsDisplayAPI/blob/master/WindowsDisplayAPI/DisplayConfig/PathInfo.cs — confirmed `GetAllPaths(virtualModeAware)` maps to `QueryDeviceConfigFlags.AllPaths` and `ApplyPathInfos` signature — HIGH confidence (source read directly)
- https://github.com/falahati/WindowsDisplayAPI/blob/master/WindowsDisplayAPI/Native/DisplayConfig/QueryDeviceConfigFlags.cs — confirmed `AllPaths`/`OnlyActivePaths`/`DatabaseCurrent` flag semantics — HIGH confidence (source read directly)
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip?view=windowsdesktop-10.0 — confirmed API surface, deprecated `timeout` parameter behavior, and current version-range applicability — HIGH confidence (fetched directly)
- Microsoft Q&A / community discussion on `ShowBalloonTip` rendering as a toast-style notification since Windows 10 and Windows 11 Notification Center history caveat — MEDIUM confidence (community-sourced, not an official statement, but consistent across multiple independent reports)
- https://learn.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/toast-desktop-apps — confirmed unpackaged-app AUMID/shortcut requirement for the older toast API, and that Windows App SDK's `AppNotificationManager` is current (2026) guidance for new toast work — MEDIUM-HIGH confidence
- CommunityToolkit.WinUI.Notifications NuGet listing and related GitHub issue threads — confirmed the package is archived — MEDIUM confidence
- https://medo64.com/posts/single-instance-application-for-net-6-or-7 — confirmed the Mutex + NamedPipeServerStream/ClientStream pattern for .NET 6/7+ single-instance apps, and the `PipeOptions.CurrentUserOnly` gotcha (fetched directly) — MEDIUM-HIGH confidence
- https://www.autoitconsulting.com/site/development/single-instance-winform-app-csharp-mutex-named-pipes/ and https://www.codeproject.com/Tips/171776/Single-application-instance-using-Mutex-and-WCF-na — corroborating independent sources for the same Mutex+named-pipe pattern — MEDIUM confidence
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey — official reference confirming `RegisterHotKey`/`WM_HOTKEY`/thread-ownership requirements — HIGH confidence
- Community examples (lostindetails.com, sudhirdotnet blog) demonstrating `NativeWindow`-based hidden-window pattern for `WM_HOTKEY` in WinForms — MEDIUM confidence, corroborates the officially-documented threading requirement
- howtoguides.org autostart-methods comparison, cross-checked against Microsoft's own Startup Apps documentation concepts — MEDIUM confidence (used for the comparison table, not for any capability claim not otherwise verifiable)
- `.planning/PROJECT.md` and `.planning/milestones/v1.0-ROADMAP.md` (this repo) — used to establish exactly what Phase 1–5 already validated (single-monitor same-session disable/restore) versus what's new in v1.1 (multi-monitor, cross-session/reboot enable)

---

# v1.0 Original Stack (Foundational — shipped, validated, unchanged)

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET | 10.0 (LTS, released 2025-11-11, supported to 2028-11-14) | Runtime/BCL | Current LTS. Self-contained publish is a first-class, well-documented deployment mode (unchanged mechanism since .NET 6), and .NET has the richest Win32 interop story (P/Invoke, COM interop) of any managed stack — required here since all three core features (CCD display API, audio COM interface, Win32 window control) are native Windows APIs with no cross-platform equivalent. Picking .NET also gives you 3 years of security patching without a rewrite. |
| C# | 13 (ships with .NET 10 SDK) | Language | Default with .NET 10; no reason to use F# or VB for this — P/Invoke and COM interop snippets found in the wild (NirSoft/EarTrumpet/AudioSwitcher source) are all C#, so following that reduces translation effort. |
| Windows Forms (WinForms) | .NET 10 (`Microsoft.NET.Sdk` with `UseWindowsForms=true`) | GUI framework | For a 2-screen personal utility (toggle view + settings view), WinForms is the lowest-friction choice: smallest self-contained publish footprint of the three Windows GUI stacks, no XAML compiler step, trivial to bind `ComboBox`/`ListBox` to enumerated monitors/audio devices/processes. WPF is a reasonable alternative (see Alternatives) if you want nicer visuals, but it adds no capability you need here. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `WindowsDisplayAPI` (falahati) | 1.3.0.13 (NuGet) | Managed wrapper around the Windows CCD (Connecting and Configuring Displays) API — `QueryDisplayConfig`/`SetDisplayConfig` | Use for both snapshotting the current display topology (`PathInfo.GetActivePaths()` / `GetAllPaths()`) and applying a new one (`PathInfo.ApplyPathInfos(...)`) that omits the target monitor's path — this is the actual mechanism that performs a true CCD-level "detach", not a power-off. Verified by reading the library source directly (see Sources): `ApplyPathInfos` calls `SetDisplayConfig` with `SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES` (or `SDC_TOPOLOGY_SUPPLIED` when no mode info is given), which is exactly the documented pattern for detaching a target from a source path. |
| Custom `IPolicyConfig` COM interop (hand-written, ~100 lines) | N/A — embed directly in project, not a NuGet dependency | Set the Windows default audio **playback** (render) device | This undocumented COM interface (`IPolicyConfig` / CLSID `{870af99c-171d-4f9e-af0d-e63df40c2bc9}`) is the *only* way to programmatically change the system default audio endpoint — there is no public WASAPI/Core Audio API for it. It is what every default-audio-switcher tool uses under the hood (NirSoft SoundVolumeView — actively updated, v2.53 as of July 2026, explicitly supports Windows 11 — and EarTrumpet, whose `IPolicyConfig.cs` interop file is public on GitHub). The interface GUID has been stable since Windows 8/8.1 (no separate "Vista" fallback needed since you only target Windows 10/11). Embed the interop code directly (copy the ~100-line file, e.g. from `File-New-Project/EarTrumpet` or `tartakynov/audioswitch`, both permissively usable as interop shims) rather than taking a package dependency — the wrapper is tiny and stable, and this avoids depending on a stale package (see AudioSwitcher.AudioApi below). |
| NAudio | 2.3.0 | Enumerate audio render endpoints (friendly names, device IDs, default device query) via `MMDeviceEnumerator` | Use for everything *except* the actual "set default" call: listing playback devices for the settings dropdown, resolving a saved device ID back to a live device at toggle-time, and reading the *current* default device before switching (for state restore). Actively maintained (v2.3.0, updated March 2026), MIT licensed, the de facto standard .NET audio library. Its `MMDeviceEnumerator`/`MMDevice` wrap the same public `IMMDeviceEnumerator` Core Audio API that Windows itself uses for enumeration — fully documented and stable, unlike the "set default" call. |
| `System.Text.Json` | Included in .NET 10 BCL | Persist user settings (selected monitor, audio device pair, app path) to a JSON file | No extra package needed. Serialize a small settings POCO to `%APPDATA%\RigToggle\settings.json`. Simpler and more transparent/debuggable than the Windows Registry or `app.config`/`user.config` (which are awkward for values the user should be able to hand-edit or that get regenerated per-publish). |
| Win32 P/Invoke (hand-written `DllImport`s) | N/A | Focus/minimize the Moza Companion window; detect if it's already running | You only need a handful of calls: `Process.GetProcessesByName` (BCL, no P/Invoke) to detect if it's running, `Process.MainWindowHandle` to get its window handle, then `user32.dll` `ShowWindow` (`SW_MINIMIZE` / `SW_RESTORE`), `IsIconic`, and `SetForegroundWindow` to focus it. This is ~15 lines of `DllImport` — not worth taking a dependency like `PInvoke.User32` (a NuGet package that wraps the same signatures) unless you want IntelliSense/typed constants; either is fine, but hand-rolling avoids a dependency for something this small. Note: post-ship, the app moved away from `SetForegroundWindow`/`ShowWindow` manipulation of a *third-party* window (see PROJECT.md Key Decisions — this caused the H9 regression); the P/Invoke calls remain valid for this project's *own* hidden window (hotkey/tray), just not for driving another app's window. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| Visual Studio 2022 (17.14+) or `dotnet` CLI + VS Code | Build/publish | Either works; `dotnet publish` is what actually produces the standalone .exe (see Installation below) and is scriptable, so prefer driving the final build via CLI even if you develop in VS Code. |
| `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true` | Standalone .exe packaging | This is the mechanism that satisfies the "no separate runtime install" constraint — see dedicated section below. |

## Standalone .exe Packaging (Self-Contained Deployment)

This directly addresses the "no separate runtime install required" constraint.

**How it works:** .NET's self-contained single-file publish bundles the CLR, the BCL, and your app into one `.exe`. The target machine needs nothing pre-installed — no .NET runtime, no VC++ redistributable beyond what's already on any Windows 10/11 box. This has been a stable, documented feature since .NET 6 and is unchanged in mechanism for .NET 10.

**Recommended `.csproj` publish settings:**

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  <PublishTrimmed>false</PublishTrimmed>
</PropertyGroup>
```

**Publish command:**

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Note on `PublishTrimmed`:** leave it `false`. Trimming reduces the ~150 MB self-contained payload down significantly, but it works by static analysis of what code paths are reachable, and it is well known to be unreliable with heavy reflection/COM-interop-marshalling and WinForms designer-generated code — exactly what this app uses (COM interop for audio, P/Invoke marshalling for CCD paths). Trimming failures here would surface as runtime `MissingMethodException`s that are painful to debug for a personal utility. Ship the untrimmed ~150 MB self-contained exe; disk space is not a constraint for this project, reliability is.

**Confidence:** HIGH — this is documented, current Microsoft guidance (Microsoft Learn: "Create a single file for application deployment"), unchanged in principle since .NET 6, and confirmed compatible with .NET 10 in Microsoft's own release notes.

## Installation

```bash
# Core project setup (no NuGet install needed for WinForms/System.Text.Json/P-Invoke — all in SDK/BCL)
dotnet new winforms -n RigToggle -f net10.0

# Supporting libraries
dotnet add package WindowsDisplayAPI --version 1.3.0.13
dotnet add package NAudio --version 2.3.0

# No package needed for IPolicyConfig — copy the interop source file directly into the project
# (e.g. adapt from https://github.com/File-New-Project/EarTrumpet/blob/dev/EarTrumpet/Interop/MMDeviceAPI/IPolicyConfig.cs)
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| WinForms | WPF (`UseWPF=true`) | If you want more polished visuals (better data binding, styling, animations) for the settings screen and don't mind XAML — WPF is equally capable of self-contained single-file publish and equally good at hosting Win32/COM interop. Functionally interchangeable for this project; choose based on taste, not capability. |
| WinForms | Avalonia UI (cross-platform XAML) | Only if there's a chance this becomes cross-platform later — it isn't (Windows-only utility per constraints), so the extra abstraction buys nothing and adds a third-party UI framework dependency. Do not use. |
| Hand-written `IPolicyConfig` interop | `AudioSwitcher.AudioApi` (NuGet, MIT) | If you'd rather take a ready-made dependency than embed ~100 lines yourself. Its stable release (3.0.3) was last published May 2023 and hasn't been updated since (a 4.0.0-alpha line exists but is pre-release); the underlying COM interface it wraps hasn't changed, so it likely still works, but you'd be depending on an unmaintained package for your single most fragile API call. Embedding the interop directly gives you the same reliability with zero dependency risk. |
| `WindowsDisplayAPI` managed wrapper | Raw P/Invoke of `user32.dll` `QueryDisplayConfig`/`SetDisplayConfig`/`DisplayConfigGetDeviceInfo` | If you want zero third-party dependencies and are comfortable writing ~300-400 lines of struct marshalling yourself (the CCD structs are numerous: `DISPLAYCONFIG_PATH_INFO`, `DISPLAYCONFIG_MODE_INFO`, etc.). `WindowsDisplayAPI` already did this correctly and is LGPLv3 (dynamic-link/DLL-reference use is fine in a closed-source app; you just need to include a notice/link back per its README) — using it saves significant P/Invoke debugging time for a personal-project timeline. |
| Both `WindowsDisplayAPI` (display) + custom `IPolicyConfig` (audio) as in-process API calls | NirSoft `MultiMonitorTool.exe` /disable + `SoundVolumeView.exe` /SetDefault, invoked as external processes | Only if you want to prototype fast without writing any interop code at all. Not recommended for the shipped product — see "What NOT to Use" below for why. |
| `Process.MainWindowHandle` + `ShowWindow`/`SetForegroundWindow` | `PInvoke.User32` NuGet package | If you want typed wrappers/constants instead of hand-rolled `DllImport` signatures. Functionally identical; only worth it if you're also doing other Win32 interop elsewhere in the app that would benefit from a fuller wrapper library. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `ChangeDisplaySettingsEx` with `CDS_UPDATEREGISTRY`/detach flags as your *only* display API | This older (pre-CCD, Windows XP-era) display API path is what people reach for first, but on many modern driver/GPU combinations it does not reliably remove a display from Windows' active display list the way the newer CCD (`SetDisplayConfig`) API does — it's more suited to resolution changes than topology (attach/detach) changes. Since the project's core value is a display that is *genuinely absent* from Windows (so BeamNG.drive stops misbehaving), this is the wrong tool for that specific job. | CCD API via `WindowsDisplayAPI`'s `PathInfo.ApplyPathInfos()` (confirmed in source to call `SetDisplayConfig` with topology-changing flags). |
| Bundling NirSoft `MultiMonitorTool.exe` / `SoundVolumeView.exe` and shelling out to them | (1) NirSoft's freeware license explicitly states you "must include all files in the distribution package, without modification" and forbids distributing it "as a part of commercial product" — messy to reason about even for a personal non-commercial tool, and directly conflicts with "one self-contained custom .exe." (2) NirSoft tools are frequently flagged by antivirus/SmartScreen as PUP/riskware because they're widely abused in malware droppers (they're legitimate but "look like" credential/config-stealing tools to heuristic scanners) — bundling one inside your own exe risks your own exe getting flagged too. (3) Shelling out to an external process is strictly less reliable than an in-process API call: it depends on locating/bundling the correct architecture binary, parsing text output, handling the child process's own console window, and has higher latency. | In-process calls via `WindowsDisplayAPI` (display) and a custom `IPolicyConfig` COM interop (audio) — no external processes, no bundled third-party binaries, no separate license to comply with beyond LGPL attribution for one library. |
| `AudioDeviceCmdlets` (PowerShell module) invoked via `Start-Process powershell` | Requires either PowerShell to be present with the module pre-installed (not guaranteed, and would violate the "standalone .exe, no separate runtime" spirit even though PowerShell itself ships with Windows) or bundling/installing the module at runtime — adds a moving part (process spawn, PowerShell startup latency ~200-500ms, output parsing) for something a 100-line in-process COM call does instantly. | Custom `IPolicyConfig` interop, in-process. |
| `FindWindow`/`FindWindowEx` by window title/class as the *primary* way to detect if Moza Companion is running | Window titles change (localization, "Moza Companion - v2.1.3" style version strings, unsaved-changes asterisks) and are fragile to match reliably; also only finds a window if one currently exists (misses "running but window destroyed/tray-only" states). | `Process.GetProcessesByName("MozaCompanion")` (match on the process's executable name, which you already have stored from Settings since the user picks the app path) to detect running state; use `Process.MainWindowHandle` only to get the handle for focus/minimize once you already know the process exists. Note: `MainWindowHandle` returns `IntPtr.Zero` if the process has no visible top-level window at that moment (e.g., minimized to tray with window destroyed rather than hidden) — treat that as "running but no window to manipulate" rather than "not running," and don't fail the whole toggle over it. Post-ship: this whole class of window manipulation was replaced by a `ShellExecute` relaunch for the target app specifically (see PROJECT.md), but the process-detection guidance here remains valid. |
| `PublishTrimmed=true` for this project | IL trimming's static analysis frequently misidentifies COM-interop and P/Invoke marshalling code paths (used heavily here) as unreachable and strips them, causing runtime failures that only show up when you hit the audio-switch or display-toggle code path — exactly the code you can least afford to have silently break. | Publish self-contained, untrimmed. Accept the larger (~150 MB) exe. |
| Framework-dependent deployment (`--self-contained false`) | Requires the target machine to already have the matching .NET runtime installed — this is precisely what the "no separate runtime install" constraint rules out. | `--self-contained true` with `PublishSingleFile=true` (see Packaging section). |

## Stack Patterns by Variant

**Global hotkey support (now in scope for v1.1 — see the dedicated section above):**
- Use `RegisterHotKey`/`UnregisterHotKey` (user32.dll P/Invoke) rather than a hooking library — it's 2 P/Invoke calls and needs a message-pump window (WinForms already gives you one), no extra dependency needed.

**System tray residency (now in scope for v1.1 — see the dedicated section above):**
- WinForms' built-in `NotifyIcon` component covers this natively — no extra library needed either.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|------------------|-------|
| `WindowsDisplayAPI` 1.3.0.13 | .NET 10 (net10.0-windows) via .NET Standard/Framework target | The package predates .NET's newer TFMs but is plain P/Invoke-based C# with no framework-specific API surface, so it resolves and runs fine under net10.0-windows self-contained publish — this is the standard situation for small, stable Win32-wrapper NuGet packages (no runtime dependency conflicts to worry about). |
| NAudio 2.3.0 | .NET 10 | Actively maintained against current .NET; no compatibility concerns. |
| Custom `IPolicyConfig` interop | Windows 10 / Windows 11 (all builds through 24H2/25H2 per current SoundVolumeView v2.53, July 2026, and EarTrumpet's continued reliance on the same interface) | Only the modern (Windows 8+) `IPolicyConfig` GUID is needed; skip the legacy `IPolicyConfigVista` fallback entirely since Windows 7/Vista support is irrelevant here. |

## Sources

- `/falahati/windowsdisplayapi` (Context7) — confirmed CCD API wrapper existence, LGPLv3 license, general shape of the library
- https://github.com/falahati/WindowsDisplayAPI (source read directly, `PathInfo.cs`) — confirmed exact `SetDisplayConfig` flag usage (`SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES` / `SDC_TOPOLOGY_SUPPLIED`) and the `GetActivePaths()`/`ApplyPathInfos()` snapshot-and-restore pattern — HIGH confidence, verified against actual code, not just docs
- https://learn.microsoft.com/en-us/windows-hardware/drivers/display/setdisplayconfig-summary-and-scenarios — official CCD API background — HIGH confidence
- https://www.nirsoft.net/utils/multi_monitor_tool.html — confirmed `/disable`/`/enable` CLI switches and, critically, the exact freeware redistribution license text — HIGH confidence (fetched directly)
- https://www.nirsoft.net/utils/sound_volume_view.html — confirmed v2.53, July 2026, explicit Windows 11 support claim — HIGH confidence (fetched directly), used as evidence the underlying `IPolicyConfig` technique still functions on current Windows
- https://github.com/File-New-Project/EarTrumpet/blob/dev/EarTrumpet/Interop/MMDeviceAPI/IPolicyConfig.cs — confirms the modern `IPolicyConfig`/`IPolicyConfigVista` GUIDs and that an actively-developed, widely-used open-source project still relies on this exact undocumented interface — MEDIUM-HIGH confidence (GitHub source, not official docs, but corroborated by multiple independent projects using the identical interface)
- https://github.com/xenolightning/AudioSwitcher and https://libraries.io/nuget/AudioSwitcher.AudioApi — confirmed last stable release 3.0.3 (May 2023), alpha 4.0.0 line inactive — MEDIUM confidence, used to justify recommending embedded interop over this package
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview and https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained — official self-contained/single-file/trimming guidance — HIGH confidence
- https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md and https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/ — confirmed .NET 10 GA 2025-11-11, LTS to 2028-11-14 — HIGH confidence
- https://www.nuget.org/packages/NAudio/ and https://github.com/naudio/NAudio — confirmed v2.3.0, MIT license, actively maintained (updated March 2026) — HIGH confidence

---
*Stack research for: Windows desktop GUI utility (display/audio/process automation) — v1.0 foundation + v1.1 tray/hotkey/CLI/IPC/multi-monitor additions + v1.2 visual polish (theming + tray icon redesign) additions*
*Researched: 2026-07-24 (v1.0) — updated 2026-07-26 (v1.1) — updated 2026-08-02 (v1.2)*
