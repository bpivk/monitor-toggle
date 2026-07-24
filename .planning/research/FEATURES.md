# Feature Research

**Domain:** Windows desktop hardware-profile-toggle utility (multi-monitor profile switching, default audio device switching, sim-racing rig automation)
**Researched:** 2026-07-24
**Confidence:** MEDIUM-HIGH (established ecosystem, multiple corroborating sources; no single tool does exactly this combination, so synthesis is inferred from adjacent tools)

## Feature Landscape

This project sits at the intersection of three existing tool categories, none of which alone covers the full "Rig Toggle" use case:

1. **Multi-monitor profile switchers** — DisplayFusion, UltraMon, NirSoft MultiMonitorTool. Save/restore monitor topology (enabled/disabled, primary, position, resolution) as named profiles, switch via hotkey/menu/trigger.
2. **Default audio device switchers** — SoundSwitch, AudioSwitcher, NirSoft SoundVolumeView. Switch Windows default playback (and often communication) device via hotkey/menu/tray, sometimes with per-app auto-switch rules.
3. **Game-monitor / rig-automation utilities** — smaller community tools like GameMonitor, plus DisplayFusion "load profile when app launches" triggers. Purpose-built for the sim-racing/dual-monitor problem: force a game onto a specific display by manipulating what Windows considers primary/available at launch time.

Rig Toggle's job is to compose the display-disable behavior of category 1, the audio-switch behavior of category 2, and a companion-app-launch step, into a single two-state toggle with automatic state memory — a narrower, single-purpose version of what these general-purpose tools offer as one feature among many.

### Table Stakes (Users Expect These)

