# Phase 4 Repositioning Re-Test — Results & Go/No-Go Decision

Fill in this template after running the extended spike tool's `--disable-primary`
mode on the rig, then return it — this filled-in file records this plan's
**go/no-go** decision for RESEARCH Assumption A1 (and probes Assumption A2).

This directly answers RESEARCH Open Question 1: does repositioning-aware
survivor reconstruction (Pattern 1) let `ApplyPathInfos` remove the GDI-primary
monitor's path where the Phase 1 spike's naive removal threw
`PathChangeException` twice (Finding 3, `spike/RESULTS-TEMPLATE.md`)?

---

## Decision

**GO / NO-GO:** ___ (fill in exactly one: `GO` or `NO-GO: <what failed>`)

---

## Build & Run Instructions

Run on the rig PC (Windows, .NET 10 SDK installed), from an ordinary
(non-elevated) terminal:

```
dotnet run --project spike/MonitorDetachSpike -- --list
```

Note the index of the display with `IsGDIPrimary=True` from the printed list.

```
dotnet run --project spike/MonitorDetachSpike -- --disable-primary <index-of-primary>
```

Follow the on-screen prompts. When prompted, press Enter to restore the
original topology.

---

## Environment

- **Windows build (`winver` output):** ___
- **GPU / driver:** AMD Radeon model ___, Adrenalin version ___
- **Target monitor connection:** DisplayPort — confirm: - [ ] Yes
- **Primary monitor identity from `--list`:**
  - FriendlyName: ___
  - DevicePath: ___
  - Index used for `--disable-primary <index>`: ___

---

## Results Table

| Check | Result (Y/N) | Notes |
|-------|---------------|-------|
| `PathChangeException` thrown? | ___ | Expect **N** — this is the A1 fix under test |
| Target absent from `GetActivePaths()` after apply? | ___ | Expect **Y** |
| Exactly one primary at (0,0) after apply? | ___ | Expect **Y** |
| Target still discoverable via `GetAllPaths()` (A2)? | ___ | Expect **Y** — confirms restore-time re-discovery premise |
| Restore returned prior layout? | ___ | Expect **Y** — visually confirm monitor count/position/primary match pre-test state |

**Visual confirmation:** Did the primary monitor genuinely go dark/disconnected
(not just power off) and did the remaining monitor become primary?
- [ ] Yes, genuinely disconnected and remaining monitor became primary
- [ ] No — describe what was observed instead: ___

---

## Go/No-Go Decision Detail

Check exactly one:

- [ ] **GO** — Repositioning-aware primary removal (RESEARCH Pattern 1) works on
  this hardware: no `PathChangeException`, target absent from `GetActivePaths()`,
  exactly one primary at (0,0), target discoverable via `GetAllPaths()` for
  restore, and restore returns the original layout. **Plan 03's
  `WindowsMonitorController.Disable`/`Restore` should use Pattern 1 as documented
  in `.planning/phases/04-monitor-control-production/04-RESEARCH.md` (Pattern 1,
  lines 167-217) as-is.**
- [ ] **NO-GO** — Pattern 1 did not resolve the primary-removal case on this
  hardware (describe failure mode below). **Plan 03 must use the raw P/Invoke
  `SetDisplayConfig` fallback documented in `.planning/research/STACK.md`'s
  Alternatives table instead of `WindowsDisplayAPI`'s managed `ApplyPathInfos`
  wrapper for the primary-removal path.**

If NO-GO, describe what failed: ___

---

## Notes / Anomalies

(Free-form: anything unexpected observed during the re-test — e.g. taskbar/icon
reflow, delayed re-detection, `Screen.AllScreens` staleness as seen in the
original spike's Finding 2, etc.)

___

---

## References

- RESEARCH Open Question 1 — whether Pattern 1's repositioning fix resolves the
  primary-removal `PathChangeException` on real hardware (only answerable
  empirically, not via further static research).
- RESEARCH Assumption A1 — repositioning-aware survivor reconstruction lets
  `ApplyPathInfos` accept a topology with the primary removed.
- RESEARCH Assumption A2 — the CCD-disabled monitor remains discoverable via
  `PathInfo.GetAllPaths()` after being removed from `GetActivePaths()`, which
  restore-time re-attachment (DISPLAY-02) depends on.
- `spike/RESULTS-TEMPLATE.md` Finding 3 — the original Phase 1 spike's naive
  primary-removal failure this re-test is designed to resolve.
