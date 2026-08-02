# Pitfalls Research

**Domain:** Adding Windows theme-following (dark/light mode) + custom tray/taskbar icons to an existing shipped WinForms tray-resident app
**Researched:** 2026-08-02
**Confidence:** HIGH for framework-API behavior (verified against official .NET 10 docs and dotnet/winforms GitHub issues), MEDIUM for icon-design/DPI specifics (WebSearch, cross-checked but not project-verified), LOW-flagged explicitly where noted

**Scope note:** This is a *subsequent-milestone* pitfalls pass for v1.2 (Visual Polish & Documentation). It supersedes the v1.1-era `PITFALLS.md` content (tray residency, hotkey reentrancy, multi-monitor topology, CCD long-idle-monitor risk) — that research is preserved in project history (git log for this file, and in the milestone's own closed research trail) since its conclusions were already validated and shipped in v1.1. This pass focuses specifically on the two new v1.2 surfaces: theme-following (DWM/registry/live-update) and tray/taskbar icon redesign, layered on top of the *existing* tray-residency, close/minimize-to-tray preference, and `NotifyIcon` lifecycle architecture shipped in Phase 8/Phase 11.

## Important Correction to Milestone Framing

PROJECT.md's "Key context" for this milestone states: *"System-theme-following in WinForms has no built-in support — requires manual DWM API calls for the title bar plus re-coloring every control by hand."*

**This is now factually outdated for this project's actual stack (.NET 10).** As of .NET 10 (GA November 2025, which is this project's pinned runtime per STACK.md), WinForms has **fully integrated, non-experimental dark mode support**: `Application.SetColorMode(SystemColorMode.System | .Dark | .Classic)`. This graduated out of the `WFO5001`-gated experimental state in .NET 9 and is a first-class API in .NET 10 — it recolors built-in controls automatically and manages the title bar's `DWMWA_USE_IMMERSIVE_DARK_MODE` attribute internally. (Source: [What's new in WinForms for .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100) — HIGH confidence, official docs.)

This doesn't eliminate the need for custom work (see Pitfalls 1 and 2 below — it has real gaps), but it changes the correct implementation strategy from "hand-roll everything" to "use `Application.SetColorMode` as the base layer, then patch its specific known gaps." Treat this as the single most important finding of this research: **verify this during the theme-infrastructure phase before writing any manual per-control recoloring code** — hand-rolling control-by-control recoloring that the framework already does is wasted effort and a source of conflicts (Pitfall 1).

## Critical Pitfalls

### Pitfall 1: Manual DWM title-bar call fights the framework's own internal call

**What goes wrong:**
If the theme-infrastructure phase (unaware of, or intentionally supplementing, `Application.SetColorMode`) also hand-rolls its own `DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ...)` P/Invoke call for the title bar, the two code paths can each set the attribute — once from WinForms' internal handling when `SetColorMode` is set, once from the app's own call. Confirmed community-reported symptom: *"Setting the DWMWA_USE_IMMERSIVE_DARK_MODE attribute twice can cause the title bar to start out as one color and then animate towards the correct color"* — a visible flash/flicker on every form show, most noticeable exactly on the app's existing hidden-tray-start path where a form's handle is created behind the scenes and later revealed.

