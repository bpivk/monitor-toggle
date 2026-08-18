# Phase 24: Self-Contained Exe Size Reduction - Research

**Researched:** 2026-08-18
**Domain:** .NET 10 self-contained WinForms single-file publish (MSBuild-level size levers only, no trimming/AOT/R2R)
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01 (Lever scope):** Package-reference swaps are in scope for this phase, not just `.csproj`/`.pubxml` property flags — if the `dotnet publish` output audit (per research/STACK.md's suggestion to check for further per-package native-asset bloat) finds an opportunity similar to v2.0's NAudio-meta-package → `NAudio.Wasapi` swap (zero source changes, just a `PackageReference` change), it's fair game. Reversibility: reversible.
- **D-02 (Startup-latency tradeoff):** A real (non-neutral) startup-latency cost is acceptable if it meaningfully shrinks the exe further and doesn't add a lot of extra load time. `EnableCompressionInSingleFile` is already on and has no adjustable "level." This decision matters if research/planning surfaces some other startup-cost-trading lever. Reversibility: reversible.
- **D-03 (UseSystemResourceKeys):** Skip it. Keep exception messages fully readable in the app's existing off-by-default `debug.log` diagnostic feature — the size win (small) isn't worth the diagnostic downside. Reversibility: reversible.
- **D-04 (Minimum bar for pursuing a lever):** No minimum-savings threshold — apply every safe lever found regardless of how small the individual saving is.

### Claude's Discretion

- Exact `dotnet publish` measurement methodology and reporting format (byte-count before/after, documented in the phase SUMMARY) — follow the established v2.0/Phase 18 pattern, no need to re-ask.
- Whether any given lever qualifies as "safe" (zero/acceptable functional risk) vs. something to flag for user sign-off — use the same judgment CLAUDE.md's "What NOT to Use" table already applies (no IL trimming, no Native AOT, no `PublishReadyToRun` — these remain hard excludes regardless of size gain).

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.
</user_constraints>

## Summary

This phase's own directive — actually run the publish and inspect the output rather than trust prior claims — paid off. All four of v2.0's already-applied levers (`EnableCompressionInSingleFile`, `SatelliteResourceLanguages=en`, `InvariantGlobalization=true`, `NAudio.Wasapi` package split) were confirmed still present and correctly configured by reading the current `.csproj`/`.pubxml` files directly. A fresh cross-compiled `dotnet publish -r win-x64 --self-contained -p:EnableWindowsTargeting=true` was run against today's codebase and produced **49,367,342 bytes** — close to, but not byte-identical to, the recorded v2.1 baseline of 49,356,430 bytes (a 10,912-byte drift explained in Common Pitfalls Pitfall 3; not a regression).

**The headline finding is new and real:** `RigToggle.App.exe`'s self-contained bundle embeds seven WinForms Design-time and VisualBasic-compatibility assemblies (`System.Windows.Forms.Design.dll`, `System.Windows.Forms.Design.Editors.dll`, `System.Design.dll`, `System.Drawing.Design.dll`, `Microsoft.VisualBasic.Core.dll`, `Microsoft.VisualBasic.Forms.dll`, `Microsoft.VisualBasic.dll`) that the SDK's `UseWindowsForms=true` reference pack pulls in automatically and that this codebase never references (confirmed via exhaustive `grep` — zero hits for `Microsoft.VisualBasic`, `System.ComponentModel.Design`, `System.Drawing.Design`, `PropertyGrid`, or `IDesigner` anywhere in `src/`). A hand-written MSBuild `<Target AfterTargets="ComputeResolvedFilesToPublishList">` that removes exactly these 7 named files from `@(ResolvedFileToPublish)` — a manually curated deny-list, **not** IL trimming, since it performs zero reachability analysis and touches nothing related to the COM/P-Invoke marshalling code CLAUDE.md warns about — was tested live this session (both as a scratch `Directory.Build.targets` and inline inside `RigToggle.App.csproj` itself; both produced byte-identical output) and cut the bundle from 49,367,342 → **46,770,879 bytes**, a further **2,596,463-byte (5.26%) reduction**, entirely on top of the existing four levers.

Three other candidate levers were investigated and ruled out with evidence, not assumption: `mscordbi.dll`/`mscordaccore*.dll`/`createdump.exe` (CLR debugger/crash-dump native components) were suspected bloat but confirmed via a diagnostic MSBuild target to already be absent from `ResolvedFileToPublish` for this project's single-file publish (the runtime pack's `DropFromSingleFile=true` metadata already excludes them — nothing to do). `IncludeNativeLibrariesForSelfExtract=false` was tested and produced a byte-identical exe in this cross-compile environment — no measurable effect, not worth changing (and changing it would risk reintroducing loose native DLLs, violating the one-exe distribution constraint). `DebugType=none`/`DebugSymbols=false` was tested and shaved only 167 bytes off the bundled exe itself (confirming STACK.md's prior claim that the `.pdb` was never bundled) while eliminating `.pdb` generation entirely — but per the same reasoning underlying D-03 (debug.log exception readability), .NET's portable-PDB support enriches unhandled-exception stack traces with file:line info even without an attached debugger, so this trades a real (if small) diagnostic capability for a negligible size win. Recommendation: skip it, same rationale class as D-03.

The known `dotnet/sdk#4078` issue (self-contained WinForms bundling the entire WPF stack, 30+ MB uncompressed) was checked directly against this project's actual publish output — no `PresentationCore.dll`/`PresentationFramework*.dll`/`PresentationUI.dll` are present (only a 16 KB `WindowsBase.dll`), confirming this specific issue does not apply to the current .NET 10 SDK for this project. `WindowsDisplayAPI.dll` (64 KB) and `NAudio.Wasapi.dll`+`NAudio.Core.dll` (368 KB combined) were also directly measured — both already minimal, no further package-swap opportunity exists in this project's own third-party dependency graph (D-01's audit is complete: it surfaces zero further package-reference swaps, but does surface the SDK-reference-pack finding above, which is presented as an MSBuild-target-based exclusion rather than a `PackageReference` change).

**Primary recommendation:** Add the 7-file WinForms-Design/VisualBasic exclusion target directly to `RigToggle.App.csproj` (verified working inline, no separate `Directory.Build.targets` file needed). Do not touch `DebugType`, `IncludeNativeLibrariesForSelfExtract`, or attempt to exclude CLR diagnostic natives — none of those produce a real win or are already excluded. This single new lever alone satisfies PERF-03's success criterion #1 (46,770,879 < 49,356,430 bytes) without touching `PublishTrimmed`, `PublishAot`, or `PublishReadyToRun`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Publish/packaging config (new exclusion target) | Build/Packaging (MSBuild, not runtime) | — | The new `<Target>` lives entirely inside `RigToggle.App.csproj`, runs only during `dotnet publish`, and has zero application-code footprint — same tier classification as v2.0's four levers per `18-RESEARCH.md` |
| Rig verification | N/A (verification activity, not a tier) | — | Confirms the exclusion target didn't regress the WinForms/COM-interop runtime tiers (display + audio + app-launch + Settings UI, which is the app's only consumer of `System.Windows.Forms.*` beyond the base assembly) |

## Project Constraints (from CLAUDE.md)

- **No IL trimming ever:** `PublishTrimmed` must remain unset/false. CLAUDE.md and `REQUIREMENTS.md`'s Out of Scope table both call this out explicitly. The new exclusion target is deliberately **not** trimming — it performs no static reachability analysis and cannot misclassify COM-interop (`IPolicyConfig`) or P/Invoke (`WindowsDisplayAPI` CCD) code paths, because it only ever removes 7 specific, by-name-matched, already-known-unused files from the publish output; it never inspects or modifies IL in any assembly that ships.
- **No Native AOT, no `PublishReadyToRun`** — both remain hard excludes regardless of any size gain (confirmed in REQUIREMENTS.md Out of Scope table; `PublishReadyToRun` is a documented *anti*-lever, increasing size).
- **`.NET 10`, self-contained, single-file, `win-x64` only** — established in `RigToggle.App.csproj`/`win-x64.pubxml`, both read directly this session and confirmed unchanged since v2.0/v2.1.
- **No elevation manifest** — do not add one while touching `RigToggle.App.csproj` in this phase.
- **`RigToggle.Core` has zero Windows API references** — unaffected by this phase; the new target lives entirely in `RigToggle.App.csproj`.
- **GSD workflow enforcement** — this phase's execution must go through `/gsd-execute-phase`, not direct edits.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-------------------|
| PERF-03 | Self-contained exe measurably smaller than the v2.1 baseline (49,356,430 bytes) via additional safe MSBuild-level levers only (no IL trimming, no Native AOT, no `PublishReadyToRun`) | Standard Stack / Code Examples give the exact, live-tested `<Target>` XML to add to `RigToggle.App.csproj`, with a measured before/after (49,367,342 → 46,770,879 bytes, 2,596,463 bytes / 5.26% additional reduction), clearing the 49,356,430-byte bar with margin to spare, using zero trimming/AOT/R2R |

</phase_requirements>

## Standard Stack

No new external dependencies are introduced by this phase. No `PackageReference` changes at all — D-01's publish-output audit found the further-size opportunity in the SDK's own WinForms reference-pack assemblies, not in this project's third-party packages (`WindowsDisplayAPI`, `NAudio.Wasapi` are both already minimal and unchanged).

### Core (unchanged, confirmed by direct file read this session)
| Library | Version | Purpose | Why unchanged |
|---------|---------|---------|--------------|
| `WindowsDisplayAPI` | 1.3.0.13 | CCD display topology control | `RigToggle.Windows.csproj` read directly — confirmed unchanged since v2.0. Measured 64 KB in the uncompressed publish output — [VERIFIED: /tmp/loose-publish/WindowsDisplayAPI.dll, this session] — negligible, no further audit opportunity. |
| `NAudio.Wasapi` | 2.3.0 | Audio endpoint enumeration/default-device COM interop support | `RigToggle.Windows.csproj` read directly — confirmed unchanged since v2.0 (already swapped from the `NAudio` meta-package at that phase). Measured 180 KB (`NAudio.Wasapi.dll`) + 188 KB (`NAudio.Core.dll`, transitive) = 368 KB combined [VERIFIED: /tmp/loose-publish/NAudio.{Wasapi,Core}.dll, this session] — negligible, no further audit opportunity. |

### Supporting (build/packaging only — no NuGet packages, one new MSBuild target)
| Property/Target | Where it lives | Purpose | Status |
|----------|----------------|---------|--------|
| `EnableCompressionInSingleFile` | `Properties/PublishProfiles/win-x64.pubxml` | Compresses embedded managed assemblies inside the single-file bundle | Already applied (v2.0), confirmed present by reading the file this session — unchanged. |
| `SatelliteResourceLanguages=en` | `RigToggle.App.csproj` | Excludes localized satellite resource assemblies | Already applied (v2.0), confirmed present by reading the file this session — unchanged. |
| `InvariantGlobalization=true` | `RigToggle.App.csproj` | Disables ICU culture-data loading | Already applied (v2.0), confirmed present by reading the file this session — unchanged. |
| **`RemoveUnusedDesignerAndVbAssemblies` target (NEW)** | `RigToggle.App.csproj` | Removes 7 named WinForms-Design/VisualBasic-compat assemblies from `@(ResolvedFileToPublish)` after publish-file resolution, before single-file bundling | **New this phase.** Live-tested this session: [VERIFIED: this session's own `dotnet publish` runs, exact byte counts below] — 2,596,463 bytes / 5.26% additional reduction, zero build warnings, zero source-code dependency on the removed assemblies (exhaustive grep, this session). |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Manually curated `ResolvedFileToPublish` deny-list (7 named files) | `PublishTrimmed=true` (would also remove these + much more) | Explicitly excluded — this is exactly the IL trimming CLAUDE.md rules out for this codebase's COM/P-Invoke surface. The deny-list approach gets a meaningful slice of trimming's benefit (unused reference-pack assemblies) with none of its risk (no reachability analysis at all). |
| Excluding 7 specific files by exact name | A broader wildcard/pattern-based exclusion (e.g., anything matching `*Design*`) | Rejected — a wildcard could accidentally catch a future dependency this project does need (e.g., if a future phase adds a `*.Design.*`-named assembly for an unrelated reason). Exact-name matching is more verbose but immune to that failure mode; matches this codebase's existing convention of being explicit rather than clever (see `18-RESEARCH.md`'s Pattern 1 "confirm dead before delete" discipline). |
| Skipping `DebugType=none` | Setting it anyway for "housekeeping" | Considered and rejected this session (new finding, not in prior research) — see Common Pitfalls Pitfall 2. The 167-byte exe savings isn't worth even a small reduction in debug.log stack-trace file:line diagnostics, per the same reasoning D-03 already applied to `UseSystemResourceKeys`. |

**Installation:** No `PackageReference` changes. The only file to edit is `src/RigToggle.App/RigToggle.App.csproj` (add the `<Target>` block — see Code Examples).

**Version verification:** No new packages, so no registry lookup is needed. `WindowsDisplayAPI` 1.3.0.13 and `NAudio.Wasapi`/`NAudio.Core` 2.3.0 were re-confirmed present and unchanged by reading `RigToggle.Windows.csproj` directly this session (see Sources).

## Package Legitimacy Audit

Not applicable — this phase introduces **zero new `PackageReference` entries** and changes **zero existing package versions**. D-01's mandated publish-output audit was performed (see Summary) and found the further-size opportunity in the SDK's own WinForms reference-pack assemblies (excluded via an MSBuild `<Target>`, not a package change), not in any `PackageReference`. `WindowsDisplayAPI` and `NAudio.Wasapi` were both re-verified present at their existing pinned versions by reading `RigToggle.Windows.csproj` directly — no drift, no swap needed, no legitimacy check required for unchanged, already-approved (v2.0) packages.

**Packages removed due to [SLOP] verdict:** none (no packages touched)
**Packages flagged as suspicious [SUS]:** none (no packages touched)

## Architecture Patterns

### System Architecture Diagram — where the new lever sits in the publish pipeline

```
dotnet publish RigToggle.App.csproj
        |
        v
[Compile RigToggle.Core/Windows/App -> managed DLLs]
        |
        v
[ComputeResolvedFilesToPublishList target]  <-- SDK resolves every file that
        |                                        will ship: app DLLs, PDBs,
        |                                        WindowsDisplayAPI.dll,
        |                                        NAudio.*.dll, the whole
        |                                        WinForms + BCL runtime pack
        |                                        (210 files, confirmed count
        |                                        this session), INCLUDING the
        |                                        7 unused Design/VB DLLs
        v
[NEW: RemoveUnusedDesignerAndVbAssemblies target, AfterTargets=
 ComputeResolvedFilesToPublishList]  <-- removes exactly 7 named files
        |                                from @(ResolvedFileToPublish)
        v
[GenerateSingleFileBundle / single-file bundler]  <-- only sees the
        |                                              already-reduced file
        |                                              list; embeds +
        |                                              compresses everything
        |                                              remaining
        v
RigToggle.App.exe  (49,367,342 bytes baseline -> 46,770,879 bytes after)
```

### Recommended Project Structure (no new files — one edit)
```
src/
└── RigToggle.App/
    └── RigToggle.App.csproj   <- EDIT: add <Target Name="RemoveUnusedDesignerAndVbAssemblies" ...>
                                    inside the existing <Project> element, after the current
                                    <ItemGroup> blocks, before </Project>
```

### Pattern 1: Hook `ComputeResolvedFilesToPublishList`, not a general `BeforePublish`/`AfterPublish` target
**What:** MSBuild's publish pipeline resolves the full file list (app output + all transitive/runtime-pack files) in a dedicated target called `ComputeResolvedFilesToPublishList`, populating the `@(ResolvedFileToPublish)` item group. The single-file bundler consumes that item group *after* this target runs. To remove a file from the final bundle without touching source or triggering trimming, hook `AfterTargets="ComputeResolvedFilesToPublishList"` and `Remove` matching items from `@(ResolvedFileToPublish)`.
**When to use:** Any time you need to exclude a specific, by-name-known file from a self-contained/single-file publish without IL trimming.
**Why this hook and not another:** Tested and confirmed working this session. An earlier attempt using `CustomAfterMicrosoftCommonTargets` pointing at an external `.targets` file produced **no effect at all** (byte-identical output to baseline) — that hook point does not intercept the SDK's publish-specific target imports the same way. `AfterTargets="ComputeResolvedFilesToPublishList"`, whether declared in a `Directory.Build.targets` file or inline inside the `.csproj` itself, **did** work — both produced byte-identical 46,770,879-byte output. Inline-in-`.csproj` is the recommended placement (no extra file, matches this project's existing single-file-per-project convention).
**Example (verified working, exact XML used and tested this session):**
```xml
<!-- Source: this research session — live-tested inline in RigToggle.App.csproj -->
<Target Name="RemoveUnusedDesignerAndVbAssemblies" AfterTargets="ComputeResolvedFilesToPublishList">
  <ItemGroup>
    <ResolvedFileToPublish Remove="@(ResolvedFileToPublish)" Condition="
      '%(FileName)%(Extension)'=='System.Windows.Forms.Design.dll' or
      '%(FileName)%(Extension)'=='System.Windows.Forms.Design.Editors.dll' or
      '%(FileName)%(Extension)'=='System.Design.dll' or
      '%(FileName)%(Extension)'=='System.Drawing.Design.dll' or
      '%(FileName)%(Extension)'=='Microsoft.VisualBasic.Core.dll' or
      '%(FileName)%(Extension)'=='Microsoft.VisualBasic.Forms.dll' or
      '%(FileName)%(Extension)'=='Microsoft.VisualBasic.dll'
    " />
  </ItemGroup>
</Target>
```

