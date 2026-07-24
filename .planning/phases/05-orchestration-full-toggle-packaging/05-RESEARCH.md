# Phase 5: Orchestration, Full Toggle & Packaging - Research

**Researched:** 2026-07-24
**Domain:** C# structured-result modeling for multi-step operations; .NET 10 self-contained single-file publish for WinForms
**Confidence:** HIGH

## Summary

This phase has two genuinely new pieces of work, both narrow and well-precedented:

1. **CORE-04 structured result** — `ToggleService.ToggleToRigMode()`/`ToggleToNormalMode()` must stop throwing on step failure and instead return a small result object describing per-step outcomes (succeeded / failed-with-reason / not-attempted). C# 13/.NET 10 has **no native discriminated-union support** (that ships in C# 15/.NET 11, still in preview as of this research) — the correct idiom here is a plain `enum` + `sealed record`, which also matches this codebase's existing house style (`AudioRoleState`, `MonitorPathSnapshot`, `StateSnapshot` — all tiny POCO-shaped `sealed record`s with no inheritance or generics). No third-party discriminated-union package (e.g. `OneOf`) is needed or justified.

2. **PACKAGING-01 publish profile** — `dotnet publish` with `SelfContained=true`, `PublishSingleFile=true`, `RuntimeIdentifier=win-x64`, `PublishTrimmed=false` is the correct, current (.NET 10) mechanism and matches CLAUDE.md's existing guidance exactly. There is one **non-obvious, verified pitfall**: `dotnet publish -p:PublishProfile=<name>` does **not** honor `RuntimeIdentifier` (or `TargetFramework`/`Configuration`/`Platform`) when those are set only inside the `.pubxml` file — that property is Visual-Studio-only for `.pubxml` consumption. The safe fix is to put `RuntimeIdentifier` directly in `RigToggle.App.csproj`'s `PropertyGroup` (not only in the `.pubxml`), or always pass `-r win-x64 --self-contained true` explicitly on the CLI alongside `-p:PublishProfile=win-x64`. This project already only ever targets `dotnet publish` via CLI (per CLAUDE.md: "prefer driving the final build via CLI even if you develop in VS Code"), so this pitfall is directly relevant and must be addressed in the plan, not just noted.

Additionally, to get a literal **single output `.exe` file** (not an exe plus a few loose native-runtime DLLs sitting next to it), `IncludeNativeLibrariesForSelfExtract=true` should also be set — without it, `PublishSingleFile=true` still leaves the .NET host's own native binaries (e.g. `hostfxr.dll`) as separate files in the publish directory. This project has no third-party *native* library dependencies of its own (`WindowsDisplayAPI` and `NAudio` are both pure managed P/Invoke/COM-interop wrappers calling OS-provided DLLs, not bundlers of native DLLs), so this flag only affects the .NET runtime's own native pieces — safe to enable.

**Primary recommendation:** Model the structured result as `ToggleStepOutcome` (enum: Succeeded/Failed/NotAttempted) + `ToggleStepResult` (record: StepName, Outcome, Reason) + `ToggleResult` (record wrapping `IReadOnlyList<ToggleStepResult>` with a computed `Success` property), placed in `RigToggle.Core/Models/` alongside the existing model records. For packaging, add `RuntimeIdentifier` to `RigToggle.App.csproj` directly (not only the `.pubxml`) to sidestep the CLI/pubxml RID gap, keep `Properties/PublishProfiles/win-x64.pubxml` for the remaining publish-only properties (`SelfContained`, `PublishSingleFile`, `PublishTrimmed=false`, `IncludeNativeLibrariesForSelfExtract=true`), and document the exact `dotnet publish -p:PublishProfile=win-x64` (or equivalent explicit-flag) command in a short README.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CORE-01 | Toggle to rig mode with one GUI action | Already wired (`MainForm.BtnToggle_Click` → `ToggleService.ToggleToRigMode()`); this phase only changes the *return contract*, not the trigger — verification only, no new research needed. |
| CORE-02 | Toggle back to normal mode with one GUI action | Same as CORE-01, mirrored for `ToggleToNormalMode()` — verification only. |
| CORE-03 | Snapshot captured before any mutation | Already implemented (`_snapshotStore.Save()` precedes all mutating calls in `ToggleToRigMode()`) — verification only, no design change from this phase's structured-result work. |
| CORE-04 | Partial-failure reporting: report succeeded/failed steps, stop, no auto-revert | New this phase. See "Standard Stack" (record/enum shape) and "Architecture Patterns" (stop-on-first-failure vs. isolate-and-continue result construction) below. |
| CORE-05 | Correct mode detection after crash | Already implemented (`IsInRigMode()` derives from snapshot-file presence, D-14) — verification-only checkpoint (kill process while in rig mode, relaunch). No design research needed; see "Environment Availability" for why this must be verified on the Windows rig, not in this sandbox. |
| PACKAGING-01 | Standalone .exe, no separate runtime install | New this phase. See "Standard Stack" (publish properties) and "Common Pitfalls" (pubxml/RID gap, native-library extraction) below. |