**Why it happens:**
Reasonable engineers assume WinForms dark mode is purely "you must do it all yourself" (reinforced by PROJECT.md's own framing above), so they write the full manual DWM call without first checking whether `Application.SetColorMode` already does it.

**How to avoid:**
Decide once, explicitly, in the theme-infrastructure phase: use `Application.SetColorMode(SystemColorMode.System)` as the base mechanism for both control recoloring and the title bar. Only add a manual `DwmSetWindowAttribute` call as a Windows 10 fallback (see Pitfall 6) — gated so it never runs on Windows 11 where the framework already owns the attribute.

**Warning signs:**
Title bar briefly flashes light-then-dark (or vice versa) when a form is first shown; happens more on the tray-hidden startup path than on a fresh normal launch (different handle-creation timing).

**Phase to address:**
Theme-infrastructure phase — this is an architecture decision, not a bug to catch in review.

---

### Pitfall 2: Assuming built-in dark mode live-updates when the user flips Windows theme while the app is running

**What goes wrong:**
`Application.SetColorMode(SystemColorMode.System)` is applied once — at the point in `Program.cs`/startup where it's called. It does **not** subscribe to live OS theme-change notifications on its own. As of this research, `dotnet/winforms#13935` ("Does WinForms react to Dark Mode settings changes... WM_SETTINGCHANGE... ImmersiveColorSet?") is open, unresolved, and explicitly confirms this is *not* implemented framework behavior. If the milestone's stated goal is "follows Windows system light/dark mode" (implying live-following, not just "correct at each launch"), relying on `SetColorMode` alone silently under-delivers: the app will only match the *theme active at process start* and will not react if the user (or a scheduled light/dark automation tool) flips the theme mid-session — which, for a tray-resident, hours-long rig session, is a realistic scenario, not an edge case.

**Why it happens:**
The API name and .NET 10 docs' "no longer experimental" framing invite an assumption of full dynamic theming; the docs don't call out the live-update gap explicitly, so it's easy to ship, test with an app restart, and never notice.

**How to avoid:**
Explicitly implement live-following via `WM_SETTINGCHANGE` in the main form's `WndProc` override: check `m.Msg == 0x001A` and `Marshal.PtrToStringUni(m.LParam) == "ImmersiveColorSet"`, then re-call `Application.SetColorMode(...)`/reapply theme and `Invalidate()`/re-theme the tray icon and any currently-hidden forms. Do this once, centrally (e.g. in a shared theme service that all forms and the tray-icon manager subscribe to), not duplicated per form.

**Warning signs:**
Testing procedure only ever restarts the app to "test dark mode" rather than toggling Windows Settings > Personalization > Colors while the app is already running — this is exactly the kind of gap that source-level review won't catch but the real rig will, consistent with this project's established pattern (v1.0/v1.1 bugs only surfaced under real usage).

**Phase to address:**
Theme-infrastructure phase for the mechanism; polish/verification phase must include an explicit "flip Windows theme live, app already running, both visible and tray-hidden" rig test — this is not covered by a normal launch/relaunch test cycle.

---

### Pitfall 3: DWM/theme calls issued before the window handle exists — silently no-op on the app's existing hidden-tray startup path

**What goes wrong:**
Both `DwmSetWindowAttribute` and reliable control-recoloring require a created `HWND`. Calling either in a form's constructor (before `CreateHandle`/`OnHandleCreated`/`Load`) is a documented failure mode: *"The `DwmSetWindowAttribute` call should be done in the window's Load/Shown event... rather than before the form is displayed."* This project already has a *proven* history of exactly this class of bug: Phase 8's `--tray` hidden-start uses `Application.Run(new ApplicationContext())` with **no `MainForm` reference** specifically because the documented `ApplicationContext(mainForm)` pattern didn't actually suppress `Show()` on this runtime (per PROJECT.md Key Decisions). That non-standard startup path means the main form's handle-creation timing on the hidden-tray path is *not* the same as on a normal visible launch — a naive "apply theme in `Form_Load`" implementation may run at a different point relative to handle creation on this path than on the normal path, and only the hidden-tray path will fail silently (no exception, no visible symptom until the window is later shown from the tray icon and looks wrong).

**Why it happens:**
Standard WinForms theming guidance (and most blog tutorials) assumes the conventional `Application.Run(new MainForm())` startup shape. This app deliberately deviates from that shape for tray-hidden start, and that deviation is exactly where handle-lifecycle assumptions break.

**How to avoid:**
Apply theming logic in `OnHandleCreated` (fires reliably regardless of whether the form is ever `Show()`n) rather than `Form_Load`/`Shown` (which only fire when the form is actually displayed) — a hidden form still creates its handle even if never shown, so `OnHandleCreated` is the one lifecycle point common to both startup paths. Explicitly test theming on the `--tray` hidden-start path, not just normal launch.

**Warning signs:**
Title bar or controls appear correctly themed on normal launch but appear in default light-mode WinForms styling the first time the window is restored from the tray icon after a `--tray` hidden start.

**Phase to address:**
Theme-infrastructure phase for implementation; must be verified specifically against the existing `--tray` startup path (not just normal launch) before considered done — flag this as a required rig test given the project's history of exactly this startup-path class of bug (Phase 8, Phase 11 lockout bug were both divergent-path bugs).

---

### Pitfall 4: Icon-swap state space quietly doubles from 2 states to 4, with GDI-handle leak risk on long rig sessions

**What goes wrong:**
The app already swaps the tray `NotifyIcon.Icon` between two states (rig mode / normal mode). Adding theme-following icons turns this into four states (rig-light, rig-dark, normal-light, normal-dark) that must be selected correctly on: mode toggle, live theme change (Pitfall 2), and app startup. If the existing icon-swap code (or its extension) constructs a `new Icon(...)` on each swap without disposing the previous one, this is a real risk specifically *because* of this app's usage pattern: it stays tray-resident for entire multi-hour rig sessions with repeated toggles, which is exactly the workload that turns a per-swap GDI object leak (`Icon`/`Bitmap` handles count against the ~10,000-per-process GDI object ceiling) into an eventual failure, whereas a short manual smoke test (a few toggles) would never surface it.

**Why it happens:**
`Icon`/`Bitmap` are `IDisposable`; `NotifyIcon.Icon =` reassignment does not dispose the outgoing icon automatically, and it's easy to overlook when refactoring a 2-state swap into a 4-state one under time pressure, especially since nothing throws or visibly breaks until handles accumulate.

**How to avoid:**
Pre-load and cache all four `Icon` instances once at startup (they're static resources, not created per-swap), and only ever assign references to `NotifyIcon.Icon` from that cache — never `new Icon(...)` inside the toggle/theme-change handlers. Dispose the cached set once, on app exit.

**Warning signs:**
Task Manager's "GDI objects" column for the process climbing steadily across a long session with repeated toggles/theme flips rather than staying flat.

**Phase to address:**
Icon-redesign phase (where the 4-icon asset set and swap logic are built) — with an explicit code-review checklist item ("are all 4 icons loaded once and cached, never constructed per-swap?"). Verification (long-session GDI handle monitoring) belongs in the polish/verification phase, ideally during an actual extended rig session rather than a short smoke test.

---

### Pitfall 5: Wrong registry key drives icon-theme selection (`AppsUseLightTheme` vs `SystemUsesLightTheme`)

**What goes wrong:**
Windows exposes two independent theme keys under `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`: `AppsUseLightTheme` (governs app/window chrome — what `Application.SetColorMode(System)` effectively tracks) and `SystemUsesLightTheme` (governs system surfaces — the **taskbar and tray**, specifically). On Windows 11 a user can legitimately run light apps with a dark taskbar or vice versa. If the theme-infrastructure/icon-redesign code reads `AppsUseLightTheme` to decide which tray icon variant to show (a natural mistake, since it's the same key already being read/watched for window theming), the tray icon can end up light-on-light or dark-on-dark against the actual taskbar background even though the app window itself is themed correctly.

**Why it happens:**
Most WinForms dark-mode tutorials only ever mention `AppsUseLightTheme` because they're only theming the window, not the tray. It's the more commonly documented key, so it gets reused by default for the icon-selection logic without realizing the tray is a separate surface with its own key.

**How to avoid:**
Read `SystemUsesLightTheme` specifically to choose which tray icon variant (light-background-safe vs dark-background-safe) to display; keep `AppsUseLightTheme` reserved for window/control theming decisions. Watch both independently if both can change live (see Pitfall 2's `WM_SETTINGCHANGE` handling — the `lParam` string doesn't distinguish which key changed, so re-read both keys on every `ImmersiveColorSet` notification).

**Warning signs:**
Manually setting "dark taskbar, light apps" (or the reverse) in Windows Settings > Personalization > Colors and observing the tray icon doesn't match the taskbar even though the window does (or vice versa).

**Phase to address:**
Icon-redesign phase for the read-the-right-key logic; theme-infrastructure phase should expose a shared "current taskbar theme" signal (not just "current app theme") for the icon logic to consume, so this isn't duplicated ad hoc.

---

### Pitfall 6: Windows 10 fallback produces a mismatched dark-title/light-content window

**What goes wrong:**
Per official .NET 10 docs, *"Dark mode is only supported on Windows 11; older systems fall back to classic mode"* for `Application.SetColorMode`. If the theme-infrastructure phase separately adds a manual `DwmSetWindowAttribute` call as a "make sure the title bar goes dark on Windows 10 too" enhancement (the attribute itself does work on Windows 10 1809+, independent of WinForms' own support), the result on a real Windows 10 machine is a dark title bar sitting on top of an otherwise fully light-mode (unthemed) window body — a worse, more jarring visual state than doing nothing at all.

**Why it happens:**
It's tempting to "complete" Windows 10 support by adding the one API call that's known to work cross-version, without realizing the *controls* underneath won't follow because the framework's own dark-mode control recoloring is Windows-11-gated.

**How to avoid:**
Before adding any Windows-10-specific manual DWM call, confirm what Windows version the actual rig PC runs (not currently documented in PROJECT.md — verify this as a pre-requisite check in the theme-infrastructure phase). If it's Windows 11, this pitfall is moot and can be explicitly scoped out. If it could be Windows 10, either theme the title bar and skip full control recoloring consistently (accept a partial-but-consistent look), or don't attempt title-bar theming on pre-Win11 at all — don't do one without the other.

**Warning signs:**
Screenshots/testing only ever happen on one Windows version; this bug is invisible unless tested on the actual OS build the rig PC runs.

**Phase to address:**
Theme-infrastructure phase — resolve the actual target Windows version first (this is a scoping question, not just an implementation detail), then decide whether Windows-10-specific handling is even in scope for this milestone.

---

### Pitfall 7: Static system-brush/pen caches don't repaint on live color-mode switch (toolstrip separators, dropdown arrows)

**What goes wrong:**
A confirmed WinForms framework bug (`dotnet/winforms#12027`) affects exactly the control type this app already uses for its tray context menu (`ContextMenuStrip`/`ToolStrip`): *"colors only update correctly if the color mode is set at application startup"* when switching live, because `SystemBrushes`/`SystemPens` maintain a static cache indexed by `KnownColor` that isn't purged on a runtime color-mode switch. Concretely: `ToolStripSeparator` lines and `ToolStripSplitButton` dropdown arrows can remain the *old* theme's color after a live theme flip even though the rest of the tray context menu updates correctly — a subtle, easy-to-miss visual inconsistency that only appears after Pitfall 2's live-theme-change path is actually exercised, not on a fresh launch.

**Why it happens:**
This is a genuine, currently-unfixed framework limitation, not an app-level mistake — but it's specific to exactly the control (tray context menu) this project already has, so it will surface here if live theme-following is implemented at all.

**How to avoid:**
No clean first-party workaround exists as of this research (the fix requires the framework to purge the static caches, tracked upstream). Practical mitigations: (a) accept this as a known minor cosmetic gap and don't spend theme-infrastructure-phase time chasing it, or (b) force a full app restart-equivalent re-theme by disposing and rebuilding the `ContextMenuStrip` (not just its colors) on live theme change, which sidesteps the stale-cache issue at the cost of slightly more churn.

**Warning signs:**
Tray context menu separators/arrows look subtly wrong-themed only after flipping Windows theme live while the app is running with the menu previously shown at least once — won't reproduce on a fresh launch in the already-current theme.

**Phase to address:**
Polish/verification phase — flag as accepted known limitation if not worth fixing; document rather than silently ship as unnoticed.

---

### Pitfall 8: Modal dialogs and `MessageBox` remain permanently light-themed

**What goes wrong:**
Per official .NET 10 docs: *"some controls, like MessageBox, remain in light mode"* even with `Application.SetColorMode(SystemColorMode.System)` fully applied. If any part of this app's existing error/status surfacing uses `MessageBox.Show(...)` (a common pattern for e.g. hotkey-registration-failure or toggle-error surfacing per PROJECT.md's TRIG-01 "registration-failure surfacing"), that dialog will pop up stark white against an otherwise dark-themed app — a jarring, obviously-unfinished-looking regression that's easy to miss in code review (nothing is "broken," it just doesn't match) and easy to miss in a quick visual pass if the error path isn't deliberately triggered during testing.

**Why it happens:**
`MessageBox` is a thin wrapper over the native Win32 message-box API, which is a system-owned dialog outside WinForms' own rendering/theming control — the framework's dark-mode work doesn't (and can't easily) reach it.

**How to avoid:**
Audit every existing `MessageBox.Show` call site during the theme-infrastructure phase; replace user-facing ones with a themed custom `Form`-based dialog (a small owned form with OK/Cancel buttons that *does* pick up `Application.SetColorMode` styling) rather than leaving native `MessageBox` calls in place.

**Warning signs:**
A white flash-dialog appears when deliberately triggering an error path (e.g. hotkey conflict, launch-target missing) while the rest of the app is dark-themed.

**Phase to address:**
Theme-infrastructure phase should include an audit-and-replace pass over existing `MessageBox` call sites, since this app already has several error-surfacing code paths from prior milestones (hotkey conflict, autostart save-failure, etc. per PROJECT.md's bug history) that are exactly the kind of rarely-triggered path likely to be missed without a deliberate audit.

---

### Pitfall 9: Custom tray icon indistinguishable against one of the two taskbar theme backgrounds

**What goes wrong:**
A newly designed icon pair (rig-mode/normal-mode) validated only by eye in an image editor can look great against a white/light background and become nearly invisible (or vice versa) against Windows' actual dark taskbar background — Windows 11 does not automatically recolor arbitrary custom notification-area icons for contrast the way it does for some first-party monochrome system icons. This is purely a design-review gap, not a code bug, so it won't show up in any form of source review — only in a real screenshot against a real light taskbar and a real dark taskbar.

**Why it happens:**
Icon design is typically previewed against a neutral/white canvas in design tools, and the "does this read clearly on a genuinely dark taskbar" check requires an actual live comparison that's easy to skip when the icon "looks fine" in isolation.

**How to avoid:**
Design both icon variants with a visible outline/stroke or sufficient internal contrast that works on both a near-black and a near-white background (don't rely on background-color assumptions); explicitly screenshot the tray icon against both an actual light-taskbar and actual dark-taskbar Windows session before considering the icon-redesign phase done.

**Warning signs:**
Icon only ever verified in the image editor or against one taskbar theme during development.

**Phase to address:**
Icon-redesign phase for design; polish/verification phase must include the explicit real-rig, both-taskbar-themes screenshot check (this project's README milestone deliverable already requires real screenshots from the rig, which is a natural place to also capture this verification).

---

### Pitfall 10: Single-resolution icon renders blurry — and this app's own core action can trigger the exact DPI-change event that exposes it

**What goes wrong:**
A `NotifyIcon.Icon` backed by only a single embedded resolution (commonly just 32x32) gets stretched or shrunk by Windows to fit the actual DPI-scaled tray slot, producing a visibly blurry icon. This is a generically known WinForms/Win32 tray-icon gotcha, but it is *unusually relevant to this specific project*: this app's entire core function is disabling/enabling monitors, and if the rig monitor and the desk/primary monitor run at different DPI scaling percentages (common in mixed desktop+dedicated-rig-monitor setups), the monitor-disable/enable toggle itself is a plausible trigger for a DPI-scaling-context change around the same time the tray icon needs to re-render — making this pitfall more likely to actually manifest in this app's real usage than in a typical single-monitor utility.

**Why it happens:**
It's easy to embed just one or two icon sizes when producing a quick `.ico` in a hurry, and a single-DPI development/test machine won't surface the blurriness at all.

**How to avoid:**
Produce each `.ico` file with the full standard multi-resolution set (at minimum 16, 20, 24, 32, 48, 256 px — covering 100%/125%/150%/200% DPI scale steps) rather than a single embedded size; verify visually at whatever DPI scaling percentage the actual rig monitor and desk monitor each run at (check both, since they may differ).

**Warning signs:**
Icon looks fine on the primary development display but blurry specifically when the taskbar renders on a differently-DPI-scaled monitor, or immediately after a monitor topology change.

**Phase to address:**
Icon-redesign phase for producing the multi-resolution `.ico` asset; polish/verification phase should explicitly check icon sharpness on the actual rig hardware at its real DPI settings (both monitors, if they differ) — this cannot be meaningfully verified in a sandboxed/non-Windows build environment per PROJECT.md's own note that screenshots require the real rig.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|--------------------|-----------------|------------------|
| Skip live `WM_SETTINGCHANGE` theme-following, rely on `Application.SetColorMode` applied once at startup only | Much less code, ships faster | App silently goes stale-themed if user flips Windows theme mid-session; contradicts milestone's "follows Windows system light/dark mode" framing | Only acceptable if explicitly re-scoped as "themed at launch, not live" — must be a conscious decision, documented in PROJECT.md, not a silent gap |
| Leave native `MessageBox.Show` calls unthemed rather than building custom dialog forms | Saves building/testing a themed dialog form | Visibly jarring white flash on every error path, undermines the whole point of the "modern, theme-aware UI" milestone goal | Acceptable only for truly rare/dev-only diagnostic dialogs, never for user-facing error paths like hotkey conflicts |
| Ship without explicit Windows-10 fallback handling for dark mode | Less version-branching code | Inconsistent look if the rig PC (or any future machine) runs Windows 10 rather than 11 | Acceptable once the actual rig PC's Windows version is confirmed to be 11 — verify this first, don't guess |
| Single-resolution tray icon | Faster asset production | Blurry rendering on DPI-scaled displays, more likely triggered by this app's own monitor-toggle core action than in a typical app | Never acceptable for the shipped icon-redesign deliverable — multi-resolution `.ico` production has effectively zero extra cost with any modern icon tool |

## Integration Gotchas

Common mistakes when adding theming/icons on top of the *existing* tray-residency system from prior milestones.

| Integration Point | Common Mistake | Correct Approach |
|--------------------|------------------|-------------------|
| Existing `--tray` hidden-start path (`Application.Run(new ApplicationContext())`, no `MainForm`) | Theming applied in `Form_Load`/`Shown`, which never fires on this path until the user restores from tray — window then appears unthemed on first restore | Apply theming in `OnHandleCreated`, which fires for hidden forms too; explicitly test the hidden-start-then-restore sequence |
| Existing derived tray-icon-existence logic (`CloseMinimizesToTray \|\| MinimizeToTray`, applied live on Settings-Save per Phase 11) | Icon-theme-variant selection logic bolted on separately from this existing live-recompute path, drifting out of sync (e.g. icon shows stale theme variant after a live Settings-Save toggles tray visibility) | Route icon-variant selection (mode × theme) through the same "recompute on Settings-Save" trigger point already established in Phase 11, not a separate ad hoc update path |
| Existing rig/normal `NotifyIcon.Icon` swap on toggle | New theme-variant selection logic re-implemented separately from the existing mode-swap logic, doubling the number of places that decide "which icon right now" | Single icon-selection function taking `(mode, theme)` → cached `Icon`, called from every place that used to just take `mode` — one source of truth, not two parallel switches |
| Existing Settings dialog lifecycle | Unclear whether SettingsForm is a fresh instance per open or reused/hidden — theming code that assumes "always constructed fresh" will silently skip re-theming a reused-and-shown instance after a live theme change while it was previously hidden | Confirm actual SettingsForm instantiation pattern in the existing codebase before writing theme-application code; if reused, ensure it's included in the same live-theme-change broadcast as MainForm |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| Constructing new `Icon`/`Bitmap` objects per toggle or per theme change instead of caching | GDI object count climbs over a session | Pre-load and cache all 4 icon variants once at startup; only assign cached references | Manifests specifically on long tray-resident rig sessions with many toggles — the app's actual real-world usage pattern, not a quick smoke test |
| Re-theming the entire control tree (deep `Invalidate`/recursive recolor walk) on every `WM_SETTINGCHANGE`, without filtering to only the `ImmersiveColorSet` payload | Visible flicker or CPU spike on unrelated settings changes (many other Windows settings also broadcast `WM_SETTINGCHANGE`) | Filter strictly on `Marshal.PtrToStringUni(m.LParam) == "ImmersiveColorSet"` before doing any re-theme work | Noticeable if the user changes any other Windows setting while the app is open — easy to miss since normal dev testing rarely triggers unrelated `WM_SETTINGCHANGE` broadcasts |

## Security Mistakes

Not a significant concern for this milestone (no network/auth surface added), but one domain-relevant item:

| Mistake | Risk | Prevention |
|---------|------|------------|
| Assuming the theme registry keys (`AppsUseLightTheme`/`SystemUsesLightTheme`) always exist | Unhandled exception on read if a key is missing (uncommon but possible on some Windows configurations/server SKUs) or if the user manually deleted the value | Wrap registry reads with a safe-default fallback (assume light mode if unreadable) rather than letting a missing key throw and crash theme application |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-------------------|
| Theme only applied at launch, not live | App looks visually "wrong"/stale for the rest of a long session after the user or a scheduled tool flips Windows theme | Implement live `WM_SETTINGCHANGE` following (Pitfall 2) if the milestone goal genuinely means "follows," not just "matches at launch" |
| Native `MessageBox` dialogs breaking visual consistency on error paths | Jarring, unpolished-looking white flash exactly when something has already gone wrong (worst possible moment for a bad first impression) | Replace with themed custom dialog forms (Pitfall 8) |
| Icon indistinguishable against one taskbar theme | User can't tell mode at a glance in the exact situation the icon exists to solve (quick visual status check) | Verify against both real taskbar themes before shipping (Pitfall 9) |

## "Looks Done But Isn't" Checklist

- [ ] **Dark mode on MainForm/SettingsForm:** Often verified only via IDE Designer preview (which doesn't render dark mode per .NET 10 docs' own caveat) or a single launch — verify on the real rig with an actual Windows 11 dark-mode session, both forms, including the `--tray` hidden-start-then-restore path
- [ ] **Live theme-following:** Often tested only by restarting the app in each theme — verify by flipping Windows Settings > Personalization > Colors *while the app is already running*, both with the window visible and while tray-hidden
- [ ] **Tray context menu theming:** Often verified only at initial theme, missing the stale-static-cache toolstrip-separator bug (Pitfall 7) — verify after at least one live theme flip with the menu previously opened
- [ ] **Error/status dialogs:** Often forgotten entirely since they're rarely-triggered paths — deliberately trigger each known error surface (hotkey conflict, launch-target missing, autostart save failure) and check it's themed, not a native `MessageBox`
- [ ] **Tray icon on both taskbar themes:** Often verified only in the image editor — screenshot against a real light taskbar and a real dark taskbar
- [ ] **Tray icon DPI sharpness:** Often verified only on the primary dev monitor's DPI — check on the actual rig monitor and desk monitor if they run different scaling percentages
- [ ] **GDI handle stability over a long session:** Often never checked at all since a short manual test won't leak visibly — monitor Task Manager's GDI-objects column across an extended real rig session with repeated toggles
- [ ] **Windows 10 vs Windows 11 consistency:** Often assumed moot without checking — confirm the actual rig PC's Windows version before deciding whether Windows-10-specific fallback handling (Pitfall 6) is even in scope

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|-----------------|------------------|
| Double DWM attribute set causing title-bar flash (Pitfall 1) | LOW | Remove the redundant manual `DwmSetWindowAttribute` call; let `Application.SetColorMode` own the title bar exclusively (or vice versa, gated by OS version per Pitfall 6) |
| Theme not live-following (Pitfall 2) | MEDIUM | Add the `WM_SETTINGCHANGE`/`ImmersiveColorSet` handler retroactively; requires touching every form/icon-manager that needs to react, plus a full live-flip rig retest |
| Theming silently skipped on `--tray` hidden-start path (Pitfall 3) | LOW-MEDIUM | Move theming logic from `Load`/`Shown` to `OnHandleCreated`; retest both startup paths |
| GDI handle leak from per-swap icon construction (Pitfall 4) | LOW | Refactor to a pre-cached 4-icon dictionary; no data/state migration needed since icons are static assets |
| Wrong registry key for tray icon theme (Pitfall 5) | LOW | Swap the key read from `AppsUseLightTheme` to `SystemUsesLightTheme` in the icon-selection function; single-point fix given the "one source of truth" integration approach recommended above |
| Native MessageBox left unthemed (Pitfall 8) | MEDIUM | Requires building a reusable themed dialog form and updating each call site — proportional to how many MessageBox call sites exist in the current codebase (audit first) |
| Icon contrast/blurriness issues found late (Pitfalls 9, 10) | LOW | Asset-only fix — regenerate the `.ico` with proper multi-resolution content and adjusted contrast; no code changes needed if the icon-loading code already just points at the file/resource |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|--------------------|----------------|
| Manual DWM call conflicts with framework's own (1) | Theme-infrastructure | No title-bar color flash/animation on form show, checked on both startup paths |
| No live theme-following without explicit work (2) | Theme-infrastructure | Flip Windows theme live while app running (visible and tray-hidden); UI updates without restart |
| Theming skipped on hidden-tray startup handle timing (3) | Theme-infrastructure | `--tray` hidden start → restore from tray → window is correctly themed on first appearance |
| Icon-swap state explosion / GDI leak (4) | Icon-redesign | Extended-session GDI-handle-count monitoring stays flat across many toggles |
| Wrong theme registry key for tray icon (5) | Icon-redesign (consuming a shared signal from theme-infrastructure) | Set "dark taskbar + light apps" (or reverse) in Windows Settings; tray icon matches taskbar, window matches app theme |
| Windows 10 dark-title/light-body mismatch (6) | Theme-infrastructure (scoping decision) | Confirm actual rig Windows version first; if Win11-only, mark this pitfall explicitly out of scope |
| Toolstrip stale static-brush cache (7) | Polish/verification (documented as known limitation if unfixed) | Live theme flip with context menu previously opened; separators/arrows checked for stale color |
| Native MessageBox unthemed (8) | Theme-infrastructure (audit) | Deliberately trigger each existing error path; confirm no native white dialog appears |
| Icon low contrast on one taskbar theme (9) | Icon-redesign | Screenshot against both real light and dark taskbar |
| Blurry icon at DPI scale steps (10) | Icon-redesign | Visual check on rig monitor and desk monitor's actual DPI scaling, especially around a monitor-toggle event |

## Sources

- [What's new in WinForms for .NET 10 — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100) — HIGH confidence, official docs, confirms `Application.SetColorMode` is non-experimental in .NET 10, Windows-11-only scope, `MessageBox`/Designer limitations, `ControlStyles.ApplyThemingImplicitly` opt-in/opt-out mechanics
- [dotnet/winforms#13935 — Does WinForms react to Dark Mode settings changes live?](https://github.com/dotnet/winforms/issues/13935) — HIGH confidence (primary source, open/unresolved issue), confirms no built-in live theme-following
- [dotnet/winforms#12027 — Some toolstrip colors don't change when switching color mode](https://github.com/dotnet/winforms/issues/12027) — HIGH confidence (primary source), confirms static `SystemBrushes`/`SystemPens` cache bug affecting `ToolStripSeparator`/`ToolStripSplitButton`
- [dotnet/winforms#12014 — Form title bars are the wrong color (regression)](https://github.com/dotnet/winforms/issues/12014) — MEDIUM confidence, corroborates double-attribute-set title-bar-flash symptom
- WebSearch aggregation on `DWMWA_USE_IMMERSIVE_DARK_MODE` handle-creation timing (Microsoft Q&A, community sources) — MEDIUM confidence, consistent across multiple independent sources on the "must be set after handle creation, not in constructor" rule
- WebSearch aggregation on `AppsUseLightTheme` vs `SystemUsesLightTheme` registry key scope — MEDIUM confidence, consistent across multiple sources; both keys live at `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`
- WebSearch aggregation on `WM_SETTINGCHANGE`/`ImmersiveColorSet` detection pattern — MEDIUM confidence, standard community-verified technique, cross-checked against the confirmed-unimplemented framework gap in `#13935`
- WebSearch aggregation on multi-resolution tray-icon `.ico` sizing (16/20/24/32/48/256px covering 100–200% DPI) and DPI-scale re-render via `WM_DPICHANGED` — MEDIUM confidence, consistent across multiple independent sources (KeePass forum bug report, Electron issue tracker, general Win32 guidance), no single authoritative Microsoft doc found specifically for `NotifyIcon` — flag as MEDIUM not HIGH
- [SystemInformation.HighContrast Property — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.systeminformation.highcontrast) plus WebSearch on high-contrast/dark-mode interaction — MEDIUM confidence, confirms high-contrast and dark mode are mutually exclusive Windows states
- PROJECT.md (this repository) — primary source for existing tray-lifecycle architecture (`--tray` hidden-start via bare `ApplicationContext`, `CloseMinimizesToTray`/`MinimizeToTray` live-derived tray existence, prior rig-only-discovered bug history) used to ground integration-gotcha analysis

---
*Pitfalls research for: WinForms system-theme-following (dark/light mode) + custom tray icon redesign, added to an existing shipped tray-resident app*
*Researched: 2026-08-02*