### Pattern 2: Confirm zero-usage via exhaustive grep before excluding any assembly (reused from 18-RESEARCH.md Pattern 1)
**What:** Before removing any assembly from the publish output, grep the entire `src/` tree for every namespace/type the assembly provides, not just its file name.
**When to use:** Before excluding any file via Pattern 1's technique — this is what makes the exclusion "manually curated" (safe) rather than "guessed" (risky).
**Confirmed this session:**
```bash
grep -rn "Microsoft.VisualBasic\|VisualBasic\." src --include="*.cs"          # 0 hits
grep -rln "ComponentModel.Design\|Drawing.Design\|System.Windows.Forms.Design" src --include="*.cs"  # 0 hits
grep -rln "PropertyGrid\|IDesigner\|ComponentDesigner" src --include="*.cs"   # 0 hits (the IContainer
                                                                                #  hits in MonitorTile.cs/
                                                                                #  ToggleSwitch.cs/*.Designer.cs
                                                                                #  are System.ComponentModel.
                                                                                #  IContainer — base BCL,
                                                                                #  unrelated to System.
                                                                                #  ComponentModel.Design)
```
**Why this matters:** `*.Designer.cs` file *names* are a WinForms Visual Studio convention (auto-generated `InitializeComponent()` partial classes) — they do **not** imply a runtime dependency on `System.Windows.Forms.Design.dll`. Confirmed by reading `MainForm.Designer.cs:8` directly — its `IContainer components` field is `System.ComponentModel.IContainer` (core BCL, ships in the base runtime, unaffected by this exclusion), not anything from the Design assembly.