Features a one-click hardware-profile-toggle tool cannot skip — without them the tool doesn't do its core job.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| One-click/one-action toggle trigger (GUI button) | This *is* the product — every adjacent tool (DisplayFusion profiles, SoundSwitch hotkeys) reduces a multi-step manual process to one action | LOW | Already scoped as GUI-only for v1 per PROJECT.md |
| True OS-level monitor disable/enable (not power-off) | NirSoft MultiMonitorTool's core feature is exactly this distinction — `/disable` and `/enable` remove/restore a display from Windows' active display set via CCD API, unlike DDC power-off which leaves the display "connected" | MEDIUM-HIGH | Windows CCD API (`SetDisplayConfig`/`QueryDisplayConfig`) or equivalent; this is the crux technical requirement and the reason simpler approaches (monitor power-off) don't satisfy the BeamNG-style misbehavior |
| Default audio output device switch | Core feature of every audio-switcher tool (SoundSwitch, SoundVolumeView); table stakes because "rig mode" isn't real if sound still comes out of the desk speakers/headset | LOW-MEDIUM | Windows Core Audio API (`IPolicyConfig` — undocumented but universally used by SoundSwitch/NirSoft/AudioDeviceCmdlets since there's no public MMDevice API for setting *default* device) |
| State snapshot before switching | Every profile tool (DisplayFusion, UltraMon) stores "current" state as a profile; here it must be captured *implicitly* at toggle time rather than pre-configured, since restore must match whatever was actually active, not a fixed preset | MEDIUM | Must capture: monitor topology (which displays enabled, which is primary) + current default audio device. **Dependency:** required before restore-on-toggle-back can exist at all |
| Restore exact previous state on toggle-back | The other half of every profile-switcher's value prop — a switch nobody trusts to switch back correctly is a switch nobody uses | MEDIUM | **Depends on** state snapshot feature above; must be reliable even if hardware config changed slightly between snapshot and restore (e.g., a monitor was unplugged) |
| Companion-app launch with duplicate-instance prevention | Standard pattern across all single-instance Windows apps (mutex check + `FindWindow`/process enumeration + `SetForegroundWindow`); without it, users get duplicate Moza Companion windows on repeated toggles | LOW-MEDIUM | Detect via named mutex or process enumeration; if running, call `SetForegroundWindow`/`ShowWindow` to focus instead of relaunching |
| Best-effort window state control on toggle-back (minimize) | Companion apps in this ecosystem (peripheral vendor tray apps) are commonly minimized rather than closed — matches how NirSoft/DisplayFusion window-rule features treat "managed" windows | LOW | Already scoped as best-effort per PROJECT.md; true close-without-kill isn't achievable externally unless the target app cooperates |
| Settings/config screen (pick monitor, audio devices, app path) | Every comparable tool (DisplayFusion profiles, SoundSwitch device selection, MultiMonitorTool monitor IDs) requires the user to identify *which* hardware/app the tool should act on — hardcoding is not viable since monitor/device IDs differ per machine | LOW-MEDIUM | One-time setup, not a repeated-use feature; needs to enumerate available displays and audio endpoints for picker UI |
| Basic failure feedback (toggle didn't fully succeed) | NirSoft and SoundSwitch both surface errors (e.g., device not found) rather than silently no-op — silent failure in a display/audio switch is confusing and hard to diagnose | LOW | Doesn't need to be sophisticated — a message box or status line is sufficient for v1; still necessary since disabling a monitor while it's not actually present, or an audio device unplugged, are realistic failure modes |
| Standalone .exe, no separate runtime install | Every well-regarded competitor in this space (NirSoft tools, SoundSwitch, DisplayFusion) ships as a single download with no separate framework install friction | LOW-MEDIUM | Already scoped; typically resolved via self-contained publish (e.g., .NET self-contained deployment) |

### Differentiators (Nice-to-Have, Explicitly Deferred to v2 per PROJECT.md)

Features that make the tool notably more convenient than "click a button in a window," but are not required for the core mechanic to work. All three below are already called out as Out of Scope in PROJECT.md — listed here to confirm that categorization against ecosystem norms and to make deferral explicit for requirements writers.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Global hotkey trigger | Every mature competitor (DisplayFusion, SoundSwitch) treats hotkeys as a primary trigger, often preferred over opening a window — valuable once the tool is used many times per session | LOW-MEDIUM | Straightforward to add later (`RegisterHotKey` Win32 API); correctly deferred since GUI click is sufficient to validate the core mechanic first |
| System tray residency + auto-start on boot | SoundSwitch and DisplayFusion both live in the tray by default — removes the "find and launch the .exe" step | LOW-MEDIUM | Natural v1.x add; requires background process + tray icon + optional Windows startup registration |
| Toast/status notification on toggle | SoundSwitch shows a banner confirming the switched device — reduces "did it actually work?" uncertainty, complements the "basic failure feedback" table-stakes item | LOW | Nice complement once tray residency exists (notification without an open window needs *some* presence) |
| Visual "which mode am I in" indicator | Adjacent to notifications — a persistent tray icon state or window title reduces reliance on memory/observation of physical monitor state | LOW | Cheap to add once tray icon exists; not needed while the tool is only reachable via an open GUI window (the window itself can show current mode) |
| Auto-trigger on game/app launch (DisplayFusion/GameMonitor-style) | Removes the toggle action entirely for a known set of games — closest analogue is DisplayFusion's "load profile when application launches" trigger and the community GameMonitor tool | MEDIUM-HIGH | Real scope creep risk: requires process-launch watching, per-game mapping UI, and handling races between game launch and display switch completing before the game queries available displays. Correctly out of v1 — validate manual toggle first |
| Toggle history/log | Useful for diagnosing "why didn't it restore correctly" after the fact | LOW | Low cost, moderate diagnostic value; reasonable v1.x candidate once basic failure feedback proves insufficient in practice |
| Confirmation/undo safety net before disabling primary monitor | Disabling the wrong monitor when GUI-input has just been removed (mouse now only on the other screen) is a real self-lockout risk seen in community discussions of monitor-disable tools | LOW-MEDIUM | Worth a "you are about to disable monitor X, confirm?" dialog on first-run or as a settings toggle — cheap insurance against the single worst failure mode of this tool |

### Anti-Features (Commonly Present in Similar Tools, but Wrong for This Project)

Features that exist in general-purpose competitors precisely *because* they're multi-user, multi-machine, or general-purpose tools — none of that applies to a single-user, single-machine, two-state personal toggle.

| Feature | Why Requested (seen in competitors) | Why Problematic Here | Alternative |
|---------|---------------------------------------|-----------------------|-------------|
| Cloud sync / multi-device profile sync | Enterprise tools (DisplayFusion Pro) support this for users who roam between machines/laptops | This tool manages exactly one machine's exactly two states — sync solves a problem that doesn't exist here, and adds an account/network dependency to what should be a fully offline utility | None needed; if the rig PC changes, redo the one-time settings screen |
| Arbitrary number of named profiles (N-way profile manager) | DisplayFusion/UltraMon are general profile managers supporting many named configs (home, work, TV, presentation, etc.) | Rig Toggle has exactly two states (normal, rig) by design — building a general profile store adds a data model, CRUD UI, and profile-selection UX for a use case that never needs more than a toggle | Hardcode the two-state toggle; if a third state is ever needed, that's a new product decision, not a v1 feature |
| Per-game/per-app auto-switch rules engine | SoundSwitch and DisplayFusion both offer conditional trigger rules (switch when app X launches) as a core feature | A rules engine (trigger types, conditions, per-app config, priority ordering) is significant surface area for a tool whose entire job is "one button, two states" — this is the same scope creep as the "auto-trigger on game launch" differentiator, but generalized further | Manual toggle before racing; revisit only if manual toggle proves genuinely annoying after real use |
| Plugin/scripting system | DisplayFusion has a full scripting engine (Lua-like) for power users to extend behavior | Wildly disproportionate for a tool with 2 states and ~4 pieces of hardware state to manage; scripting support implies a stable extension API, sandboxing considerations, and documentation burden with zero current users beyond the author | If new behavior is needed later, add it directly to the app rather than exposing an extension point |
| Multi-user / role-based configuration | Enterprise multi-monitor tools support per-user profiles on shared machines | Single-user personal tool — there is no second user, and building account/user separation is pure wasted effort | None; one settings file for the one user |
| Auto-update mechanism | Commercial tools (DisplayFusion, SoundSwitch) ship auto-updaters since they have a public user base | A personal tool with one user (the author) has no update-distribution problem to solve — auto-update infrastructure (update server, signing, rollback) is effort spent on a non-problem | Rebuild and replace the .exe manually when changes are made |
| Telemetry/analytics/crash reporting | Commercial tools instrument usage to guide product decisions | No product-market questions to answer for a personal tool — this is pure privacy-irrelevant overhead | None; if something breaks, the author debugs it directly, e.g., via local error dialogs/logs already covered under table stakes |
| Licensing/activation/DRM | Commercial competitors (DisplayFusion, UltraMon) gate features behind license keys | Not a commercial product — there is nothing to protect and no one to gate | None |
| General import/export of arbitrary display/audio configs (config file portability across many machines) | Profile-manager tools let you export/import configs to move between machines | Only one machine exists in this project's scope; portability solves a problem the user doesn't have | If the rig PC is ever replaced, redo the one-time settings screen (already a table-stakes feature) |

## Feature Dependencies

```
[Settings/config UI: pick monitor, audio devices, app path]
    └──requires──> (nothing — this is the entry point; all toggle logic depends on it)

[State snapshot (capture current monitor + audio config)]
    └──requires──> [Settings/config UI] (must know which monitor/devices to inspect)
    └──enables───> [Restore exact previous state on toggle-back]

[Restore exact previous state on toggle-back]
    └──requires──> [State snapshot]

[Companion-app duplicate-instance prevention]
    └──requires──> [Settings/config UI] (must know app path to detect/launch)
    └──enables───> [Best-effort minimize on toggle-back] (needs the same window-handle discovery)

[True OS-level monitor disable/enable]
    └──requires──> [Settings/config UI] (must know which monitor is "primary to disable")
    └──enhances──> [One-click toggle] (this is the mechanic that makes the toggle worth building at all — the BeamNG-style misbehavior is only fixed by true disable, not by simpler alternatives)

[Global hotkey trigger] (deferred)
    └──enhances──> [One-click toggle] (alternate trigger path, does not replace it)

[System tray residency] (deferred)
    └──enables───> [Toast/status notification] (deferred)
    └──enables───> [Visual "which mode" indicator] (deferred)

[Auto-trigger on game/app launch] (deferred)
    └──conflicts──> [Confirmation/undo safety net] (an automatic trigger by definition can't pause for a confirmation dialog — if both were ever pursued, auto-trigger would need to skip confirmation, undermining the safety net's purpose)

[Per-game/per-app auto-switch rules engine] (anti-feature)
    └──generalizes──> [Auto-trigger on game/app launch] (the differentiator is the narrow, single-target version of this broader anti-feature; do not build the general form)
```

### Dependency Notes

- **State snapshot requires Settings/config UI:** the tool can't know which monitor or audio devices to snapshot until the user has identified them once via setup.
- **Restore-on-toggle-back requires State snapshot:** this is the most important ordering constraint in the whole feature set — there is no way to "remember previous state" without first building the capture step. Any roadmap phase implementing restore must have snapshot capture already working.
- **Companion-app duplicate-prevention requires Settings/config UI:** the app path/process to check for must be known first.
- **Best-effort minimize enables from duplicate-instance prevention:** the same mechanism used to find the running companion app's window handle (for focusing it) is reused to minimize it on toggle-back — building these together is more efficient than building them separately.
- **True monitor disable enhances the core toggle:** without true OS-level disable (vs. simple power-off), the tool doesn't actually solve the stated problem (games like BeamNG misbehaving on a "secondary" display) — this is the single most technically load-bearing table-stakes feature and deserves the most implementation/testing attention.
- **Auto-trigger conflicts with confirmation/undo safety net:** flagged so that if either deferred feature is picked up in v2, the tradeoff is visible up front rather than discovered mid-implementation.
- **Per-game rules engine generalizes auto-trigger-on-launch:** the anti-feature and the differentiator are the same underlying capability at different scope levels — this dependency line exists to make clear that "just build the general version since we're already touching this code" is the scope-creep trap to avoid.

## MVP Definition

### Launch With (v1)

Everything in Table Stakes above, which maps directly to the "Active" requirements already listed in PROJECT.md:

- [ ] One-click GUI toggle (normal → rig)
- [ ] True OS-level primary monitor disable/enable
- [ ] Default audio output device switch
- [ ] State snapshot at toggle time
- [ ] One-click GUI toggle back (rig → normal) with exact-state restore
- [ ] Companion app launch-or-focus (no duplicate instances)
- [ ] Best-effort companion app minimize on toggle-back
- [ ] Settings screen: pick monitor, audio device pair, app path
- [ ] Standalone .exe distribution
- [ ] Basic failure feedback (toggle didn't complete as expected)

### Add After Validation (v1.x)

Trigger for adding: the manual GUI-click flow has been used for real racing sessions and proven the core mechanic works reliably.

- [ ] Global hotkey trigger — once opening the GUI window every session feels like friction
- [ ] System tray residency + auto-start on boot — once manual launch each time is annoying
- [ ] Confirmation dialog before disabling the primary monitor — cheap insurance, consider pulling forward into v1 if a lockout incident occurs during testing
- [ ] Toast/status notification on toggle — once tray residency exists, to close the "did it work?" feedback gap
- [ ] Toggle history/log — if diagnosing an incorrect restore becomes a recurring need

### Future Consideration (v2+)

Features to defer until the core two-state mechanic has been used long enough to know if broader automation is actually wanted.

- [ ] Auto-trigger on specific game/app launch — defer because it reintroduces the exact display-detection-timing problem this tool exists to solve manually, plus adds process-watching and per-game config surface area
- [ ] Visual persistent "which mode" indicator — defer until tray residency exists to host it

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| True OS-level monitor disable/enable | HIGH | HIGH | P1 |
| Default audio device switch | HIGH | MEDIUM | P1 |
| State snapshot + exact restore | HIGH | MEDIUM | P1 |
| One-click GUI toggle | HIGH | LOW | P1 |
| Companion app launch/focus, no duplicates | MEDIUM-HIGH | LOW-MEDIUM | P1 |
| Settings/config UI | HIGH | LOW-MEDIUM | P1 |
| Best-effort minimize on toggle-back | MEDIUM | LOW | P1 |
| Basic failure feedback | MEDIUM | LOW | P1 |
| Standalone .exe packaging | MEDIUM | LOW-MEDIUM | P1 |
| Global hotkey trigger | MEDIUM | LOW-MEDIUM | P2 |
| Tray residency + autostart | MEDIUM | LOW-MEDIUM | P2 |
| Confirmation dialog before monitor disable | MEDIUM | LOW | P2 |
| Toast notification on toggle | LOW-MEDIUM | LOW | P2 |
| Toggle history/log | LOW | LOW | P2 |
| Auto-trigger on game launch | LOW-MEDIUM | HIGH | P3 |
| Visual mode indicator | LOW | LOW | P3 |

**Priority key:**
- P1: Must have for launch (this v1)
- P2: Should have, add when possible (v1.x)
- P3: Nice to have, future consideration (v2+)

## Competitor Feature Analysis

| Feature | DisplayFusion / UltraMon | SoundSwitch / NirSoft SoundVolumeView | NirSoft MultiMonitorTool | Rig Toggle Approach |
|---------|---------------------------|-----------------------------------------|--------------------------|----------------------|
| Monitor enable/disable | Yes, via monitor profiles (GUI + hotkey) | N/A | Yes, dedicated feature — CLI `/enable` `/disable`, true CCD-level disable, not power-off | Adopt the same true-disable semantics, but scoped to exactly one "primary" monitor rather than a general N-monitor profile system |
| Default audio device switch | No (out of scope for these tools) | Yes, core feature, with hotkeys/profiles/per-app rules | N/A | Adopt basic switch-on-toggle only; skip per-app rules (anti-feature) |
| App launch integration | Yes — DisplayFusion can trigger a profile load when an app launches, or launch an app as part of a profile | No | No | Adopt launch-as-part-of-toggle, but only for one fixed companion app, not a general "launch N apps" list |
| Duplicate-instance handling | Not applicable (DisplayFusion itself is the always-running tool) | Not applicable | Not applicable | Must build this ourselves — mutex/process-check + `SetForegroundWindow`, a well-established Windows pattern outside this tool category |
| Trigger mechanisms | Hotkey, title-bar button, app-launch trigger, monitor-connect trigger, cycling | Hotkey, tray double-click, profile auto-switch on app/game launch | CLI only (called by scripts/hotkey tools) | v1: GUI button only. v1.x: hotkey. v2+: consider app-launch trigger only if manual toggle proves insufficient |
| Profile count | Many named profiles (general-purpose) | Many device pairs/profiles | N/A (raw enable/disable, no profile concept) | Exactly two states (normal/rig) — deliberately not a general profile manager |
| Notifications | Some (via triggers/scripting) | Yes — banner notification on switch | No (silent CLI tool) | Defer to v1.x; basic failure feedback only in v1 |
| Packaging/distribution | Installer, licensed commercial software | Free, installer or portable | Free, portable .exe, no install | Standalone .exe, no license/installer — closest analogue is SoundSwitch/NirSoft's zero-friction distribution model |

## Sources

- [DisplayFusion — Working with Monitor Profiles](https://www.displayfusion.com/HelpGuide/WorkingWithDisplayFusionMonitorProfiles/) — MEDIUM confidence, official help guide
- [DisplayFusion Discussions — Hot Keys & Monitor Configuration Profiles](https://www.displayfusion.com/Discussions/View/hot-keys-monitor-configuration-profiles/?ID=e7b3ea44-2ce7-4ed5-bfa2-60f790bff987) — MEDIUM confidence, official vendor community forum
- [DisplayFusion Discussions — Triggers and Functions for Monitor Profile Changes](https://www.displayfusion.com/Discussions/View/triggers-and-functions-for-monitor-profile-changes/?ID=01944b58-d888-72b4-ad3b-f8fa86f93299) — MEDIUM confidence
- [DisplayFusion Discussions — Sim racing monitor](https://www.displayfusion.com/Discussions/View/sim-racing-monitor/?ID=1abcc809-b39b-4f74-a4b2-9b6ed37fce2b) — MEDIUM confidence, directly relevant use case discussion
- [GameMonitor (GitHub)](https://github.com/supercam19/GameMonitor) — LOW-MEDIUM confidence, community tool, single source
- [UltraMon — Display Profiles feature tour](https://www.realtimesoft.com/ultramon/tour/display_profiles.asp) — MEDIUM confidence, official vendor page
- [UltraMon — Wikipedia](https://en.wikipedia.org/wiki/UltraMon) — MEDIUM confidence, general background
- [SoundSwitch (GitHub, Belphemur)](https://github.com/belphemur/soundswitch) — HIGH confidence, official open-source repo
- [SoundSwitch — official site](https://soundswitch.aaflalo.me/) — HIGH confidence, official vendor page
- [PC Gamer — SoundSwitch coverage](https://www.pcgamer.com/this-little-tool-takes-the-hassle-out-of-switching-audio-devices/) — MEDIUM confidence, independent editorial coverage corroborating official claims
- [NirSoft MultiMonitorTool — official page](https://www.nirsoft.net/utils/multi_monitor_tool.html) — HIGH confidence, official vendor documentation (referenced via multiple secondary sources including command-line syntax)
- [NirSoft — New utility to handle multiple monitors (NirBlog)](https://blog.nirsoft.net/2012/07/19/new-utility-to-handle-multiple-monitors/) — HIGH confidence, official vendor blog
- [CodeProject — Detect if another process is running and bring it to the foreground](https://www.codeproject.com/Articles/2976/Detect-if-another-process-is-running-and-bring-it-) — MEDIUM confidence, long-standing widely-cited community pattern, corroborated by Microsoft Learn discussions
- [Microsoft Learn — Single Instance Detection Sample](https://learn.microsoft.com/en-us/previous-versions/ms771662(v=vs.100)?redirectedfrom=MSDN) — MEDIUM confidence, archived but authoritative Microsoft pattern documentation
- [Microsoft Learn — WindowsAppSDK Discussion #1747, single-instance app](https://github.com/microsoft/WindowsAppSDK/discussions/1747) — MEDIUM confidence, current official framework guidance corroborating mutex + `SetForegroundWindow` approach

---
*Feature research for: Windows desktop hardware-profile-toggle utility (Rig Toggle)*
*Researched: 2026-07-24*
