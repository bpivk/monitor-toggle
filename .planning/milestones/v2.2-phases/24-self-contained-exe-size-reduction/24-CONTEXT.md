# Phase 24: Self-Contained Exe Size Reduction - Context

**Gathered:** 2026-08-18
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase makes the self-contained win-x64 exe measurably smaller than the v2.1 baseline (49,356,430 bytes), using additional safe, non-trimming levers on top of what v2.0 already applied (`EnableCompressionInSingleFile`, `SatelliteResourceLanguages=en`, `InvariantGlobalization=true`, NAudio meta-package split). No IL trimming, no Native AOT, no `PublishReadyToRun`. Fully independent of Phase 25 (Single-Instance Guard) and Phase 26 (Auto-Update) — no runtime code touched here, packaging/build configuration only, plus possible narrow dependency-graph changes (package swaps) in the spirit of v2.0's precedent.

</domain>

<decisions>
## Implementation Decisions

### Lever scope
- **D-01:** Package-reference swaps are in scope for this phase, not just `.csproj`/`.pubxml` property flags — if the `dotnet publish` output audit (per research/STACK.md's suggestion to check for further per-package native-asset bloat) finds an opportunity similar to v2.0's NAudio-meta-package → `NAudio.Wasapi` swap (zero source changes, just a `PackageReference` change), it's fair game. — **Reversibility:** reversible — a `PackageReference` swap is a one-line revert with no source-code impact, same shape as the v2.0 precedent.

### Startup-latency tradeoff
- **D-02:** A real (non-neutral) startup-latency cost is acceptable if it meaningfully shrinks the exe further and doesn't add a lot of extra load time. This relaxes the implicit "all levers must be startup-neutral" assumption — the user is open to trading some additional cold-start cost for a real size win, not just free/neutral levers. Note: `EnableCompressionInSingleFile` is already on (the single largest lever, ~57.8% of v2.0's total cut combined with the other three flags) and is a binary on/off in .NET's publish pipeline — there is no adjustable "more aggressive compression" level to turn up further. This decision matters if research/planning surfaces some other startup-cost-trading lever, not as an instruction to push compression harder (no such knob exists). — **Reversibility:** reversible — any newly-applied latency-trading lever can be turned back off.

### UseSystemResourceKeys
- **D-03:** Skip it. Keep exception messages fully readable in the app's existing off-by-default `debug.log` diagnostic feature — the size win (small) isn't worth the diagnostic downside. — **Reversibility:** reversible.

### Minimum bar for pursuing a lever
- **D-04:** No minimum-savings threshold — apply every safe lever found regardless of how small the individual saving is. "Just smaller is better" means stack every safe, low-risk win the research/publish-audit turns up, not just the ones that clear some rough worthwhile-effort bar.

### Claude's Discretion
- Exact `dotnet publish` measurement methodology and reporting format (byte-count before/after, documented in the phase SUMMARY) — follow the established v2.0/Phase 18 pattern (see canonical refs below), no need to re-ask.
- Whether any given lever qualifies as "safe" (zero/acceptable functional risk) vs. something to flag for user sign-off — use the same judgment CLAUDE.md's "What NOT to Use" table already applies (no IL trimming, no Native AOT, no `PublishReadyToRun` — these remain hard excludes regardless of size gain).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### This milestone's research (v2.2)
- `.planning/research/STACK.md` — confirms current MSBuild levers already in place, flags `UseSystemResourceKeys` (declined this phase, D-03) and a per-package native-asset publish-output audit as the remaining candidate levers, confirms `PublishReadyToRun` is an anti-lever (increases size)
- `.planning/research/SUMMARY.md` — v2.2 milestone-level synthesis; confirms Phase 24 is fully independent of Phases 25/26 and is the recommended first phase (isolated, no runtime code)

### Direct precedent (v2.0 Phase 18 — same domain, same methodology)
- `.planning/milestones/v2.0-phases/18-cleanup-pass-exe-size-reduction/18-RESEARCH.md` — exact measured baseline (116,946,229 bytes pre-v2.0) and post-v2.0 result (49,360,387 bytes, 57.8% reduction), the reproducible cross-platform measurement command (`dotnet publish -r win-x64 --self-contained -p:EnableWindowsTargeting=true`), and the NAudio-meta-package → `NAudio.Wasapi` swap this phase's D-01 explicitly extends the precedent of
- `.planning/milestones/v2.0-phases/18-cleanup-pass-exe-size-reduction/18-VERIFICATION.md` — how PERF-01/PERF-02 were verified (cold autostart boot + toggle round trip on real rig hardware), the same verification shape Phase 24's ROADMAP success criteria reuse

### Constraints (locked, do not re-litigate)
- `CLAUDE.md` "What NOT to Use" table — `PublishTrimmed=true` and Native AOT explicitly excluded (COM/P-Invoke static-analysis breakage risk); `PublishReadyToRun` confirmed to increase size, not reduce it
- `.planning/REQUIREMENTS.md` PERF-03 and its Out of Scope table — same exclusions restated at the requirement level

### Current build configuration (read directly, current state)
- `src/RigToggle.App/RigToggle.App.csproj` — current `SatelliteResourceLanguages`/`InvariantGlobalization` properties; no `DebugType` currently set (defaults to SDK's `portable`, i.e. separate `.pdb`, not embedded — `DebugType=none` would only remove the loose `.pdb`, not shrink the bundled exe)
- `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` — current `PublishTrimmed=false` (explicit, documented), `EnableCompressionInSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`
- `src/RigToggle.Windows/RigToggle.Windows.csproj` — current `NAudio.Wasapi` (already swapped from the meta-package in v2.0) and `WindowsDisplayAPI` package references — starting point for D-01's publish-output audit

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- v2.0's exact measurement command and methodology (18-RESEARCH.md) — reuse verbatim for before/after byte counts rather than re-deriving
- The existing `RigToggle.App.csproj`/`win-x64.pubxml`/`RigToggle.Windows.csproj` files already carry detailed inline comments documenting why each current lever is set the way it is (see canonical refs) — new levers should follow the same documentation-in-comment convention

### Established Patterns
- Exe-size work in this codebase is always paired with a rig-hardware verification (cold autostart boot + full toggle round trip), never accepted on build-output byte counts alone — Phase 24's ROADMAP success criteria already encode this

### Integration Points
- None — this phase touches only build/publish configuration and package references, no application code, no other phase's code paths

</code_context>

<specifics>
## Specific Ideas

- User's framing (verbatim intent): "if possible we could also compress the app and make it load a bit slower" — "yes, if it meaningfully shrinks it further and it does not take a lot of extra time." This sets the tolerance for D-02: real size win > minor/moderate added cold-start cost, but not an open-ended tradeoff — a lever that saves very little for a large latency cost would not clear this bar.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 24-Self-Contained Exe Size Reduction*
*Context gathered: 2026-08-18*