### Anti-Patterns to Avoid
- **Wildcard-matching `*Design*` or `*VisualBasic*` instead of exact file names:** Would be more concise but risks silently catching a future legitimate dependency. Use the exact 7-name list above.
- **Applying the exclusion target globally (e.g., in a solution-wide `Directory.Build.targets`):** Scope it to `RigToggle.App.csproj` only — that's the only project that actually publishes a self-contained single-file exe; `RigToggle.Windows`/`RigToggle.Core`/the test projects don't publish this way and don't need the target.
- **Confusing this with `PublishTrimmed`:** This target does not analyze IL, does not use `ILLink`, and cannot strip a reachable COM/P-Invoke type by mistake — it only ever removes files matched by an exact, hand-picked name. Do not describe it as "a form of trimming" in code comments or task descriptions; that framing risks a future contributor conflating it with the excluded `PublishTrimmed` and either "completing the job" (dangerous) or reflexively reverting it (losing the win) based on a misunderstanding.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Verifying "is this assembly really unused" before excluding it | Trusting the assembly's *name* alone ("Design" sounds design-time-only, must be safe) | Exhaustive `grep -rn` across the entire `src/` tree for every namespace/type the assembly exposes, cross-checked by reading at least one hit's surrounding context (Pattern 2) | A name-only judgment call would have wrongly flagged `*.Designer.cs`'s `IContainer` usage as a dependency on `System.Windows.Forms.Design.dll` if not read carefully — the actual type is `System.ComponentModel.IContainer`, unrelated. |
| Measuring exe size before/after a lever | Estimating from a blog's generic percentage | Actually run `dotnet publish` with and without the change in this exact repo/environment and diff real byte counts | Same discipline as `18-RESEARCH.md` — this app's own dependency graph (WindowsDisplayAPI + NAudio.Wasapi + WinForms, no PropertyGrid/VB usage) differs from any generic example; only a live measurement is trustworthy. |

