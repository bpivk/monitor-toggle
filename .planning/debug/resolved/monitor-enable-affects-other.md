---
status: resolved
trigger: "With 3 monitors connected, after disabling all but the primary monitor (2 of 3 disabled), re-enabling just one of the two disabled monitors via the tile dashboard turns BOTH previously-disabled monitors on instead of only the one selected. Found during Phase 24 rig verification on a rig now upgraded to 3 monitors (previously tested with 2)."
created: 2026-08-19T00:00:00Z
updated: 2026-08-19T01:15:00Z
---

## Symptoms

expected: Clicking a single disabled monitor's tile to enable it activates only that one monitor. The other monitor(s) that were already disabled stay disabled.
actual: Both previously-disabled monitors turn on — the targeted one and the other disabled one — even though only one tile was clicked.
errors: None reported — no exception dialog, just the wrong end state (an extra monitor activated).
timeline: First observed after the rig was upgraded from 2 monitors to 3. Not previously exercised — the 2-monitor case (1 enabled + 1 disabled, toggling the single disabled one) can't reproduce this since there's only ever one candidate "other" monitor to leave alone or wrongly include. Unknown whether this is a pre-existing latent bug in the tile-click single-monitor mutation path or was introduced by a recent change; Phase 24 (in progress) only touched a `.csproj` MSBuild property and did not touch monitor-control source, so it's very unlikely to be the cause.
reproduction: |
  1. On a 3-monitor rig, disable two of the three monitors via the tile dashboard (leave the primary enabled).
  2. Click the tile for one of the two disabled monitors to re-enable it.
  3. Observe: the clicked monitor turns on AND the other still-should-be-disabled monitor also turns on.

## Prior Context

