# Phase 1: Monitor-Disable Feasibility Spike - Context

**Gathered:** 2026-07-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Prove that the primary monitor can be truly disconnected at the OS level (removed from Windows' active display set, not merely powered off) on the actual rig hardware — before any GUI, settings, or production monitor-control code is built on top of that assumption. This is a throwaway validation spike with no user-facing deliverable and no mapped requirements; its output is a go/no-go decision and a validated mechanism that Phase 4 will build the production `IMonitorController` around.

</domain>

<decisions>
## Implementation Decisions

### Test Execution Environment
- **D-01:** This session runs in a Linux environment and cannot execute or test Windows-native code directly. All spike code must be handed to the user as a runnable console tool + explicit instructions; the user builds/runs it on the actual rig PC and reports results back.
- **D-02:** The user only has VS Code on the rig PC — no confirmed .NET SDK install. Spike instructions MUST include .NET SDK installation/setup steps, not just build/run commands.

### Target Hardware
- **D-04:** Rig PC GPU is AMD (Radeon). Prioritize/verify the CCD topology-path-removal approach against AMD's driver behavior specifically — do not assume NVIDIA-only reference implementations transfer directly.
- **D-05:** The primary monitor (the one being disabled) is connected via DisplayPort.

### Success Validation
- **D-06:** Phase 1's pass/fail criterion is display-enumeration only: the monitor must disappear from Windows' active display list (Display Settings / `EnumDisplayMonitors` / `QueryDisplayConfig`). A real game launch test is NOT required to pass this phase.

### Elevation Policy
- **D-08:** The main app must stay non-elevated (`asInvoker`). If the chosen monitor-disable mechanism turns out to require admin rights, do not elevate the whole app — isolate that one operation in a small separate helper process instead. This protects cross-process window-focus control on the Moza Companion app (Phase 3), which breaks under UIPI if the calling process is elevated and the target isn't.

### Claude's Discretion [informational]
- **D-03** [informational]: Iterate as many round-trips as needed across build → run → report cycles until a clear go/no-go answer is reached. Do not artificially cap attempts at 1-2 tries. This governs the follow-up interaction after this plan executes (user runs the tool, reports back), not a task within this plan — no plan task implements it directly.
- **D-07** [informational]: BeamNG.drive is the designated real-world validation game for later phases (once the full toggle flow exists in Phase 4/5) — noted for later use, not part of this spike's acceptance bar. Carry this forward to Phase 4/5 discussion; not implementable as a Phase 1 task.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Research (from /gsd:new-project)
- `.planning/research/STACK.md` — recommended stack (.NET 10 + WinForms, `WindowsDisplayAPI` NuGet wrapper for CCD calls), confirmed via direct source read of `PathInfo.ApplyPathInfos()`
- `.planning/research/ARCHITECTURE.md` — component boundaries; flags monitor-disconnect as MEDIUM-LOW confidence, recommends this spike be resolved before other architecture is treated as settled
- `.planning/research/PITFALLS.md` — Pitfall 1 (no public CCD "disconnect" API exists) and Pitfall 2 (elevation/UIPI conflicts) are directly relevant to this phase
- `.planning/research/SUMMARY.md` — Phase 1 rationale and research flags

### Project-level
- `.planning/PROJECT.md` — Key Decisions table already records "true OS-level monitor disable, not power-off" and "elevation kept minimal" as pending decisions this spike will help validate
- `.planning/REQUIREMENTS.md` — DISPLAY-01/02/03 depend on this spike's outcome (mapped to Phase 4, not this phase)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
None — greenfield project, no code exists yet.

### Established Patterns
None yet established.

### Integration Points
This spike's validated mechanism becomes the implementation basis for `IMonitorController` in Phase 4 (per `.planning/research/ARCHITECTURE.md`'s adapter pattern). No integration with other components happens in this phase — it's a standalone throwaway console tool.

</code_context>

<specifics>
## Specific Ideas

- The spike tool should be a minimal standalone console app (not the full WinForms app) — its only job is to answer the go/no-go question, then it can be discarded once Phase 4 builds the production adapter.
- Since the user will be manually building/running this on Windows via VS Code, keep the spike's setup instructions self-contained and copy-pasteable (SDK install command, project scaffold command, run command).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 1-Monitor-Disable-Feasibility-Spike*
*Context gathered: 2026-07-24*