**Key insight:** The biggest risk in this phase is *not* finding a lever — it's finding one that *looks* free (a name match, a plausible-sounding blog claim) and applying it without verifying zero actual usage in this specific codebase. Every lever recommended or rejected in this research was backed by a live `dotnet publish` run and/or an exhaustive grep, not general "should be fine" reasoning.

## Common Pitfalls

### Pitfall 1: `EnableCompressionInSingleFile` still trades exe size for startup decompression cost (unchanged from v2.0, restated)
**What goes wrong:** A smaller exe is not free — compressed assemblies decompress into memory on every app start.
**Why it happens:** Already-applied lever from v2.0; the new Design/VB exclusion target in this phase does not add to this cost (it *removes* files before compression even runs, so if anything there is marginally *less* to decompress at startup, not more).
**How to avoid:** No new action needed — the phase's own success criterion #2 (cold autostart boot + toggle round trip on real rig hardware) already covers this, unchanged in scope from v2.0's Phase 18.
**Warning signs:** Same as `18-RESEARCH.md` Pitfall 1 — a noticeably slower `--tray` cold start would be the pre-existing compression tradeoff surfacing, not something this phase's new lever introduces.

### Pitfall 2: `DebugType=none` looks like a free lever but isn't — new finding this session
**What goes wrong:** It's tempting to add `<DebugType>none</DebugType>` alongside the new exclusion target since "no debug symbols" sounds like an obvious size win, matching the general internet consensus for "reduce .NET exe size" guides.
**Why it happens:** The bundled `RigToggle.App.exe` never embeds the loose `.pdb` files in the first place (confirmed both by `18-RESEARCH.md` and re-confirmed live this session: setting `DebugType=none`/`DebugSymbols=false` changed the bundled exe by only 167 bytes — noise, not a real reduction). The only actual effect is that the three `.pdb` files (79,228 bytes combined) stop being *generated at all* during CI/build — smaller loose build artifacts, not a smaller shipped exe.
**How to avoid:** Skip it. Setting `DebugType=none` also disables portable-PDB-based file:line enrichment in unhandled-exception stack traces — a real (if narrow) diagnostic capability this app's own `debug.log` feature benefits from, for a 167-byte win on the artifact that actually ships. This is the same tradeoff class D-03 already ruled against for `UseSystemResourceKeys` — apply the same judgment here.
**Warning signs:** If a future PR adds `DebugType=none` "for consistency" alongside the new exclusion target, flag it in review — it doesn't belong to the same risk/reward category as the Design/VB exclusion.

