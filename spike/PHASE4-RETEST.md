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

**GO / NO-GO:** GO

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

- **Windows build (`winver` output):** not captured (not blocking — see Notes)
- **GPU / driver:** AMD Radeon (model/driver version not captured)
- **Target monitor connection:** DisplayPort — confirm: - [x] Yes
- **Primary monitor identity from `--list`:**
  - FriendlyName: VG248
  - DevicePath: \\?\DISPLAY#ACI24A4#7&16485deb&0&UID512#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
  - Index used for `--disable-primary <index>`: 0

---

## Results Table

| Check | Result (Y/N) | Notes |
|-------|---------------|-------|
| `PathChangeException` thrown? | N | Confirmed — no exception; ran to completion |
| Target absent from `GetActivePaths()` after apply? | Y | Monitor visibly turned off as expected |
| Exactly one primary at (0,0) after apply? | Y (inferred) | Tool reported success; user did not paste raw console output, but no failure/anomaly reported for this line |
| Target still discoverable via `GetAllPaths()` (A2)? | Y (inferred) | Consistent with restore succeeding — user reactivated the monitor successfully afterward |
| Restore returned prior layout? | Y | User confirmed reactivating the primary screen worked |

**Visual confirmation:** Did the primary monitor genuinely go dark/disconnected
(not just power off) and did the remaining monitor become primary?
- [x] Yes, genuinely disconnected and remaining monitor became primary

---

## Go/No-Go Decision Detail

Check exactly one:

- [x] **GO** — Repositioning-aware primary removal (RESEARCH Pattern 1) works on
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

---

## Notes / Anomalies

(Free-form: anything unexpected observed during the re-test — e.g. taskbar/icon
reflow, delayed re-detection, `Screen.AllScreens` staleness as seen in the
original spike's Finding 2, etc.)

After restoring the primary monitor, the user's Chrome browser window (which
had been on the disabled primary display) did NOT move back to the primary
monitor automatically — it stayed wherever Windows placed it when the display
was removed. This is expected/out-of-scope behavior: DISPLAY-02 covers display
*configuration* restore (position, primary designation, orientation of the
monitors themselves), not per-application window placement, which is a Windows
shell responsibility outside this project's scope (per REQUIREMENTS.md
DISPLAY-02 wording and PROJECT.md's core value). Not a blocker for the GO
decision.

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
