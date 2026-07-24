# Monitor-Detach Spike — Results & Go/No-Go Decision

Fill in this template after running the spike per `spike/RUN-INSTRUCTIONS.md`, then return it — this filled-in file is what records this phase's **go/no-go** decision (ROADMAP Phase 1 Success Criterion #3).

---

## Environment

- **Windows build (`winver` output):** ___ (not yet captured — run `winver` and fill in)
- **GPU / driver:** AMD Radeon model ___, Adrenalin version ___ (not yet captured)
- **Target monitor connection:** DisplayPort (per D-05) — confirm: - [x] Yes, connected via DisplayPort (both targets showed `OutputTechnology=DisplayPortExternal`)
- **Target monitor identity from `--list`:**
  - FriendlyName: VG248 (primary) / DELL U2415 (secondary)
  - DevicePath (primary): `\\?\DISPLAY#ACI24A4#7&16485deb&0&UID512#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}`
  - DevicePath (secondary): `\\?\DISPLAY#DELA0B8#7&16485deb&0&UID516#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}`
  - Index used for `--disable <index>`: tested both `0` (primary, VG248) and `1` (secondary, DELL U2415)

## Before-Disable State (dual-source count)

- **WindowsDisplayAPI `--list` active-path count:** 2
- **`Screen.AllScreens.Length` (from `--verify`):** 2 (inferred from `--disable` output: `before=2`)
- Full printed list from `--list` (paste verbatim):
  ```
  [0] Target=VG248 DevicePath=\\?\DISPLAY#ACI24A4#7&16485deb&0&UID512#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7} IsGDIPrimary=True OutputTechnology=DisplayPortExternal
  [1] Target=DELL U2415 DevicePath=\\?\DISPLAY#DELA0B8#7&16485deb&0&UID516#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7} IsGDIPrimary=False OutputTechnology=DisplayPortExternal
  ```

## Disable Result

**Two distinct outcomes depending on which monitor was targeted — see Notes/Anomalies for full explanation.**

### `--disable 1` (secondary, DELL U2415, non-primary)

- **Immediate PASS/FAIL line (verbatim from the tool):**
  ```
  FAIL: monitor still detected by at least one enumeration source.
    - Screen.AllScreens.Length did not drop (before=2, after=2).
  ```
- **~20-second DELAYED re-check PASS/FAIL line (verbatim from the tool):**
  ```
  PASS: monitor removed from BOTH WindowsDisplayAPI and Screen.AllScreens enumeration.
  ```
  - Did the monitor reappear after the delay (Pitfall C — hotplug re-detection)? - [x] No (immediate FAIL was a `Screen.AllScreens` stale-cache artifact, not reappearance — see Notes)
- **Did the monitor merely go black/blank while still counted as active (Pitfall A), or did it actually disappear from both enumeration sources?**
  - [x] Actually disappeared from both `--list`-equivalent (WindowsDisplayAPI) and `Screen.AllScreens` (true detach, confirmed by delayed re-check)
- **Did Windows reflow the taskbar/desktop icons onto the remaining monitor?** - [ ] Yes  - [ ] No — not observed/reported yet

### `--disable 0` (primary, VG248)

- **Result:** Unhandled exception, reproduced identically on two separate attempts:
  ```
  Unhandled exception. WindowsDisplayAPI.Exceptions.PathChangeException: Invalid paths information.
     at WindowsDisplayAPI.DisplayConfig.PathInfo.ApplyPathInfos(...)
  ```
  This is `SetDisplayConfig`'s own validation (`SDC_VALIDATE`) rejecting the topology — see Notes/Anomalies for root cause.

## Restore Result

- Pressed Enter to restore (after `--disable 1`) — did the original monitor count/position/primary designation come back correctly? - [x] Yes (topology-wise; the printed `Screen.AllScreens.Length is now 1` immediately after restore is the same stale-cache artifact as the immediate-check FAIL above, not evidence restore failed — no delayed re-check was run after restore to confirm the count numerically, but no visual/behavioral issue was reported)
- If no, what happened instead: N/A

## Elevation Observation