### Pitfall 3: Freshly measured baseline (49,367,342 bytes) differs slightly from the recorded v2.1 baseline (49,356,430 bytes) — re-measure at execution time, don't hardcode the old number
**What goes wrong:** The phase's own success criterion #1 references a specific recorded number (49,356,430 bytes). This session's live re-measurement of the *unmodified* current codebase (before any of this phase's changes) produced 49,367,342 bytes — 10,912 bytes *larger* than the recorded figure, even though no code changes have landed since v2.1 closed.
**Why it happens:** Most likely candidates, in rough order of likelihood: (a) minor .NET SDK patch-version drift between whenever the v2.1 baseline was captured and this session's SDK (10.0.302, confirmed via `dotnet --version` this session) — patch releases occasionally adjust runtime-pack file sizes by a few KB; (b) this session's measurement is a Linux-hosted cross-compile (`-p:EnableWindowsTargeting=true`) rather than a native Windows build — while `18-RESEARCH.md` established this cross-compile technique is reliable for *relative* before/after comparisons, it may not be byte-for-byte identical to a native `windows-latest` GitHub Actions runner's output (which is what `release.yml` actually uses for real releases). This is a plausible, non-alarming explanation, not confirmed with certainty this session.
**How to avoid:** The plan's verification task must capture a **fresh "before" byte count** at execution time (same repo state, same environment the "after" measurement will use) rather than asserting against the hardcoded 49,356,430 figure from CONTEXT.md. The comparison that actually matters for PERF-03's success criterion is *this specific before vs. this specific after*, both measured the same way, in the same session — exactly as `18-RESEARCH.md` did for its own baseline. The 49,356,430 number remains valid as the phase's minimum bar (this phase's result — 46,770,879 bytes either way — clears it), but should not be treated as the literal number to diff against pre/post in the phase's own verification artifact.
**Warning signs:** If a verification task hardcodes "49,356,430 → X bytes" instead of doing a live before/after in the same session, the reported percentage will be very slightly wrong (though direction and the pass/fail bar are unaffected).

