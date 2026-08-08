---
phase: 17-manual-monitor-panel-shared-safety-guard
plan: 04
subsystem: verification
tags: [dotnet, winforms, regression-gate, display-12-audit, rig-verification]

# Dependency graph
requires:
  - phase: 17-01
    provides: ToggleOrchestrator.BeginExclusiveMonitorAccess() lease, MonitorIdentifyOverlay
  - phase: 17-02
    provides: MonitorPanelForm (grid, hotplug refresh, row actions, confirm gate, Identify wiring)
  - phase: 17-03
    provides: MainForm/tray entry points, MonitorPanelFormFactory composition-root wiring
provides:
  - "Full-solution regression gate result for Phase 17"
  - "Static proof that the zero-survivors guard has exactly one implementation reached by all three mutation paths (Rig toggle, Normal toggle, manual panel)"
affects: [18 (cleanup pass — inherits a confirmed-clean Phase 17 baseline)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Comment-exclusion required for grep-based compliance checks: two of Task 1's literal acceptance-criteria greps (mode-config persistence, GDI-identity leakage) were written without the comment-exclusion filter checks 3/4 used, producing false positives against accurate doc comments describing what the code deliberately does NOT do. Re-run with the same comment-exclusion pattern confirms 0/0 — genuinely compliant, not a defect. Same false-positive class encountered in Phase 16's 16-05 stale-prose check."

key-files:
  modified: []

key-decisions:
  - "Treated the two failing literal acceptance-criteria greps (mode-config persistence, GDI-identity leakage) as acceptance-criteria authoring gaps, not code defects, after confirming both hits are exclusively inside doc/line comments (visible line-number prefixes 18-19 and 16/18/39, all `///` or `//`) that explain why the code avoids the forbidden pattern. Re-verified with the same comment-exclusion filter the plan's own checks 3/4 already use — both drop to 0, matching the plan's actual intent."

requirements-completed: []

# Metrics
duration: ~15min (Task 1 only — Task 2 is the blocking rig checkpoint)
completed: 2026-08-08
---

# Phase 17 Plan 04: Regression Gate + DISPLAY-12 Audit + Rig Verification Summary

**Task 1 (automated) complete: full solution builds clean, 84/84 core tests pass, and the DISPLAY-12 static audit confirms the zero-survivors guard has exactly one implementation reached by all three mutation paths with no duplicate guard, no toggle-pipeline coupling, no mode-config writes, and no GDI-identity leakage in the new App-tier files. Task 2 (rig-hardware verification) is the blocking human-verify checkpoint and has not yet run.**

## Regression Gate

**Build** (`PATH="$HOME/.dotnet:$PATH" dotnet build RigToggle.sln -p:EnableWindowsTargeting=true`):
```
Build succeeded.
    4 Warning(s)
    0 Error(s)
```
Warning count is 4, not the documented 3-warning baseline. Investigated: all 4 are the identical pre-existing `xUnit1031` ("Test methods should not use blocking task operations") lint on `ToggleOrchestratorTests.cs` — 3 from the original Phase 16 concurrency tests, plus 1 new hit from Plan 17-01's `BeginExclusiveMonitorAccess` concurrency test, which uses the same established `Task.Run` + blocking-wait pattern as the pre-existing tests. Same warning class, not a new defect category — the baseline simply grew by one test that legitimately needed the same pattern.

**Test** (`PATH="$HOME/.dotnet:$PATH" dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj`):
```
Passed!  - Failed:     0, Passed:    84, Skipped:     0, Total:    84, Duration: 109 ms - RigToggle.Tests.dll (net10.0)
```

**RigToggle.Windows.Tests build** (`PATH="$HOME/.dotnet:$PATH" dotnet build src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj -p:EnableWindowsTargeting=true`):
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
Not executed — `Microsoft.WindowsDesktop.App` runtime is not installed in this Linux dev environment. Documented, pre-existing limitation carried over from Phase 16, not a regression.

## DISPLAY-12 Audit

**1. Exactly one implementation site** — `grep -rn "at least one active display must remain" src --include=*.cs`:
```
src/RigToggle.Windows/WindowsMonitorController.cs:307:                "Cannot disable all configured monitors — at least one active display must remain.");
```
**PASS** — exactly one line, in the expected file.

**2. Exactly three production call sites** — `grep -rn "DeactivateMonitors(" src --include=*.cs | grep -v "/bin/" | grep -v "/obj/"`:
```
src/RigToggle.App/MonitorPanelForm.cs:259:                    _monitorController.DeactivateMonitors(new HashSet<string> { devicePath });
src/RigToggle.Core/ToggleService.cs:104:                _monitorController.DeactivateMonitors(disableSet);
src/RigToggle.Core/ToggleService.cs:356:            _monitorController.DeactivateMonitors(disableSet);
src/RigToggle.Core/Abstractions/IMonitorController.cs:42:    void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths);
src/RigToggle.Tests/Doubles/BlockingMonitorController.cs:48:    public void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths)
src/RigToggle.Tests/Doubles/FakeControllers.cs:64:    public void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths)
```
(Remaining hits in `WindowsMonitorController.cs` are all comments/doc-comments discussing the method, plus its own implementation at line 268 — expected, not a fourth caller.)
**PASS** — production call set is exactly: interface declaration, implementation, two `ToggleService.cs` sites (Rig + Normal), one `MonitorPanelForm.cs` site (panel). Test doubles present as expected, cleanly separable from production call sites.

**3. No panel-side re-derivation** (comment-excluded, `src/RigToggle.App/MonitorPanelForm.cs`):
| Pattern | Count |
|---|---|
| `Count\(.*IsActive` | 0 |
| `survivors` | 0 |
| `at least one active display` | 0 |
**PASS**.

**4. No toggle-pipeline coupling** (comment-excluded, `MonitorPanelForm.cs`):
`ToggleToRigMode\|ToggleToNormalMode\|ToggleResult\|ToggleService\|IModeStore` → **0 matches**. **PASS**.

**5. No mode-config persistence** — `grep -c 'MonitorsToDisable\|MonitorsToEnable\|NormalMonitorsToDisable\|NormalMonitorsToEnable' src/RigToggle.App/MonitorPanelForm.cs`:
```
2
```
**Literal check: FAIL (2, expected 0).** Investigated — both hits are inside a single doc comment (lines 18-19) explicitly documenting that panel actions "never persist into `AppSettings.MonitorsToDisable`/`NormalMonitorsToDisable`/`MonitorsToEnable`/`NormalMonitorsToEnable` or `IModeStore`." Re-run with the same comment-exclusion filter checks 3/4 use:
```
grep -nE 'MonitorsToDisable|MonitorsToEnable|NormalMonitorsToDisable|NormalMonitorsToEnable' MonitorPanelForm.cs | grep -vE '^[0-9]+:\s*(//|\*|/\*|///)' | wc -l
→ 0
```
**PASS (corrected)** — the literal acceptance-criteria grep as written doesn't exclude comments (unlike checks 3/4); this is an acceptance-criteria authoring gap, not a code defect. The doc comment accurately describes genuinely-absent behavior.

**6. No GDI-identity leakage** — `grep -rn "Screen.AllScreens" src/RigToggle.App/MonitorPanelForm.cs src/RigToggle.App/MonitorIdentifyOverlay.cs | wc -l`:
```
3
```
**Literal check: FAIL (3, expected 0).** Investigated — all 3 hits are in `MonitorIdentifyOverlay.cs` doc/line comments (lines 16, 18, 39) explaining why `Screen.AllScreens` is deliberately NOT used (17-RESEARCH.md Pitfall 2: its monitor-identity space doesn't correlate with CCD device paths). Re-run comment-excluded:
```
grep -nE "Screen.AllScreens" MonitorPanelForm.cs MonitorIdentifyOverlay.cs | grep -vE ':\s*[0-9]+:\s*(//|\*|/\*|///)' | wc -l
→ 0
```
**PASS (corrected)** — same authoring-gap class as check 5. No actual `Screen.AllScreens` call exists in either file.

**7. No dependency drift** — `git diff --stat de7618f..HEAD -- '*.csproj'`: empty output. **PASS** — no `.csproj` changes across any of Phase 17's commits.

### Audit Verdict

All 7 checks PASS. Checks 5 and 6 required comment-exclusion (not specified in the plan's literal grep) to reach their true PASS result; both are documented here as an acceptance-criteria gap (missing comment filter), not a code compliance gap — verified by direct inspection of every matched line.

## Rig Verification

Not yet run. Task 2 is a blocking human-verify checkpoint requiring real Windows rig hardware. See the six scenarios in `17-04-PLAN.md` Task 2 — presented to the operator in this session; results to be appended here once received.

---
*Phase: 17-manual-monitor-panel-shared-safety-guard*
*Task 1 completed: 2026-08-08*