</phase_requirements>

## Architectural Responsibility Map

This project has no browser/CDN/database tiers (single-user Windows desktop utility). Tiers below are adapted to the project's actual layering (established in Phases 2-4, unchanged by this phase).

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Structured result construction (step outcomes) | `RigToggle.Core` (`ToggleService`) | — | Business/orchestration logic owns the sequencing and knows which step failed; zero Windows API references live here (unchanged constraint from Phase 2). |
| Result rendering (checklist MessageBox) | `RigToggle.App` (`MainForm`) | — | Presentation-only; consumes the result object, builds display strings. Never constructs a `ToggleResult` itself. |
| Per-step mutation calls (disable monitor, switch audio, launch app) | `RigToggle.Windows` (adapters) | `RigToggle.Core` (interfaces) | Unchanged this phase — `IMonitorController`/`IAudioController`/`IAppController` signatures are not touched; `ToggleService` still calls them the same way, just wraps each call in try/catch for result-tracking. |
| Publish/packaging configuration | Build tier (`.csproj` / `.pubxml`) | — | MSBuild property surface, not application code — new project-file territory untouched since Phase 2's scaffold. |

## Standard Stack

### Core (no new packages)

This phase introduces **zero new NuGet packages**. It only adds new C# types (records/enum) to `RigToggle.Core` and new MSBuild publish configuration to `RigToggle.App.csproj`. All packages already present (`WindowsDisplayAPI 1.3.0.13`, `NAudio 2.3.0`, `xunit 2.9.2`, `Microsoft.NET.Test.Sdk 17.12.0`) are unaffected and out of scope for re-verification here — they were already verified in Phases 2-4 research.

### Supporting

| Item | Where it lives | Purpose | Why Standard |
|------|-----------------|---------|---------------|
| `ToggleStepOutcome` (enum) | `RigToggle.Core/Models/ToggleStepOutcome.cs` (new) | Represents Succeeded / Failed / NotAttempted per step | Idiomatic C# for a small closed set of states when no data needs to travel with the case itself; matches this codebase's plain-POCO house style. `[ASSUMED — exact 3-value shape is design guidance, not a verified external source; confirmed only that discriminated unions are unavailable in C# 13, not that this specific enum shape is used elsewhere]` |
| `ToggleStepResult` (record) | `RigToggle.Core/Models/ToggleStepResult.cs` (new) | `(string StepName, ToggleStepOutcome Outcome, string? Reason)` — one step's outcome | Mirrors `AudioRoleState`/`MonitorPathSnapshot` — small `sealed record` with a one-line XML-doc summary, nullable `Reason` (only populated on Failed) same pattern as nullable fields elsewhere in `Models/`. `[ASSUMED — design guidance based on codebase precedent, not an external citation]` |
| `ToggleResult` (record) | `RigToggle.Core/Models/ToggleResult.cs` (new) | Wraps `IReadOnlyList<ToggleStepResult> Steps` with a computed `bool Success` | Single return type shared by both `ToggleToRigMode()` and `ToggleToNormalMode()`, consumed identically by `MainForm` regardless of direction (D-03). `[ASSUMED]` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Plain enum + record | `OneOf<Success, Failure, NotAttempted>` (OneOf NuGet package) | Adds a dependency for a 3-case closed set that a plain enum already models exactly; violates this project's established no-unnecessary-dependency posture (see `02-02-PLAN.md`: "no Moq/NSubstitute"). Not recommended. |
| Plain enum + record | Wait for C# 15 `union` keyword (.NET 11) | Not available — C# 15/.NET 11 unions are still in preview (Preview 2, ~April 2026) with GA targeted November 2026; this project targets .NET 10/C# 13 today. `[CITED: learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/unions]` |
| One `ToggleResult` type for both directions | Separate `RigModeResult`/`NormalModeResult` types | CONTEXT.md D-03 explicitly locks a single shared shape/UX for both directions — do not split. |