### Pitfall 4: `mscordbi.dll`/`mscordaccore*.dll`/`createdump.exe` are a plausible-looking but already-closed lever — don't waste plan time re-investigating
**What goes wrong:** These CLR debugger-interface and crash-dump native components total ~3.9 MB in a *non*-single-file (`PublishSingleFile=false`) publish output, which could look like an obvious next target for exclusion.
**Why it happens:** They only appear in a *loose* (non-single-file) publish. Confirmed via a live diagnostic MSBuild target this session (`AfterTargets="ComputeResolvedFilesToPublishList"`, printing `@(ResolvedFileToPublish)` matched against these file names) that for this project's actual `PublishSingleFile=true` configuration, these three files are **not present** in `@(ResolvedFileToPublish)` at all — the runtime pack's own `DropFromSingleFile=true` metadata (documented behavior per `dotnet/runtime` issue #112584, confirmed via WebSearch this session) already excludes them before the bundler ever runs.
**How to avoid:** Do not add these three files to the exclusion target's deny-list — they're not there to remove, and attempting to `Remove` non-existent items from `@(ResolvedFileToPublish)` is a silent no-op that wastes a task without changing anything measurable.
**Warning signs:** If a future re-investigation finds these files present in `@(ResolvedFileToPublish)` for a *newer* .NET SDK version, that would mean the SDK's default behavior changed — re-verify with the diagnostic-message technique (Pattern 1) before assuming the finding above still holds.

## Code Examples

### Full recommended `RigToggle.App.csproj` edit (verified, exact byte-count effect measured)
```xml
<!-- Source: this research session — live-tested, byte counts confirmed real -->
<!-- Add this Target element inside the existing <Project> block, after the current
     <ItemGroup> elements (EmbeddedResource tray icons), before </Project> -->

<!-- Excludes WinForms Design-time and VisualBasic-compatibility assemblies that the
     Microsoft.NET.Sdk automatically pulls into UseWindowsForms=true self-contained
     publishes even though this app never uses PropertyGrid/IDesigner/VB interop
     (verified: zero references to System.ComponentModel.Design, System.Drawing.Design,
     or Microsoft.VisualBasic anywhere in src/, 24-RESEARCH.md). This is a manually
     curated deny-list of 7 specific, known-unused files, not IL trimming; it performs
     no reachability analysis and cannot misclassify the COM/P-Invoke marshalling code
     paths CLAUDE.md warns PublishTrimmed breaks. -->
<Target Name="RemoveUnusedDesignerAndVbAssemblies" AfterTargets="ComputeResolvedFilesToPublishList">
  <ItemGroup>
    <ResolvedFileToPublish Remove="@(ResolvedFileToPublish)" Condition="
      '%(FileName)%(Extension)'=='System.Windows.Forms.Design.dll' or
      '%(FileName)%(Extension)'=='System.Windows.Forms.Design.Editors.dll' or
      '%(FileName)%(Extension)'=='System.Design.dll' or
      '%(FileName)%(Extension)'=='System.Drawing.Design.dll' or
      '%(FileName)%(Extension)'=='Microsoft.VisualBasic.Core.dll' or
      '%(FileName)%(Extension)'=='Microsoft.VisualBasic.Forms.dll' or
      '%(FileName)%(Extension)'=='Microsoft.VisualBasic.dll'
    " />
  </ItemGroup>
</Target>
```

### Measurement commands used this session (reproducible, no Windows machine needed — same technique as 18-RESEARCH.md Pattern 3)
```bash
# Baseline (current codebase, unmodified, this session)
export PATH="$HOME/.dotnet:$PATH"
rm -rf src/RigToggle.App/bin/publish/win-x64
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release \
  -p:PublishProfile=win-x64 -p:EnableWindowsTargeting=true
ls -la src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
# -> 49367342 bytes

# After adding the RemoveUnusedDesignerAndVbAssemblies target to RigToggle.App.csproj:
rm -rf src/RigToggle.App/bin/publish/win-x64
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release \
  -p:PublishProfile=win-x64 -p:EnableWindowsTargeting=true
ls -la src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
# -> 46770879 bytes  (2,596,463 bytes / 5.26% smaller than the just-measured baseline;
#                      2,585,551 bytes / 5.24% smaller than the recorded v2.1 baseline
#                      of 49,356,430 bytes — clears PERF-03's success criterion #1)
```

### Diagnostic technique for auditing `@(ResolvedFileToPublish)` before excluding anything (used to rule out mscordbi/mscordaccore/createdump, and to confirm the 7 Design/VB files' presence)
```xml
<!-- Source: this research session — scratch diagnostic, not part of the shipped fix -->
<Target Name="ListPublishFiles" AfterTargets="ComputeResolvedFilesToPublishList">
  <Message Importance="high" Text="ALL-RESOLVED-COUNT: @(ResolvedFileToPublish->Count())" />
  <ItemGroup>
    <_DesignFiles Include="@(ResolvedFileToPublish)" Condition="
      $([System.String]::Copy('%(FileName)').Contains('Design')) or
      $([System.String]::Copy('%(FileName)').Contains('VisualBasic'))" />
  </ItemGroup>
  <Message Importance="high" Text="DESIGN-VB-FILES: @(_DesignFiles->'%(FileName)%(Extension)|%(DropFromSingleFile)')" />
</Target>
<!-- Output this session: ALL-RESOLVED-COUNT: 210
     DESIGN-VB-FILES: Microsoft.VisualBasic.Core.dll|;Microsoft.VisualBasic.Forms.dll|;
       Microsoft.VisualBasic.dll|;System.Design.dll|;System.Drawing.Design.dll|;
       System.Windows.Forms.Design.Editors.dll|;System.Windows.Forms.Design.dll|
     (empty %(DropFromSingleFile) confirms these 7 files are NOT already excluded by the
     SDK's own metadata — they will be embedded unless explicitly removed) -->
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Self-contained WinForms publish ships whatever the `UseWindowsForms=true` reference pack resolves, including Design-time/VB-compat assemblies never used at runtime | Explicit `ResolvedFileToPublish`-level deny-list removes exactly the 7 confirmed-unused assemblies before single-file bundling | This phase (proposed) | 2,596,463 bytes (5.26%) additional reduction, zero source changes, zero trimming risk |
| Assumed `mscordbi.dll`/`mscordaccore*.dll`/`createdump.exe` might be bundled bloat worth excluding | Confirmed (this session) these are already excluded from single-file publishes by the runtime pack's own `DropFromSingleFile=true` metadata — nothing to do | Always true for this SDK version; newly confirmed this session, not previously verified in `18-RESEARCH.md`/`STACK.md` | Closes a candidate lever definitively — saves a future planner from re-investigating it |
| Assumed self-contained WinForms might bundle the full WPF stack (`dotnet/sdk#4078`) | Confirmed (this session) not applicable to this project's current .NET 10 SDK — no `PresentationCore`/`PresentationFramework*`/`PresentationUI` present in the actual publish output | Always true for this SDK version; newly confirmed this session | Closes another candidate lever definitively |

**Deprecated/outdated:** N/A — this phase is purely additive/subtractive build configuration; no APIs are deprecated by these changes.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The 10,912-byte discrepancy between the recorded v2.1 baseline (49,356,430) and this session's fresh baseline measurement (49,367,342) is attributable to SDK patch-version drift and/or Linux-cross-compile-vs-native-Windows-build differences, not a code regression | Common Pitfalls Pitfall 3 | LOW — the phase's actual pass/fail bar (46,770,879 < 49,356,430) is cleared either way with over 2.5 MB of margin; the exact attribution doesn't change the plan's action, only the precision of the reported percentage. Confirm with a fresh native-Windows CI measurement at execution time regardless (Pitfall 3's recommendation), which resolves this either way. |
| A2 | Removing the 7 named WinForms-Design/VisualBasic-compat assemblies causes no runtime failure, based on exhaustive `grep` finding zero source references (this session) plus successful `dotnet build`/`dotnet publish` with no warnings | Architecture Patterns Pattern 2, Code Examples | MEDIUM — a `grep`-based absence check cannot rule out a purely reflection-based runtime dependency inside a *third-party* assembly this app *does* use (e.g., if `System.Windows.Forms.dll` itself lazily reflects into `System.Design.dll` for some rarely-hit code path this app happens to trigger, such as an uncommon `TypeConverter` resolution). This is exactly why the phase's existing success criterion #2 (full toggle round trip + cold autostart boot on real rig hardware) is the correct final gate for this specific lever — do not treat the grep-based confirmation alone as sufficient; the rig check is load-bearing here, more so than for the other (already-established, low-risk) levers. |

**If this table is empty:** N/A — two assumptions logged above; both point to the phase's own already-planned rig verification as the resolving step, not a new gap requiring separate user confirmation.

## Open Questions

None outstanding. The phase's one open empirical question this session (does the exclusion target actually reduce shipped bytes without breaking the build?) was resolved via live testing, not left open.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build/publish everything in this phase | Yes (this research environment) | 10.0.302 [VERIFIED: `dotnet --version`, this session] | — |
| `dotnet publish -r win-x64 --self-contained -p:EnableWindowsTargeting=true` cross-publish | Measuring exe size for this phase's lever without a Windows OS | Yes — confirmed this session, multiple successful runs, exact byte counts captured | — | — |
| Real Windows rig hardware (CCD display API, `IPolicyConfig` COM interop, autostart registration, WinForms `Settings`/tile UI) | Success criterion #2's functional verification (toggle round trip + cold autostart boot) | Not available in this research/planning environment (Linux) | — | None — expected, matches every prior exe-size/packaging phase (18) in this project, which reserved a final human-checkpoint wave for rig-only verification. Not a gap; a structural constraint of this project type. |
| GitHub Actions `windows-latest` runner (`release.yml`) | Actual release-artifact publish | Confirmed via direct read of `.github/workflows/release.yml` this session — runs `dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64` on `windows-latest`, no `EnableWindowsTargeting` flag needed there (native Windows build). The new `<Target>` requires no CI workflow changes — it is entirely inside `RigToggle.App.csproj` and any `dotnet publish` invocation on any platform will honor it. | — | — |

**Missing dependencies with no fallback:** None that block *planning* — the rig dependency is expected and already the established pattern for this project's final verification wave (see A2 above for why the rig check specifically matters for this phase's new lever).
**Missing dependencies with fallback:** None needed this session — all measurements were performed directly against the real repository with a real .NET SDK.

## Security Domain

> `security_enforcement` is not explicitly set to `false` in `.planning/config.json`, so this section is included per protocol. This phase has an unusually small security surface — no new user input handling, no network calls, no authentication/authorization/session logic, and no new third-party attack surface (zero `PackageReference` changes).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | N/A — single-user desktop utility, no auth surface, unaffected by this phase |
| V3 Session Management | No | N/A |
| V4 Access Control | No | N/A |
| V5 Input Validation | No | This phase adds no new user-input-handling code path (build config only) |
| V6 Cryptography | No | N/A |
| V14 Configuration | Marginal | The new `<Target>` is build/publish configuration, not runtime security configuration — no secrets, no exposed endpoints. The one relevant control: verify `PublishTrimmed` stays `false` and no elevation manifest is (re-)introduced while touching `RigToggle.App.csproj` (unrelated to this phase's actual change, but worth a regression check given the file is being edited). |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Removing an assembly that turns out to be a genuine runtime dependency, causing an unhandled `FileNotFoundException`/`TypeLoadException` at a code path not exercised by manual rig testing | (Availability/reliability, not a STRIDE security category, but a real correctness risk for a rig-critical tool) | Mitigated by (1) exhaustive grep confirming zero source-level usage, (2) the exclusion being a narrow, exact-name deny-list rather than a broad pattern, (3) the phase's own mandated full rig verification (toggle round trip + cold autostart boot) as the final gate — see A2 |
| Supply-chain risk from unnecessary dependency surface | Tampering (larger attack surface if an unused bundled assembly has a future CVE) | This phase's exclusion *reduces* this risk slightly — 7 fewer assemblies shipped in the final artifact, none of which this app's own code ever executes |

## Sources

### Primary (HIGH confidence)
- `src/RigToggle.App/RigToggle.App.csproj`, `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml`, `src/RigToggle.Windows/RigToggle.Windows.csproj`, `.github/workflows/release.yml` (read directly from this repo, this session) — confirmed current v2.1 publish configuration is unchanged from what `18-RESEARCH.md` documented, confirmed the CI publish command needs no changes for this phase's lever
- This research session's own live `dotnet publish` runs against the actual `RigToggle.sln`/`RigToggle.App.csproj` — every byte count in this document (49,367,342 baseline; 46,770,879 after; 167-byte `DebugType=none` delta; byte-identical `IncludeNativeLibrariesForSelfExtract=false` result) was measured directly, not estimated
- Exhaustive `grep -rn` of the entire `src/` tree for `Microsoft.VisualBasic`, `ComponentModel.Design`, `Drawing.Design`, `System.Windows.Forms.Design`, `PropertyGrid`, `IDesigner`, `ComponentDesigner` — zero relevant hits, performed this session
- `.planning/milestones/v2.0-phases/18-cleanup-pass-exe-size-reduction/18-RESEARCH.md` — direct precedent for methodology, cross-compile technique, and the four already-applied levers, re-verified still accurate this session

### Secondary (MEDIUM confidence)
- `.planning/research/STACK.md` (this milestone's v2.2 research) — flagged `UseSystemResourceKeys` (declined, D-03) and a per-package audit as remaining candidates, and correctly identified `DebugType=none` as a housekeeping-only, no-size-win change (re-confirmed live this session with the exact 167-byte delta)
- https://github.com/dotnet/runtime/issues/112584 ("Add property to include libmscordbi.so, libmscordaccore.so, and createdump when publishing self-contained, single file applications") — confirms `DropFromSingleFile=true` is the runtime pack's own mechanism for excluding these files from single-file bundles by default, corroborating this session's live diagnostic-target finding that these 3 files are absent from `@(ResolvedFileToPublish)` for this project
- https://github.com/dotnet/sdk/issues/4078 ("Self-contained deployment of WinForms always includes WPF assemblies") — checked directly against this project's actual publish output (no `PresentationCore.dll`/`PresentationFramework*.dll`/`PresentationUI.dll` present, only a 16 KB `WindowsBase.dll`) and found not applicable to the current .NET 10 SDK for this project

### Tertiary (LOW confidence)
- General WebSearch results on ".NET self-contained exe size reduction" guides — used only to generate candidate hypotheses (WPF-bundling issue, mscordbi/mscordaccore bloat) that were then independently verified or ruled out via direct measurement against this actual repository; no unverified claim from these results appears as a recommendation in this document

## Metadata

**Confidence breakdown:**
- Standard stack (unchanged v2.0 levers, no new packages): HIGH — every property/package re-verified live this session by reading the actual project files
- New lever (WinForms Design/VB exclusion target): HIGH — measured live, reproducible, exact byte counts given, both the working target-hook technique and a non-working alternative were tested so the recommendation isn't a first-guess
- Ruled-out levers (mscordbi/mscordaccore/createdump, WPF-bundling issue, `IncludeNativeLibrariesForSelfExtract=false`, `DebugType=none`): HIGH — each backed by a live diagnostic run or measurement this session, not assumption
- Runtime safety of the new lever (A2): MEDIUM — grep-based confirmation is strong but not exhaustive against reflection-based third-party dependencies; the phase's existing rig-verification success criterion is the correct final gate, and this document flags that explicitly rather than presenting the lever as risk-free

**Research date:** 2026-08-18
**Valid until:** 30 days (stable domain — MSBuild publish-pipeline target hooks and .NET SDK reference-pack contents don't change quickly within a single SDK feature band; re-verify the exact byte counts if significant time passes or the .NET SDK patch version changes before this phase executes, per Pitfall 3)
