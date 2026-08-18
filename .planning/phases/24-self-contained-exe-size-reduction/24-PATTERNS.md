# Phase 24: Self-Contained Exe Size Reduction - Pattern Map

**Mapped:** 2026-08-18
**Files analyzed:** 1 (single-file edit; no new files created)
**Analogs found:** 1 / 1 (the analog is the file itself — this is a self-consistent, in-place property/target addition, not a new-role file)

## Scope Note

This phase is unusual for pattern-mapping purposes: RESEARCH.md and CONTEXT.md agree there is exactly **one file to modify** (`src/RigToggle.App/RigToggle.App.csproj`), **zero new files**, and **zero application/runtime code changes**. The "pattern to copy" is not cross-file (controller → controller, service → service) — it is intra-file: follow the exact commenting/structuring convention already used by the four existing MSBuild levers in this same file when adding the fifth (new) lever, the `RemoveUnusedDesignerAndVbAssemblies` `<Target>`.

There is no controller/component/service/model role here — this is 100% build/packaging configuration (MSBuild `.csproj` XML), which the role taxonomy does not cleanly cover. Classified below as `config` / `build-transform` for completeness.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.App/RigToggle.App.csproj` | config (MSBuild project file) | build/transform (publish-pipeline file-list mutation, not runtime data flow) | itself (existing `<PropertyGroup>` levers in the same file) | exact — the analog IS the file being edited; no other file in the repo defines an MSBuild `<Target>` |

No other files are touched. `RigToggle.Windows.csproj` and `win-x64.pubxml` were read/audited by RESEARCH.md but confirmed to need **no changes** (D-01's package-swap audit found nothing further; the only viable lever is the new `<Target>` in `RigToggle.App.csproj`).

## Pattern Assignments

### `src/RigToggle.App/RigToggle.App.csproj` (config, build/transform)

**Analog:** the file's own existing `<PropertyGroup>` block (lines 3-30) — this project has a strong, consistent convention: every non-obvious MSBuild lever gets an inline XML comment immediately above it, explaining *why* it's set, citing the originating research doc, and (where relevant) a specific pitfall it guards against.

**Full current file content (all 50 lines already read, no further reads needed):**
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <!-- RuntimeIdentifier lives here (not only in the .pubxml) because `dotnet publish` CLI
         does not honor RuntimeIdentifier set only inside a .pubxml file — see 05-RESEARCH.md
         Pitfall 1. This project only ever targets win-x64 (D-09), so this is unconditioned. -->
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- Intentionally no elevation manifest element of any kind: this keeps the tool asInvoker
         (02-RESEARCH.md Pitfall 6) — do not add an elevated execution level or admin requirement. -->
    <!-- Embeds app.ico as the compiled exe's native Win32 icon resource (RT_GROUP_ICON/RT_ICON),
         read directly by Explorer/Alt-Tab/taskbar shell APIs — a separate mechanism from the
         EmbeddedResource+LogicalName tray icons below (13-RESEARCH.md Pattern 4). -->
    <ApplicationIcon>Resources\app.ico</ApplicationIcon>
    <!-- Excludes localized satellite resource assemblies (WindowsDisplayAPI, NAudio.Wasapi,
         and the BCL) from the self-contained publish output. Must be `en`, not `en-US` — `en-US`
         matches only the en-US-specific satellites and would leave generic `en` satellites in
         place (18-RESEARCH.md Pitfall 2). -->
    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
    <!-- Disables ICU culture-data loading. Confirmed safe: no user-facing culture-dependent
         date/number formatting exists anywhere in src/ — the only DateTime.Now/ToString
         ("HH:mm:ss.fff") uses are debug trace lines, MonitorIdentifyOverlay already formats its
         number labels with CultureInfo.InvariantCulture explicitly, and the solution contains
         zero .resx files (18-RESEARCH.md Pitfall 3). -->
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\RigToggle.Core\RigToggle.Core.csproj" />
    <ProjectReference Include="..\RigToggle.Windows\RigToggle.Windows.csproj" />
  </ItemGroup>

  <!-- Tray glyphs (TRAY-04/D-01): embedded (not Content/CopyToOutput) so both icons live
       inside the single-file publish, addressable at runtime via
       Assembly.GetManifestResourceStream using the deterministic LogicalName below
       rather than the namespace-mangled default resource name. -->
  <ItemGroup>
    <EmbeddedResource Include="Resources\normal.ico">
      <LogicalName>normal.ico</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="Resources\rig.ico">
      <LogicalName>rig.ico</LogicalName>
    </EmbeddedResource>
  </ItemGroup>

</Project>
```