**Installation:** N/A — no new packages.

## Package Legitimacy Audit

**Not applicable this phase.** No external packages are introduced, upgraded, or removed by Phase 5's work (structured-result types are hand-written C#; packaging config is MSBuild properties only, referencing no new NuGet packages). The slopcheck/registry-verification protocol is skipped because there is nothing to audit.

## Architecture Patterns

### System Architecture Diagram

```
BtnToggle_Click (MainForm)
        │
        ▼
IsInRigMode()? ──false──► preflight guards (IsSettingsConfigured, monitor-confirm dialog)
        │true                      │ (unchanged — still throw/return early, NOT part of ToggleResult)
        ▼                          ▼
ToggleService.ToggleToNormalMode()   ToggleService.ToggleToRigMode()
        │                                   │
        │  [isolate-and-continue]           │  [stop-on-first-failure]
        ▼                                   ▼
  ┌─────────────┐                    ┌──────────────┐
  │ Monitor     │──try/catch─┐       │ Monitor      │──throws?──┐
  │ Restore     │            │       │ Disable      │           │
  └─────────────┘            ▼       └──────────────┘           │(stop, mark
  ┌─────────────┐    record outcome  ┌──────────────┐           │ remaining
  │ Audio       │──try/catch─┐       │ Audio        │           │ steps
  │ Restore     │  (swallow) │       │ SetDefault   │◄──────────┘ NotAttempted)
  └─────────────┘            ▼       └──────────────┘
  ┌─────────────┐    record outcome  ┌──────────────┐
  │ App         │  (skipped if       │ App          │
  │ Minimize    │   monitor failed)  │ LaunchOrFocus│
  └─────────────┘                    └──────────────┘
        │                                   │
        ▼                                   ▼
  ToggleResult { Steps: [...] } ◄───────────┘
        │
        ▼
MainForm renders checklist MessageBox (per-step outcome lines)
```

### Recommended Project Structure (additions only)

```
src/RigToggle.Core/Models/
├── ToggleStepOutcome.cs   # new — enum Succeeded/Failed/NotAttempted
├── ToggleStepResult.cs    # new — record (StepName, Outcome, Reason)
└── ToggleResult.cs        # new — record wrapping IReadOnlyList<ToggleStepResult>

src/RigToggle.App/
└── Properties/PublishProfiles/win-x64.pubxml   # new

README.md                  # new — repo root, documents publish command
```

### Pattern 1: Stop-on-first-failure result construction (`ToggleToRigMode`)

**What:** Build the full ordered step list up front as constants (`"Monitor"`, `"Audio"`, `"App"`), iterate with an index, try each mutation; on the first exception, record `Failed` with the exception message as `Reason`, then mark every remaining step `NotAttempted` and stop — do not attempt subsequent steps, do not roll back completed ones (D-04/D-06).

**When to use:** `ToggleToRigMode()` only — forward-direction steps have real ordering dependencies (D-04's rationale: no point switching audio if the monitor didn't disable).

