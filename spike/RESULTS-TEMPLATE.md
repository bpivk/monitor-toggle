# Monitor-Detach Spike — Results & Go/No-Go Decision

Fill in this template after running the spike per `spike/RUN-INSTRUCTIONS.md`, then return it — this filled-in file is what records this phase's **go/no-go** decision (ROADMAP Phase 1 Success Criterion #3).

---

## Environment

- **Windows build (`winver` output):** ___
- **GPU / driver:** AMD Radeon model ___, Adrenalin version ___
- **Target monitor connection:** DisplayPort (per D-05) — confirm: - [ ] Yes, connected via DisplayPort
- **Target monitor identity from `--list`:**
  - FriendlyName: ___
  - DevicePath: ___
  - Index used for `--disable <index>`: ___

## Before-Disable State (dual-source count)

- **WindowsDisplayAPI `--list` active-path count:** ___
- **`Screen.AllScreens.Length` (from `--verify`):** ___
- Full printed list from `--list` (paste verbatim):
  ```
  ___
  ```

## Disable Result

- **Immediate PASS/FAIL line (verbatim from the tool):**
  ```
  ___
  ```
- **~20-second DELAYED re-check PASS/FAIL line (verbatim from the tool):**
  ```
  ___
  ```
  - Did the monitor reappear after the delay (Pitfall C — hotplug re-detection)? - [ ] Yes  - [ ] No
- **Did the monitor merely go black/blank while still counted as active (Pitfall A), or did it actually disappear from both enumeration sources?**
  - [ ] Went black but still enumerated (FAIL — power-off behavior, not true detach)
  - [ ] Actually disappeared from both `--list` and `Screen.AllScreens` (true detach)
- **Did Windows reflow the taskbar/desktop icons onto the remaining monitor?** - [ ] Yes  - [ ] No

## Restore Result

- Pressed Enter to restore — did the original monitor count/position/primary designation come back correctly? - [ ] Yes  - [ ] No
- If no, what happened instead: ___

## Elevation Observation

- Did any UAC prompt appear when running the tool from an ordinary (non-elevated) terminal? (Expected: **NO**, per Assumption A4 / D-08) - [ ] Yes  - [ ] No
- Confirm the tool was run **NOT** as administrator for this primary test: - [ ] Confirmed non-elevated

## Go/No-Go Decision

Check exactly one:

- [ ] GO — SetDisplayConfig topology-path-removal (primary, non-elevated) works reliably
- [ ] GO (with fallback) — primary approach failed; pnputil /disable-device admin fallback (see FALLBACK.md) works — NOTE: this makes Phase 4 elevation-isolation mandatory (Assumption A1)
- [ ] NO-GO — neither mechanism removes the monitor from enumeration on this hardware

**Chosen mechanism for Phase 4:** ___

## Notes / Anomalies

___