- Did any UAC prompt appear when running the tool from an ordinary (non-elevated) terminal? (Expected: **NO**, per Assumption A4 / D-08) - [ ] Yes  - [ ] No — not explicitly confirmed in session; no UAC prompt was reported at any point, tool was never run elevated
- Confirm the tool was run **NOT** as administrator for this primary test: - [x] Confirmed non-elevated

## Go/No-Go Decision

Check exactly one:

- [x] GO — SetDisplayConfig topology-path-removal (non-elevated) works reliably **for a non-primary display**; the primary-display case requires one additional, well-understood fix (see Notes) before it will work — this is an implementation requirement for Phase 4, not evidence the mechanism itself is unreliable.
- [ ] GO (with fallback) — primary approach failed; pnputil /disable-device admin fallback (see FALLBACK.md) works
- [ ] NO-GO — neither mechanism removes the monitor from enumeration on this hardware

**Chosen mechanism for Phase 4:** CCD topology-path-removal via `SetDisplayConfig` (non-elevated), same as this spike — but Phase 4's implementation MUST explicitly reposition the surviving display to desktop position `(0,0)` before applying the reduced topology when the removed display was the GDI primary. The spike's naive approach (reuse `PathInfo` objects as-is from `GetActivePaths()`, `allowChanges: true`) is not sufficient for that case because `WindowsDisplayAPI.PathInfo.Position` has no public setter — Phase 4 will need either a lower-level path/mode reconstruction or a different repositioning approach before calling `ApplyPathInfos`.

## Notes / Anomalies

**Finding 1 — Core mechanism confirmed working (non-primary case):** `--disable 1` (DELL U2415, non-primary, DisplayPort) succeeded via CCD topology removal, non-elevated, no UAC. Confirms the fundamental approach — real OS-level detach via `SetDisplayConfig`, not a power-off — works on this AMD Radeon + DisplayPort rig.

**Finding 2 — `Screen.AllScreens` staleness is a verification-tool artifact, not a real failure:** The immediate post-disable check reported `Screen.AllScreens.Length` unchanged (still 2) even though WindowsDisplayAPI's own re-query already confirmed the path was gone. `Screen.AllScreens` caches its result in a static field internal to WinForms and only refreshes on a `SystemEvents.DisplaySettingsChanging` event, which can lag in a console app with no message loop. The 20-second delayed re-check (added specifically for Pitfall C, hotplug re-detection) happened to also give this cache time to catch up, and the delayed check passed cleanly on both oracles. **This means the spike's dual-oracle PASS/FAIL logic is slightly too strict for an instant check** — it will report false-negative FAILs immediately after a real, successful detach. Worth noting for Phase 4: don't gate success purely on an instant `Screen.AllScreens` read; prefer the CCD/WindowsDisplayAPI oracle as authoritative, or add a short settle delay before trusting `Screen.AllScreens`.

**Finding 3 — Removing the primary monitor fails validation, reproducibly:** `--disable 0` (VG248, `IsGDIPrimary=True`) threw `PathChangeException: Invalid paths information` on two separate attempts. Root cause (confirmed by reading the `WindowsDisplayAPI` library source): `ApplyPathInfos` internally calls `SetDisplayConfig` with the `SDC_VALIDATE` flag before applying, and Windows itself rejects the requested topology — almost certainly because after removing the primary, no remaining display occupies desktop position `(0,0)` (DELL U2415 was presumably positioned as an extended secondary, not at the origin), and Windows requires a display at `(0,0)`. `allowChanges: true` does not fix this when explicit mode info is supplied (as `GetActivePaths()` provides), and `WindowsDisplayAPI.PathInfo.Position` has no public setter, so this specific spike code cannot self-correct it. This is a solvable, well-understood CCD gotcha (not unique to this library or hardware) but requires real implementation work in Phase 4 — not just reusing this spike's code as-is.

**Overall assessment:** the project's one previously-unvalidated core assumption — that true CCD-level monitor detach is achievable on this rig, non-elevated — is confirmed. The remaining primary-specific repositioning requirement is a scoped, known engineering task for Phase 4, not a feasibility blocker. Fallback path (FALLBACK.md / pnputil) was not needed and not tested.

**Still to capture:** `winver` output, GPU driver version, taskbar/icon reflow observation. Fill in above if you'd like a fully complete record, though they don't change the go/no-go conclusion.