**Example (illustrative shape, not exact code):**
```csharp
// RigToggle.Core/ToggleService.cs
private static readonly string[] RigModeStepOrder = { "Monitor", "Audio", "App" };

public ToggleResult ToggleToRigMode()
{
    // ...existing preflight guards (unchanged — still throw InvalidOperationException,
    // NOT part of the structured result; see Open Questions) ...

    var steps = new List<ToggleStepResult>();
    var remaining = new Queue<string>(RigModeStepOrder);

    void RunStep(string name, Action action)
    {
        remaining.Dequeue(); // consume this step's slot
        try
        {
            action();
            steps.Add(new ToggleStepResult(name, ToggleStepOutcome.Succeeded, null));
        }
        catch (Exception ex)
        {
            steps.Add(new ToggleStepResult(name, ToggleStepOutcome.Failed, ex.Message));
            foreach (var skipped in remaining)
                steps.Add(new ToggleStepResult(skipped, ToggleStepOutcome.NotAttempted, null));
            remaining.Clear();
        }
    }

    RunStep("Monitor", () => _monitorController.Disable(settings.MonitorDevicePath!));
    if (remaining.Count == RigModeStepOrder.Length - 1) // Monitor didn't fail
        RunStep("Audio", () => _audioController.SetDefault(settings.RigAudioDeviceId!));
    // ... App step similarly guarded ...

    return new ToggleResult(steps);
}
```
*(This is illustrative pseudo-shape for the planner, not final code — a cleaner mechanism, e.g. a small local helper that short-circuits once any step has failed, is a legitimate implementation detail left to the planner/executor.)*

### Pattern 2: Isolate-and-continue result construction (`ToggleToNormalMode`)

**What:** Preserve the *exact* existing control flow (monitor try/catch, audio try/catch-and-swallow, conditional stop-before-minimize-and-clear on monitor failure per D-05) — only change the *reporting mechanism* from throw/swallow to structured-result accumulation. The existing behavior where a monitor-restore failure skips `MinimizeIfRunning`/`Clear` entirely must be preserved and now shows up as `App: NotAttempted` and (implicitly) no snapshot-clear step in the result, rather than a thrown exception.

**When to use:** `ToggleToNormalMode()` only.

**Key constraint:** D-05 requires this asymmetry be documented inline (XML-doc comment) so a future reader doesn't "fix" it into stop-on-first-failure symmetry with `ToggleToRigMode`.

### Anti-Patterns to Avoid

- **Collapsing both directions into one shared step-runner helper that treats them identically:** D-04/D-05 are a *deliberate* asymmetry — a shared helper is fine for the *result type*, but the *control flow* (stop vs. isolate) must stay direction-specific. Don't refactor away the difference in the name of DRY.
- **Using exceptions for step-failure signaling internally:** internal `try/catch` per step is fine and necessary (to catch adapter exceptions), but the *public* contract must be the `ToggleResult`, not a re-thrown exception — except for the preflight guards, which intentionally stay exception-based (see Open Questions).
- **Reaching for a discriminated-union NuGet package:** unnecessary for a 3-case closed set; adds a dependency this personal-tool project doesn't need (see Alternatives Considered).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Representing a closed set of 3 outcomes | A custom class hierarchy with `is`-pattern-matching base/derived types mimicking a union | Plain `enum ToggleStepOutcome` | C# 13 has no union type; an enum is the simplest correct primitive for a fixed, non-data-carrying case set. Only reach for a base/derived hierarchy if each case needs materially different data shapes (not the case here — every step just needs Name+Outcome+optional Reason). |
| Self-contained single-file Windows packaging | Hand-rolled ILMerge/Costura-style DLL-merging, or a custom bootstrapper .exe that extracts embedded resources | `PublishSingleFile`/`SelfContained` MSBuild properties (built into the SDK since .NET 6, unchanged mechanism in .NET 10) | This is a first-class, officially supported SDK feature — no library needed at all, let alone hand-rolling. `[CITED: learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview]` |

**Key insight:** Both of this phase's "new" problems (result modeling, single-file packaging) have zero-dependency, SDK-native solutions. Nothing here justifies adding a package.

## Common Pitfalls