- Tile clicks mutate monitor state through `IMonitorController` (`MonitorTile` raises `ActionRequested`, `MainForm` is the sole caller — v2.1 Phase 19/20 architecture, see `.planning/PROJECT.md` Key Decisions).
- Project history flags a directly relevant prior bug class: `WindowsMonitorController.GetAllMonitors()` had a duplicate-row/dual-primary bug (fixed 2026-07-28, quick task 260728-qj1) and a `Restore()` cache-replay fast path that required exact `SetEquals` — "any enable-set monitor or stale cache routes through live reconstruction" because "an intervening CCD mutation can renumber a Source-ID between capture and replay" (v1.1 Phase 6 decision, `.planning/PROJECT.md`). A single-monitor-enable call that internally reconstructs/reapplies a broader topology (rather than mutating only the target monitor's CCD path) is a very plausible root cause — the same class of bug as the Source-ID staleness issue.
- `.planning/debug/knowledge-base.md` holds prior rig-discovered CCD findings extracted before the old snapshot-restore subsystem was deleted (Phase 18/CLEANUP-01) — worth checking for prior notes on multi-monitor CCD apply behavior before re-deriving from scratch.
- This is a Windows-only bug; this sandbox has no Windows runtime — root-cause confirmation via static code reading is possible, but live behavioral confirmation and fix verification are gated on the user testing on the actual 3-monitor rig.

## Evidence

- timestamp: 2026-08-19T00:10:00Z
  checked: .planning/debug/knowledge-base.md for prior CCD findings
  found: Entry `ccd-topology-restore-findings` documents (finding 5) that manual PathTargetInfo/mode reconstruction for previously-inactive targets failed three separate rig-tested times, and that the stable, proven mechanism for reactivating a disabled monitor is `PathInfo.ApplyTopology(Extend)` (zero-argument, OS-driven from the CCD persistence DB's last-known layout).
  implication: Any fix must not reintroduce manual per-target reconstruction — it must work with/around ApplyTopology(Extend), not replace it.

- timestamp: 2026-08-19T00:15:00Z
  checked: src/RigToggle.Windows/WindowsMonitorController.cs — ActivateMonitors(IReadOnlySet<string> monitorDevicePaths)
  found: The method's own doc comment (Pitfall 2/3 block, lines ~155-174 and ~185-190) states outright — "Extend recomputes the WHOLE topology from the DB record, not just the newly-added target(s) — it can incidentally reposition an unrelated, already-correct third monitor." The implementation calls `PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false)` unconditionally (once the skip-optimization for "all requested already active" is bypassed) with no post-call filtering: it verifies only that the *requested* device paths ended up active, never that no *unrequested* device path was also activated.
  implication: The code already knew Extend has whole-topology side effects, but the existing skip-optimization and verify-and-throw only guard against "requested paths didn't activate" — nothing guards against "extra unrequested paths got activated too."

- timestamp: 2026-08-19T00:18:00Z
  checked: WindowsDisplayAPI 1.3.0.13 NuGet package XML doc (no source available, DLL-only) — full `PathInfo` public member list
  found: The class exposes exactly two apply primitives — `ApplyPathInfos(IEnumerable<PathInfo>, bool, bool, bool)` (explicit, per-path) and `ApplyTopology(DisplayConfigTopologyId, bool)` (whole-topology, e.g. Extend/Clone/External/Internal). There is no per-device-path-scoped Extend overload.
  implication: ActivateMonitors has no way to ask the Extend topology to touch only one target — this confirms Extend is structurally whole-topology, matching the doc comment and matching Microsoft's public SDC_TOPOLOGY_EXTEND semantics (OS decides the entire layout from the persistence database, not caller-scoped).

- timestamp: 2026-08-19T00:22:00Z
  checked: src/RigToggle.App/MainForm.cs OnTileAction (tile click handler, ~line 839-857) and Controls/MonitorTile.cs
  found: MonitorTile.OnClick raises a parameterless ActionRequested event; MainForm.OnTileAction resolves the tile's single DevicePath and calls `_monitorController.ActivateMonitors(new HashSet<string> { devicePath })` — always a single-element set — with no subsequent DeactivateMonitors call in this code path (unlike ToggleService, see next entry).
  implication: Confirms the reported repro path (tile click -> ActivateMonitors with exactly one device path) reaches the whole-topology Extend call with no compensating step in the tile flow.

- timestamp: 2026-08-19T00:25:00Z
  checked: src/RigToggle.Core/ToggleService.cs — ToggleToRigMode/ToggleToNormalMode Monitor steps
  found: Both toggle directions call `_monitorController.ActivateMonitors(enableSet)` immediately followed by `_monitorController.DeactivateMonitors(disableSet)` in the same step closure — any monitor Extend over-activates that also happens to be in that direction's disableSet gets turned back off one line later, masking the bug in the full-toggle flow (as long as every managed monitor is listed in one of the two sets).
  implication: Explains why this bug was never observed via the main "Switch to Rig Mode"/"Switch to Normal Mode" buttons, only via the tile dashboard's single-monitor action — the full-toggle flow has an incidental self-correction the tile flow lacks. It also flags a related, currently-unconfirmed secondary risk: if a monitor is ever omitted from BOTH a toggle direction's enable-set and disable-set (e.g. a newly added 3rd monitor not yet added to either configured list after the 2->3 rig upgrade), the same Extend side effect could silently activate it via the full-toggle path too — out of scope for this fix but worth flagging to the user.

## Current Focus

reasoning_checkpoint:
  hypothesis: "ActivateMonitors(devicePaths) calls WindowsDisplayAPI's PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend) to bring a single OS-disabled monitor back online, but Extend is a whole-topology operation driven by the CCD persistence database's last-known extend layout, not scoped to the caller's requested device path set — so on a rig with >=2 currently-disabled-but-available monitors, calling Extend to activate ONE of them also reactivates any OTHER disabled-but-available monitor the DB remembers as part of the last extend layout, because ApplyTopology has no per-target overload and nothing in ActivateMonitors filters/corrects its result down to only the requested set."
  confirming_evidence:
    - "WindowsMonitorController.cs's own doc comment (Pitfall 3) states outright: Extend recomputes the WHOLE topology from the DB record, not just the newly-added target(s) — it can incidentally reposition an unrelated, already-correct third monitor. The code already documents whole-topology side effects on a third monitor; it just previously believed the side effect was limited to repositioning, not reactivation of a still-should-stay-disabled monitor."
    - "WindowsDisplayAPI 1.3.0.13's PathInfo class (verified via the package's XML doc + full member list, no source available) exposes exactly two apply primitives: ApplyPathInfos(explicit paths) and ApplyTopology(topology, allowPersistence) — there is no per-device-path-scoped Extend overload, so ActivateMonitors has no way to ask Extend to touch only one target."
    - "OnTileAction (MainForm.cs) calls _monitorController.ActivateMonitors(new HashSet<string> { devicePath }) with a single-element set on tile click, with no compensating DeactivateMonitors call afterward — unlike ToggleService's ToggleToRigMode/ToggleToNormalMode, which always call ActivateMonitors(enableSet) immediately followed by DeactivateMonitors(disableSet) in the same step, incidentally masking the same side effect there."
    - "The reported trigger explicitly could not reproduce on 2 monitors ('the 2-monitor case ... can't reproduce this since there's only ever one candidate other monitor') — consistent with Extend needing >=2 simultaneously-disabled-but-available monitors to produce an observable wrong-extra-monitor-turned-on symptom."
  falsification_test: "On the 3-monitor rig, with two monitors disabled, log GetActivePaths() device paths immediately before and immediately after ApplyTopology(Extend) inside ActivateMonitors when only one device path was requested. If the post-Extend active set contains only the requested device path added (not the other still-should-be-disabled monitor), the hypothesis is false. This check cannot be run in this sandbox (Windows-only CCD API) — it is the first verification step for the user on the rig."
  fix_rationale: "The fix does not touch the proven-working Extend call (per knowledge-base ccd-topology-restore-findings finding 5, manual PathTargetInfo/mode reconstruction for inactive targets failed three separate rig-tested times and must not be reintroduced). Instead it treats Extend's whole-topology side effect as an expected hazard to detect and correct afterward, using the ALREADY rig-proven DeactivateMonitors CCD-removal path (repositioning-aware ApplyPathInfos + verify-and-throw) to turn back off any device path that (a) is active after Extend, (b) was NOT active before Extend, and (c) was NOT in the caller's requested set. This targets the root cause (Extend's unscoped side effect) directly, adds no new CCD apply primitive, and reuses code with existing rig verification history instead of inventing a new mutation path."
  blind_spots:
    - "Cannot execute this on Windows in this sandbox — the hypothesis rests on static reading of WindowsMonitorController.cs's own doc comments, the WindowsDisplayAPI XML doc's method list, and general Win32 CCD SDC_TOPOLOGY_EXTEND semantics, not a rig-observed log. This fix ships rig-verification-gated, not self-verified."
    - "Have not confirmed the correction call (DeactivateMonitors on the unexpectedly-activated set) always succeeds cleanly if Extend's reactivated monitor lands overlapping a survivor or becomes GDI-primary — theoretically safe by the set-difference construction, but only rig testing proves it end-to-end."
  candidate_causes:
    - "code: ActivateMonitors's use of the unscoped PathInfo.ApplyTopology(Extend) primitive with no post-call correction for monitors outside the requested set"
    - "config: settings' MonitorsToEnable/NormalMonitorsToEnable lists could be stale/incomplete after the 2->3 monitor rig upgrade and omit the new third monitor from either set — a related, distinct secondary risk on the full-toggle path, not the cause of the reported tile-click symptom"
  and_gate: "no — Extend's whole-topology behavior is a single mechanism fully sufficient on its own to produce the exact reported symptom; the config-staleness item is a related but distinct latent risk on a different code path, not a second condition required alongside this one."

next_action: RESOLVED — user confirmed the fix on the real 3-monitor rig ("confirmed fixed"). Fix committed (see Resolution.files_changed), session archived to .planning/debug/resolved/. Nothing further to do. Secondary risk noted in Evidence (MonitorsToEnable/NormalMonitorsToEnable lists possibly stale after the 2->3 monitor upgrade, could let the full-toggle path silently activate a monitor omitted from both sets) remains documented here but was out of scope for this fix — flagged to the user, not silently dropped.

## Resolution

root_cause: "WindowsMonitorController.ActivateMonitors(devicePaths) reactivates a single disabled monitor via WindowsDisplayAPI's PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false) — a whole-topology CCD operation that restores the OS's entire last-known extend layout from the persistence database, not an operation scoped to the caller's requested device path(s) (WindowsDisplayAPI's PathInfo class has no per-target Extend overload). With only one monitor previously disabled (the pre-upgrade 2-monitor case), this whole-topology restore coincidentally only ever reactivates the one requested monitor. With >=2 currently-disabled-but-available monitors (the post-upgrade 3-monitor case), Extend reactivates ALL of them, not just the one the user selected via the tile. The full toggle flow (ToggleService.ToggleToRigMode/ToggleToNormalMode) was never affected because it always calls DeactivateMonitors(disableSet) immediately after ActivateMonitors(enableSet) in the same step, incidentally turning the over-activated monitor back off; the tile dashboard's single-monitor action (MainForm.OnTileAction) has no such compensating call, so the over-activation was directly observable there."
fix: "Added a post-Extend correction step inside ActivateMonitors: capture the active device-path set before calling ApplyTopology(Extend), capture it again after, and compute which device paths became active that were neither previously active nor part of the caller's requested set (extracted as a pure, unit-tested seam ComputeUnexpectedlyActivated). Any such unexpectedly-activated device paths are turned back off via the same already rig-proven DeactivateMonitors CCD-removal path (repositioning-aware ApplyPathInfos + verify-and-throw) before ActivateMonitors' own verify-and-throw runs. No change to the Extend call itself or to any previously-abandoned manual PathTargetInfo/mode reconstruction path."
verification: "CONFIRMED ON RIG. Static self-verification (pre-rig-test): (1) added unit tests for the pure ComputeUnexpectedlyActivated seam covering the exact reported scenario (2 disabled monitors, activate 1, other must be flagged for correction) plus the previously-passing 2-monitor case (no extras to correct) and the already-active/no-op case; (2) `dotnet build` succeeds; (3) full existing test suite still passes. Live rig verification (human-verify checkpoint): user tested on the actual 3-monitor rig per the reproduction steps in Symptoms and confirmed the fix — clicking one disabled monitor's tile now activates only that monitor; the other still-disabled monitor stays disabled (previously it was also reactivated). User response: \"confirmed fixed\"."
files_changed:
  - src/RigToggle.Windows/WindowsMonitorController.cs
  - src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs
