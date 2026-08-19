---
phase: 24-self-contained-exe-size-reduction
reviewed: 2026-08-19T00:00:00Z
depth: standard
files_reviewed: 1
files_reviewed_list:
  - src/RigToggle.App/RigToggle.App.csproj
findings:
  critical: 0
  warning: 1
  info: 1
  total: 2
status: issues_found
---

# Phase 24: Code Review Report

**Reviewed:** 2026-08-19
**Depth:** standard
**Files Reviewed:** 1
**Status:** issues_found

## Summary

Reviewed the scoped Phase 24 deliverable in isolation: the `RemoveUnusedDesignerAndVbAssemblies` MSBuild target added to `src/RigToggle.App/RigToggle.App.csproj` (commit `67f4bfd`), which deny-lists 7 named assemblies out of `@(ResolvedFileToPublish)` after `ComputeResolvedFilesToPublishList`.

Verification performed beyond reading the file:
- Confirmed via `grep -rn "ComponentModel.Design|Drawing.Design|VisualBasic"` across `src/` that there are zero source references to any of the removed assemblies' namespaces — the safety claim in the target's comment is accurate, not just asserted.
- Confirmed all 7 filenames in the deny-list are exact, typo-free matches against the real assembly set produced by the local build (`bin/Release/**/*.dll`) — no silently-ineffective entries due to misspelling.
- Read the installed .NET 10 SDK's `Microsoft.NET.Publish.targets` directly to confirm `AfterTargets="ComputeResolvedFilesToPublishList"` is the correct hook point: `_ComputeFilesToBundle` (which feeds `GenerateSingleFileBundle`) consumes `@(ResolvedFileToPublish)` later in the same `Publish` dependency chain, after this custom target runs, so the removed items are correctly excluded from both the single-file bundle and any non-single-file publish output. The mechanism is sound and matches Microsoft's own documented pattern for excluding files from single-file publish.
- Confirmed `PropertyGrid`/`UITypeEditor`/`IDesignerHost` are not used anywhere in `src/` (only base `System.ComponentModel` types like `IContainer`/`ISupportInitialize`, which live in a different assembly than the ones being removed), ruling out a runtime break from the removal.

No correctness or security defects found in the mechanism itself. Two maintainability observations below are worth addressing but neither blocks shipping this phase's deliverable.

## Warnings

### WR-01: Deny-list has no build-time guard against silent staleness

**File:** `src/RigToggle.App/RigToggle.App.csproj:58-70`
**Issue:** The `Condition` on the `<ResolvedFileToPublish Remove=.../>` item matches on exact literal filenames (`System.Windows.Forms.Design.dll`, `Microsoft.VisualBasic.Core.dll`, etc.). If a future .NET SDK bump (this project already tracks .NET 10, a fast-moving LTS with multiple SDK patch releases ahead of it) renames, splits, or merges any of these 7 assemblies, the `Condition` simply stops matching for that entry — MSBuild raises no warning or error when a `Remove` condition matches zero items. The publish output silently regresses back toward its pre-Phase-24 size with no signal to the developer, and the regression would only be caught by someone manually re-measuring `RigToggle.App.exe`'s byte size after an SDK upgrade (exactly the kind of manual step Phase 24's own verification notes show is easy to forget between sessions).
**Fix:** Add a lightweight post-condition check that fails the build (or emits an explicit `<Warning>`) if the expected assemblies were not found for removal, so an SDK-driven rename surfaces immediately instead of silently:
```xml
<Target Name="RemoveUnusedDesignerAndVbAssemblies" AfterTargets="ComputeResolvedFilesToPublishList">
  <ItemGroup>
    <_DesignerAndVbAssembliesToRemove Include="@(ResolvedFileToPublish)" Condition="
      '%(FileName)%(Extension)'=='System.Windows.Forms.Design.dll' or
      '%(FileName)%(Extension)'=='System.Windows.Forms.Design.Editors.dll' or
      '%(FileName)%(Extension)'=='System.Design.dll' or
      '%(FileName)%(Extension)'=='System.Drawing.Design.dll' or
      '%(FileName)%(Extension)'=='Microsoft.VisualBasic.Core.dll' or
      '%(FileName)%(Extension)'=='Microsoft.VisualBasic.Forms.dll' or
      '%(FileName)%(Extension)'=='Microsoft.VisualBasic.dll'
    " />
    <ResolvedFileToPublish Remove="@(_DesignerAndVbAssembliesToRemove)" />
  </ItemGroup>
  <Warning Text="RemoveUnusedDesignerAndVbAssemblies expected to remove 7 known assemblies but only matched %(_DesignerAndVbAssembliesToRemove.Count) — the SDK's publish output may have changed; re-verify the deny-list."
           Condition="'@(_DesignerAndVbAssembliesToRemove->Count())' != '7'" />
</Target>
```

## Info

### IN-01: Deny-list matches by filename only, not full path

**File:** `src/RigToggle.App/RigToggle.App.csproj:60-68`
**Issue:** The condition matches on `%(FileName)%(Extension)` alone, without also checking directory/identity. For the current, known set of 7 BCL-shipped assembly names this is safe (verified against the actual publish output), but it is a slightly broader match than necessary — any future `ResolvedFileToPublish` item that happens to share one of these 7 exact filenames (e.g. from a different NuGet package's runtimes/ subfolder, or a ref-assembly copy) would also be silently dropped without anyone noticing, since the condition can't distinguish "the BCL's `System.Design.dll`" from "some other package's file that happens to be named `System.Design.dll`".
**Fix:** Low priority given the current known-safe file set; if this list grows, consider matching on `%(Identity)` (full resolved path) or `%(NuGetPackageId)` metadata instead of bare filename for stronger precision.

---

_Reviewed: 2026-08-19_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