### Pitfall 1: `RuntimeIdentifier` in `.pubxml` is silently ignored by `dotnet publish` CLI
**What goes wrong:** A `.pubxml` file sets `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` (along with `SelfContained`/`PublishSingleFile`), the team runs `dotnet publish -p:PublishProfile=win-x64`, and the publish either fails (SDK requires an explicit `--self-contained`/`--no-self-contained` choice once any RID is in play) or silently produces a framework-dependent / wrong-RID build, because the CLI does not read `RuntimeIdentifier` (or `TargetFramework`/`Configuration`/`Platform`) out of `.pubxml` files — those specific properties are Visual-Studio-Publish-dialog-only.
**Why it happens:** Documented, current (.NET 10-era docs, updated 2026-02) limitation: "The most notable `.pubxml` properties that aren't supported by `dotnet publish`... `RuntimeIdentifier`, `RuntimeIdentifiers`" among others. `[CITED: learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish, "## .pubxml files" section]`
**How to avoid:** Put `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` directly in `RigToggle.App.csproj`'s own `PropertyGroup` (unconditioned — this project only ever targets one RID per D-09, so there's no multi-RID conflict), so it's honored regardless of whether publish is invoked via `dotnet publish` CLI, `dotnet publish -p:PublishProfile=win-x64`, or Visual Studio's Publish dialog. Keep the remaining publish-only properties (`SelfContained`, `PublishSingleFile`, `PublishTrimmed=false`, `IncludeNativeLibrariesForSelfExtract`) in the `.pubxml` — those *are* honored by the CLI. Alternatively (if the planner prefers to keep the csproj RID-free), always document and use the explicit-flag command form: `dotnet publish -p:PublishProfile=win-x64 -r win-x64 --self-contained true` — the flags override/supply what the pubxml alone can't guarantee via CLI.
**Warning signs:** Publish output folder lacks the expected `.exe`/runtime files, or `dotnet publish` emits a warning about needing `--self-contained`/`--no-self-contained`, or the produced exe still requires a .NET runtime install on a clean machine.

### Pitfall 2: `PublishSingleFile=true` alone does not produce a literal single file
**What goes wrong:** After publishing, the output folder contains the main `.exe` plus a handful of loose native DLLs (e.g. `hostfxr.dll` and similar .NET host native components) — not the "one file to copy anywhere" outcome the name implies.
**Why it happens:** Per official docs: "Only managed DLLs are bundled with the app into a single executable... the native binaries of the core runtime itself are separate files." Embedding those too requires `IncludeNativeLibrariesForSelfExtract=true`. `[CITED: learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview, "### Native libraries" section]`
**How to avoid:** Set `IncludeNativeLibrariesForSelfExtract=true` in the publish profile. This project has no bundled third-party *native* DLLs of its own to worry about (`WindowsDisplayAPI` and `NAudio` are managed-only, calling OS-provided DLLs via P/Invoke/COM — nothing to extract from them specifically); the flag only affects the .NET host/runtime's own native components. Cost: those native components extract to `%TEMP%\.net\<appname>\<hash>` on first run of a given build — negligible for a manually-launched personal utility, and does not affect the managed P/Invoke or COM-interop code paths (those call into OS system DLLs already present on the machine, not into anything bundled).
**Warning signs:** Publish output directory has more than one file for a "single-file" configuration.

### Pitfall 3: `PublishTrimmed` reintroducing itself accidentally
**What goes wrong:** A future contributor (or an IDE "Publish" wizard) adds `PublishTrimmed=true` to reduce the ~150MB exe size, silently breaking the COM interop (`IPolicyConfig`) or P/Invoke marshalling (`WindowsDisplayAPI`) at runtime — a failure mode that is very hard to repro without rig hardware, and one CLAUDE.md already explicitly warns against.
**Why it happens:** IL trimming's static reachability analysis cannot see through COM interop or dynamically-invoked P/Invoke patterns reliably; Microsoft's own trimming docs explicitly call out "built-in COM" as a common source of trim-analysis failures. `[CITED: learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained, "Components that cause trimming problems" section]`
**How to avoid:** Explicitly set `<PublishTrimmed>false</PublishTrimmed>` in the `.pubxml` (even though `false` is the SDK default — being explicit documents intent and prevents silent reintroduction) per CLAUDE.md's existing directive and D-07. Note: trimming is *opt-in* in .NET (never happens unless explicitly enabled), so the risk is specifically a *future addition*, not an accidental default.
**Warning signs:** Sudden `TypeLoadException`/`MissingMethodException`/`COMException` at runtime for CCD or audio-switch calls specifically, with no corresponding code change — a classic trim-related regression signature.