**Comment-convention pattern to replicate for the new `<Target>`** (extracted from the four existing `<!-- ... -->` blocks above, lines 7-9, 13-14, 15-17, 19-22, 24-28):
- One comment block immediately above the element it documents (not a trailing comment, not a separate doc file).
- States *what* the setting does in the first sentence.
- States *why it's safe/why it was chosen*, citing the specific evidence (e.g., "no user-facing culture-dependent formatting exists anywhere in src/").
- Cites the originating research doc and, where applicable, a specific numbered Pitfall (e.g., `18-RESEARCH.md Pitfall 2`).

**Exact new `<Target>` to add (verified working, exact byte-count effect measured — from 24-RESEARCH.md Code Examples, reproduced here as the literal text to insert):**
```xml
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

**Placement:** Inside the existing `<Project>` element, after the current `<ItemGroup>` blocks (tray icons, lines 41-48), immediately before `</Project>` (line 50) — per RESEARCH.md's Recommended Project Structure section. This is a pure XML-sibling insertion; no existing element is modified.

**Error handling / validation pattern:** N/A — MSBuild `<Target>`/`<ItemGroup Remove>` XML has no runtime try/catch equivalent. The "validation" for this pattern class is external: (1) exhaustive `grep -rn` confirming zero source usage of the excluded assemblies' namespaces (already done in RESEARCH.md, reusable verbatim, not re-derivable from this file), and (2) a `dotnet publish` dry run to confirm no build warnings, and (3) rig-hardware functional verification (toggle round trip + cold autostart boot) as the final safety gate — same verification shape as `18-VERIFICATION.md`.

**Testing pattern:** No unit-test file is implicated — this is build-output verification, not application-code testing. The established test pattern for this class of change (per CONTEXT.md's canonical refs) is: run the exact measurement commands from `18-RESEARCH.md`/`24-RESEARCH.md` (reproduced below) before and after the edit, diff byte counts, then perform the rig-hardware round trip.

**Measurement commands to reuse verbatim (from 24-RESEARCH.md, already verified working this session):**
```bash
export PATH="$HOME/.dotnet:$PATH"
rm -rf src/RigToggle.App/bin/publish/win-x64
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release \
  -p:PublishProfile=win-x64 -p:EnableWindowsTargeting=true
ls -la src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
```

## Shared Patterns

### Inline-comment-per-lever convention
**Source:** `src/RigToggle.App/RigToggle.App.csproj` lines 7-29 (all four existing levers)
**Apply to:** The single new `<Target>` element being added — must carry the same comment style (what/why/citation), not a bare uncommented XML block.

### "Confirm dead before delete" discipline
**Source:** `.planning/milestones/v2.0-phases/18-cleanup-pass-exe-size-reduction/18-RESEARCH.md` Pattern 1, reused explicitly by `24-RESEARCH.md` Pattern 2
**Apply to:** Any exclusion-by-name lever — exhaustive `grep -rn` across `src/` for every namespace/type the assembly exposes must precede any exclusion; already performed in RESEARCH.md for the 7 target files, reusable verbatim by the planner (no need to re-grep).

### Rig-hardware verification as final gate
**Source:** `.planning/milestones/v2.0-phases/18-cleanup-pass-exe-size-reduction/18-VERIFICATION.md`
**Apply to:** The one plan this phase will produce — cold autostart boot + full toggle round trip on real Windows rig hardware is mandatory before closing the phase, per both CONTEXT.md's `## Established Patterns` and RESEARCH.md's A2 risk note (grep-based confirmation alone is not sufficient; reflection-based third-party dependencies inside `System.Windows.Forms.dll` itself cannot be ruled out by static grep).

## No Analog Found

None. The one file in scope already exists and contains its own convention to follow (self-referential analog). No files require an analog from elsewhere in the codebase.

## Metadata

**Analog search scope:** `src/RigToggle.App/RigToggle.App.csproj` (target file itself, full read), `src/RigToggle.Windows/RigToggle.Windows.csproj` and `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` (referenced in CONTEXT.md canonical refs, confirmed via RESEARCH.md to need no changes — not independently re-read since RESEARCH.md already quoted their relevant state).
**Files scanned:** 1 (full read) + 2 (state confirmed via RESEARCH.md's direct quotes, no independent read needed — avoids redundant re-reads of files RESEARCH.md already fully covered).
**Pattern extraction date:** 2026-08-18