### Pitfall 4: Preflight-guard exceptions vs. structured-result steps getting conflated
**What goes wrong:** If the planner routes `ToggleToRigMode()`'s existing preflight guards (`IsFullyConfigured` check, `File.Exists(CompanionAppPath)` check — both currently `throw new InvalidOperationException(...)` *before* any step begins) into the new `ToggleResult` as a synthetic "Preflight" step, `MainForm`'s existing generic catch-block handling for these two specific messages (with their carefully-worded, actionable user-facing text) gets bypassed or duplicated, and the checklist UI has to special-case a step that isn't really part of the "which of the 3 mutation steps succeeded" story CORE-04 is about.
**Why it happens:** These preflight guards were written pre-CORE-04 (Phase 3, D-05) with their own good user-facing messages, and structurally happen *before* the step sequence, not as part of it.
**How to avoid:** See Open Questions — recommend keeping preflight guards as-is (still throw, still caught by `MainForm`'s existing generic `catch (Exception ex)` block), and scope `ToggleResult`/the checklist strictly to the 3 mutation steps (Monitor/Audio/App) that D-01/D-02/D-03 describe. This is Claude's-discretion territory per CONTEXT.md but worth flagging explicitly so the planner makes the call deliberately rather than by accident.

## Code Examples

### Minimal record/enum shape (house-style match)
```csharp
// Source: house-style pattern, modeled on src/RigToggle.Core/Models/AudioRoleState.cs
// and src/RigToggle.Core/Models/StateSnapshot.cs (both read directly this session)
namespace RigToggle.Core.Models;

public enum ToggleStepOutcome { Succeeded, Failed, NotAttempted }

/// <summary>One toggle step's outcome — step name, result, and (if Failed) the reason.</summary>
public sealed record ToggleStepResult(string StepName, ToggleStepOutcome Outcome, string? Reason);

/// <summary>
/// Full outcome of a ToggleToRigMode/ToggleToNormalMode call — ordered per-step results,
/// consumed identically by MainForm regardless of toggle direction (D-03).
/// </summary>
public sealed record ToggleResult(IReadOnlyList<ToggleStepResult> Steps)
{
    public bool Success => Steps.All(s => s.Outcome == ToggleStepOutcome.Succeeded);
}
```

### Publish profile
```xml
<!-- Source: pattern synthesized from learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
     and learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish -->
<!-- src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml -->
<Project>
  <PropertyGroup>
    <PublishDir>bin\publish\win-x64\</PublishDir>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>
</Project>
```

```xml
<!-- src/RigToggle.App/RigToggle.App.csproj — RuntimeIdentifier added directly here,
     NOT only in the pubxml, per Pitfall 1 -->
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

### Publish command (README)
```bash
# From repo root, or from src/RigToggle.App/
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64
```
*(Given the `RuntimeIdentifier` fix above lives in the csproj itself, no separate `-r win-x64` flag is required on the command line — but documenting `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishProfile=win-x64` explicitly in the README as a belt-and-suspenders fallback is reasonable given Pitfall 1.)*

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| ILMerge / Costura.Fody for "single exe" packaging | SDK-native `PublishSingleFile` | .NET Core 3.0 (2019), matured through .NET 6+ | No third-party merging tool needed; this project already avoids both, consistent with research. |
| `PublishTrimmed` opt-out by omission only | Still opt-in only (unchanged) | N/A | Confirms CLAUDE.md's "explicitly set to false" instruction is a defensive/documentation choice, not a behavior change — trimming was never going to happen by accident. `[CITED: learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained]` |
| Manual union-via-inheritance patterns | Native `union` keyword (C# 15/.NET 11) | Preview as of ~April 2026, GA targeted November 2026 | Not usable for this project (.NET 10/C# 13) — noted for awareness only, do not attempt to adopt early. |

**Deprecated/outdated:** None directly relevant — the .NET single-file/trim mechanism this phase relies on has been stable since .NET 6 with no breaking changes through .NET 10.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | Exact 3-type shape (`ToggleStepOutcome`/`ToggleStepResult`/`ToggleResult`) is the right decomposition | Standard Stack, Code Examples | Low — CONTEXT.md explicitly leaves "exact structured-result type shape" to planner's discretion; this is a recommendation, not a locked design. Planner/executor may reshape without violating any locked decision. |
| A2 | Preflight-guard exceptions (unconfigured settings, missing companion app path) should stay exception-based rather than folding into `ToggleResult` | Common Pitfalls #4, Open Questions | Medium — if the planner decides otherwise, `MainForm`'s existing tested error messages for these two cases need to be re-plumbed through the checklist UI instead of the generic catch block; not a correctness risk, but a scope/effort risk if assumed away. |
| A3 | `IncludeNativeLibrariesForSelfExtract=true` is safe/desirable given this project's specific (managed-only) dependency set | Common Pitfalls #2, Code Examples | Low — verified via official docs that the flag's effect is scoped to native runtime components; this project's actual P/Invoke/COM calls target OS-provided DLLs already present on any Windows machine, unaffected by this flag either way. |

## Open Questions

1. **Do preflight-guard failures (unconfigured settings / missing companion app path) belong in the `ToggleResult` checklist, or stay as separately-thrown/caught exceptions?**
   - What we know: CONTEXT.md's D-01/D-02/D-03 describe the checklist in terms of the 3 mutation steps (Monitor/Audio/App); the preflight guards are structurally *before* any step runs and already have well-crafted, tested user-facing messages (WR-01, D-05 from Phase 3).
   - What's unclear: CONTEXT.md doesn't explicitly say whether "not attempted" should ever cover the *entire* toggle (i.e., zero steps even started) versus only steps after a failure within an attempted sequence.
   - Recommendation: Keep preflight guards exception-based (unchanged), scope `ToggleResult` strictly to the 3 mutation steps. This keeps existing tested messages/behavior stable and matches the literal wording of D-01 ("Monitor: ... / Audio: ... / App: ..."). Planner should make this decision explicit in the plan rather than leaving it implicit.

2. **Should the `.pubxml`'s `RuntimeIdentifier` gap (Pitfall 1) be fixed by moving `RuntimeIdentifier` into the `.csproj`, or by always documenting explicit `-r win-x64 --self-contained true` CLI flags?**
   - What we know: Both fixes work; moving it to the `.csproj` is more robust (works regardless of invocation method) but is a slightly bigger csproj-surface-area change than CONTEXT.md's D-07 literally described ("Publish configuration lives in a `PublishProfiles/win-x64.pubxml`").
   - What's unclear: Whether the user considers "RuntimeIdentifier lives in the csproj, not the pubxml" still compliant with D-07's spirit (it is a publish-relevant property, just placed where it actually works) or a deviation worth flagging back to them.
   - Recommendation: Put `RuntimeIdentifier` in the `.csproj` (Code Examples above) — it's the more robust fix and D-07's "or equivalent PropertyGroup" phrasing already anticipates PropertyGroup-based configuration as an acceptable alternative. Document the reasoning inline (comment) so it isn't mistaken for scope creep.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|--------------|-----------|---------|----------|
| .NET 10 SDK / `dotnet` CLI | Build, test, publish for this entire phase | ✗ (this sandbox) | — | All build/publish/run/test verification must happen on the Windows rig — confirmed by `dotnet --version` exiting 127 (command not found) in this Linux sandbox. This matches every prior phase's documented execution boundary (01/02/03/04). |
| Windows 10/11 | Runtime target; `SetDisplayConfig`/`IPolicyConfig`/Win32 window APIs | ✗ (this sandbox is Linux) | — | N/A — no fallback exists or is needed; this is a Windows-only utility by design (see CLAUDE.md Constraints). |

**Missing dependencies with no fallback:**
- `dotnet` SDK / Windows runtime — blocks all actual build/publish/execution verification in this sandbox. The plan MUST include explicit `checkpoint:human-verify` tasks for: (a) `dotnet build` succeeding, (b) `dotnet test` passing (existing + new `ToggleServiceTests` assertions on `ToggleResult`), (c) `dotnet publish -p:PublishProfile=win-x64` producing a working single-file exe, (d) the exe launching and completing a full rig-mode/normal-mode round trip on the actual rig hardware, and (e) the CORE-05 kill-process-while-in-rig-mode-then-relaunch checkpoint — all consistent with every prior phase's convention.

**Missing dependencies with fallback:** None — grep-based static verification (matching exact patterns/shapes) remains available for reviewing generated code in this sandbox, as used in every prior phase, but is not a substitute for the checkpoints above.

## Security Domain

`security_enforcement` is not set in `.planning/config.json` (absent = enabled per policy), so this section is included per protocol. This phase's actual new surface area (result-object plumbing, MSBuild publish config) introduces no new attack surface — no new network calls, no new user input parsing, no new file I/O beyond what already exists.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|-------------------|
| V2 Authentication | No | Single-user local desktop tool, no auth surface — unchanged by this phase. |
| V3 Session Management | No | No sessions — unchanged. |
| V4 Access Control | No | No multi-principal access model — unchanged (asInvoker-only, no elevation, per existing csproj comment). |
| V5 Input Validation | No (unchanged) | This phase adds no new external input paths — `ToggleResult`/`ToggleStepResult` carry only internal data (step names, exception messages already being surfaced verbatim in `MainForm` today per D-13). |
| V6 Cryptography | No | No cryptographic operations in this project at all. |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| Exception message text surfaced verbatim in the new checklist UI could theoretically leak local file-path/environment details | Information Disclosure | Already an accepted, deliberate tradeoff per existing D-13/T-02-FAKEFAIL rationale ("surfacing the real error is more useful than hiding it, especially for CCD-mutation failures... single-user diagnostic tool, not a hardened multi-user app") — no change needed, just carry the same posture into the new checklist rendering. Not a new risk introduced by this phase. |
| Self-contained single-file exe extracting native components to `%TEMP%\.net` at runtime (Pitfall 2) | Tampering (if `%TEMP%` were writable by a lower-privileged/different-context principal) | Standard .NET guidance already covers this for the general case (don't use world-writable extraction dirs like `/tmp` on Linux); on a single-user Windows machine with a per-user `%TEMP%`, this is not an elevated risk for this project's threat model. No action needed. |

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview — fetched directly this session; confirmed `PublishSingleFile`/`SelfContained`/`RuntimeIdentifier` property meanings, native-library extraction behavior, `IncludeNativeLibrariesForSelfExtract`/`IncludeAllContentForSelfExtract`, extraction-directory behavior
- https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained — fetched directly this session; confirmed trimming is opt-in only (`PublishTrimmed` defaults false), confirmed built-in COM as an explicit trim-hazard category
- https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish — fetched directly this session; confirmed `.pubxml` folder convention (`Properties/PublishProfiles/<name>.pubxml`), confirmed `RuntimeIdentifier`/`TargetFramework`/`Configuration`/`Platform` are NOT honored by `dotnet publish` CLI when set only in `.pubxml`
- https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/unions — confirmed C# unions are a preview-stage proposal, not part of C# 13/.NET 10
- Direct codebase reads this session: `src/RigToggle.Core/ToggleService.cs`, `src/RigToggle.App/MainForm.cs`, `src/RigToggle.Core/Models/{AudioRoleState,MonitorPathSnapshot,StateSnapshot}.cs`, `src/RigToggle.App/RigToggle.App.csproj`, `src/RigToggle.Core/RigToggle.Core.csproj`, `src/RigToggle.Windows/RigToggle.Windows.csproj`, `src/RigToggle.Tests/ToggleServiceTests.cs`, `RigToggle.sln`

### Secondary (MEDIUM confidence)
- WebSearch cross-verification of the `.pubxml`/`RuntimeIdentifier`/`dotnet publish` gap (multiple independent sources, incl. `dotnet/sdk` GitHub issues, agree with the official docs finding)
- WebSearch for C# 15/.NET 11 unions timeline (blog posts dated 2026, cross-checked against the official language-reference proposal page)

### Tertiary (LOW confidence)
- None used as load-bearing claims in this document.

## Metadata

**Confidence breakdown:**
- Standard stack (result-type shape): MEDIUM-HIGH — the enum/record mechanics are HIGH confidence (verified language facts), the specific 3-type decomposition is a design recommendation (explicitly logged as ASSUMED/discretion, not a verified external fact)
- Architecture (publish properties, pitfalls): HIGH — every claim traced to official Microsoft Learn docs fetched directly this session, plus direct reads of the actual project files
- Pitfalls: HIGH — the `.pubxml`/`RuntimeIdentifier` gap and the `IncludeNativeLibrariesForSelfExtract` behavior are both drawn from official docs text quoted verbatim, not inferred

**Research date:** 2026-07-24
**Valid until:** ~90 days (stable SDK-level publish mechanics unlikely to change; re-verify if .NET 11/C# 15 GA lands and the team considers migrating, since that would newly make native discriminated unions available)
