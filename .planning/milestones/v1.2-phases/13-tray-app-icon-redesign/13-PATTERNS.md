# Phase 13: Tray & App Icon Redesign - Pattern Map

**Mapped:** 2026-08-03
**Files analyzed:** 6 (1 new project/csproj, 2 new class files in it, 1 new/replaced `.csproj` edit, 3 replaced/new `.ico` assets, 1 modified `.cs`)
**Analogs found:** 4 / 6 (2 files — the binary geometry/ICO-writer logic — have no in-repo analog; RESEARCH.md's own code examples are the pattern source for those, flagged below)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.IconGen/RigToggle.IconGen.csproj` | config (new console project) | file-I/O (write) | `src/RigToggle.App/RigToggle.App.csproj` | role-match (WinExe + UseWindowsForms shape; App also has the `<ApplicationIcon>` property Pitfall to compare against) |
| `src/RigToggle.IconGen/Program.cs` | utility (dev-time console entry point) | batch (generate N files, run once, exit) | `src/RigToggle.App/Program.cs` | partial-match (same "composition root / `Main` entry point, construct-then-run" shape, but App's `Main` runs a message loop — IconGen's just calls generator methods and returns) |
| `src/RigToggle.IconGen/IconGeometry.cs` | utility (pure drawing functions) | transform (fractions → `Bitmap`) | none in-repo | no analog — see "No Analog Found" |
| `src/RigToggle.IconGen/IconWriter.cs` | utility (binary file writer) | file-I/O (write) | `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` | partial-match (file-write-to-disk shape/atomicity awareness only — JSON vs. hand-rolled binary format is a different problem entirely; RESEARCH.md Pattern 3 is the real source of truth here) |
| `src/RigToggle.App/RigToggle.App.csproj` (edit: add `<ApplicationIcon>`, keep `EmbeddedResource` block) | config | request-response (MSBuild property read at compile time) | itself (existing file, in-place edit) | exact — extend the file already read below |
| `src/RigToggle.App/MainForm.cs` (edit: `LoadTrayIconsIfNeeded()`, ~lines 231-249) | controller (WinForms code-behind) | event-driven (icon loaded once, assigned to `NotifyIcon.Icon` on mode-change events) | itself (existing file, in-place edit) | exact — same method, one-line fix per call site |

## Pattern Assignments

### `src/RigToggle.IconGen/RigToggle.IconGen.csproj` (config, new console project)

**Analog:** `src/RigToggle.App/RigToggle.App.csproj` (WinExe shape) cross-checked against `src/RigToggle.Windows/RigToggle.Windows.csproj` (library shape, no `RuntimeIdentifier`/no `OutputType`) and `src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj` (a project that references `RigToggle.Windows` but is deliberately its own `.csproj`, never added to `RigToggle.Tests`, so the main test suite stays cross-platform-buildable — the same isolation principle applies here: `RigToggle.IconGen` must never become a `ProjectReference` of `RigToggle.App`, or it would be dragged into the self-contained publish).

**Full current `RigToggle.App.csproj`** (`src/RigToggle.App/RigToggle.App.csproj:1-35`):
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\RigToggle.Core\RigToggle.Core.csproj" />
    <ProjectReference Include="..\RigToggle.Windows\RigToggle.Windows.csproj" />
  </ItemGroup>

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

**What to copy / what to change for `RigToggle.IconGen.csproj`:**
- Copy the `<Project Sdk="Microsoft.NET.Sdk">` shape, `<TargetFramework>net10.0-windows</TargetFramework>`, `<UseWindowsForms>true</UseWindowsForms>` (RESEARCH.md Standard Stack: this is how System.Drawing/GDI+ types are obtained with zero new PackageReferences — same mechanism `RigToggle.App.csproj` already uses), `<ImplicitUsings>enable</ImplicitUsings>`, `<Nullable>enable</Nullable>`.
- `<OutputType>Exe</OutputType>` — NOT `WinExe` (this is a console tool with no window; RESEARCH.md's Recommended Project Structure explicitly says `OutputType=Exe`).
- Do NOT set `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` unless the executor wants a self-contained publish of the tool itself — RESEARCH.md treats this as a `dotnet run`-from-source dev tool, not a distributed artifact, so this property is likely unnecessary (omit unless the plan decides otherwise).
- NO `<ItemGroup><ProjectReference ...>` block at all — this project references nothing (RESEARCH.md Recommended Project Structure: "never referenced as a ProjectReference from RigToggle.App — keeps it out of the self-contained publish entirely"). This is the one structural point where the analog diverges: `RigToggle.App.csproj` has two `ProjectReference`s; `RigToggle.IconGen.csproj` should have zero.
- NO `EmbeddedResource`/`LogicalName` block — `RigToggle.IconGen` writes `.ico` files to disk via relative path (`../RigToggle.App/Resources/*.ico`), it does not embed them into itself.
- Must also be added to `RigToggle.sln` (see `RigToggle.sln:6-15` for the `Project(...)...EndProject` block shape per existing project, plus the matching `GlobalSection(ProjectConfigurationPlatforms)` GUID entries — follow the exact same two-part pattern used for the other 5 projects).

---

### `src/RigToggle.IconGen/Program.cs` (utility, dev-time console entry point)

**Analog:** `src/RigToggle.App/Program.cs` (composition-root/`Main` shape only — the actual generation logic has no analog).

**Entry-point shape to copy** (`src/RigToggle.App/Program.cs:34-36`):
```csharp
[STAThread]
static void Main(string[] args)
{
```
Keep `[STAThread]` since `System.Drawing`/GDI+ `Graphics` objects are being created (same reason `RigToggle.App`'s WinForms `Main` needs it) — no message loop or `Application.Run` call is needed here, `Main` should just call the geometry+writer functions for all three icons and return.

**What NOT to copy from `Program.cs`:** the entire composition-root DI wiring (settings store, snapshot store, controllers, `ApplicationConfiguration.Initialize()`, `Application.Run(...)`) — none of that applies; `RigToggle.IconGen`'s `Main` is a flat sequence: draw N bitmaps per icon → call `IconWriter.WriteIco(...)` → write to `../RigToggle.App/Resources/{normal,rig,app}.ico`.

---

### `src/RigToggle.IconGen/IconGeometry.cs` (utility, pure drawing)

**No in-repo analog** — no existing file in this codebase does GDI+ shape drawing. Use RESEARCH.md's Pattern 1 (`src/RigToggle.IconGen` architecture section, "Redraw-per-size GDI+ geometry") and Pattern 2 ("Contiguous-silhouette outline via `GraphicsPath.AddPath`/union") as the concrete source — both are fully-formed code examples in `13-RESEARCH.md` lines 175-221, cross-referenced against the exact fractional coordinates locked in `13-UI-SPEC.md`'s "Icon Geometry Contract" section (lines 65-107, `normal.ico`/`rig.ico`/`app.ico` tables). Do not deviate from the UI-SPEC's fraction values — they are locked, not discretionary.

**Rendering-quality settings required** (RESEARCH.md Pattern 1, `13-RESEARCH.md:183-184`):
```csharp
g.SmoothingMode = SmoothingMode.AntiAlias;
g.PixelOffsetMode = PixelOffsetMode.HighQuality;
g.Clear(Color.Transparent);
```
Use `PixelFormat.Format32bppArgb` for every `Bitmap` (RESEARCH.md Anti-Patterns section, `13-RESEARCH.md:359` — paletted/8bpp formats cannot represent the required per-pixel alpha transparency).

---

### `src/RigToggle.IconGen/IconWriter.cs` (utility, binary file writer)

**Closest in-repo analog (partial, file-I/O shape only):** `src/RigToggle.Core/Persistence/JsonSettingsStore.cs`.

**Atomic-write pattern worth copying (`JsonSettingsStore.cs:76-87`):**
```csharp
public void Save(AppSettings settings)
{
    var directory = Path.GetDirectoryName(_path);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var tempPath = _path + ".tmp";
    File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, Options));
    File.Move(tempPath, _path, overwrite: true);
}
```
Apply the same `.tmp` + `File.Move(overwrite: true)` shape when `IconWriter`/`Program.cs` writes the final `.ico` bytes to `Resources/*.ico`, so a failed/interrupted dev-time run never leaves a corrupted `.ico` checked into git. This is a shape-only borrow — the actual byte-format logic has no in-repo precedent.

**Real pattern source (binary ICO packing):** RESEARCH.md Pattern 3, `13-RESEARCH.md:237-329` — full `WriteIco(Stream, IReadOnlyList<Bitmap>)` and `EncodeBmpInIco(Bitmap)` implementations (ICONDIR/ICONDIRENTRY header, bottom-up BGRA rows, 4-byte-padded AND mask). Copy this code directly; it is already a complete, ready-to-use implementation, cross-referenced against 3 independent sources in RESEARCH.md's Sources section. Pair with RESEARCH.md Pitfall 3 (`13-RESEARCH.md:388-393`, the `bWidth`/`bHeight` byte-overflow-at-256px handling, already present in the example as `(byte)(bmp.Width >= 256 ? 0 : bmp.Width)`) and Pitfall 4 (`13-RESEARCH.md:395-400`, bottom-up row order + mask padding, already present in the example) — do not simplify either away.

**Verification requirement (RESEARCH.md Assumption A4, `13-RESEARCH.md:422`):** after writing each `.ico`, round-trip it through `new Icon(path, size)` for every embedded size and assert it loads without throwing and reports the expected `Size` — do not rely on visual inspection alone before the human-verification rig-checkpoint step.

---

### `src/RigToggle.App/RigToggle.App.csproj` (config, in-place edit)

**Current full file already shown above.** Two edits:

1. Add `<ApplicationIcon>Resources\app.ico</ApplicationIcon>` inside the existing top `<PropertyGroup>` (RESEARCH.md Pattern 4, `13-RESEARCH.md:343-351` — confirmed via direct read that this property does not currently exist in the file). No separate `<ItemGroup>`/`Content`/`None` entry needed for `app.ico` — the property alone is sufficient (RESEARCH.md, same section, `13-RESEARCH.md:353`).
2. Keep the existing `EmbeddedResource`/`LogicalName` `<ItemGroup>` block for `normal.ico`/`rig.ico` completely unchanged (`13-RESEARCH.md`'s Anti-Patterns section explicitly warns against loading `app.ico` the same way — it is a separate embedding mechanism, native Win32 resource vs. managed embedded resource).

Resulting `<PropertyGroup>` shape to produce:
```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <ApplicationIcon>Resources\app.ico</ApplicationIcon>
</PropertyGroup>
```

---

### `src/RigToggle.App/MainForm.cs` (controller, in-place edit ~lines 231-249)

**Current code (exact, `src/RigToggle.App/MainForm.cs:225-249`):**
```csharp
/// <summary>
/// 08-RESEARCH.md Pitfall 3: loads the two pre-made embedded .ico resources
/// once and keeps the resulting Icon instances for the lifetime of the form —
/// never re-derive an Icon from a Bitmap per toggle (Icon.FromHandle leaks the
/// underlying GDI handle since the wrapper does not own it).
/// </summary>
private void LoadTrayIconsIfNeeded()
{
    if (_normalIcon is not null && _rigIcon is not null)
    {
        return;
    }

    var assembly = typeof(MainForm).Assembly;
    using var normalStream = assembly.GetManifestResourceStream("normal.ico");
    using var rigStream = assembly.GetManifestResourceStream("rig.ico");
    // IN-01 (code review): explicit null-check with a descriptive message
    // instead of the null-forgiving operator — a missing/renamed embedded
    // resource should fail with a clear diagnostic, not an opaque exception
    // from the Icon constructor at startup, before any window exists.
    _normalIcon = new System.Drawing.Icon(normalStream
        ?? throw new InvalidOperationException("Embedded resource 'normal.ico' not found."));
    _rigIcon = new System.Drawing.Icon(rigStream
        ?? throw new InvalidOperationException("Embedded resource 'rig.ico' not found."));
}
```

**Required fix (RESEARCH.md Pitfall 1, `13-RESEARCH.md:373-379`):** the two size-less `new System.Drawing.Icon(stream)` calls always return the *smallest* frame in a multi-size `.ico` and never re-select per current DPI — `NotifyIcon` performs no corrective re-selection of its own (confirmed against `dotnet/winforms` issue #6955). Change both constructor calls to the sized overload:
```csharp
_normalIcon = new System.Drawing.Icon(normalStream
    ?? throw new InvalidOperationException("Embedded resource 'normal.ico' not found."),
    SystemInformation.SmallIconSize);
_rigIcon = new System.Drawing.Icon(rigStream
    ?? throw new InvalidOperationException("Embedded resource 'rig.ico' not found."),
    SystemInformation.SmallIconSize);
```
Keep everything else in the method identical — the null-check/`InvalidOperationException` pattern (IN-01 code-review convention already established) must be preserved unchanged; only the constructor call itself gains the `SystemInformation.SmallIconSize` argument. Do NOT touch `app.ico`/`<ApplicationIcon>` anywhere in this file — that embedding path is unaffected by this defect and has no runtime code referencing it at all (RESEARCH.md Anti-Patterns, `13-RESEARCH.md:357`).

---

## Shared Patterns

### Embedded-resource vs. native-resource dual mechanism
**Source:** `src/RigToggle.App/RigToggle.App.csproj` (existing `EmbeddedResource`/`LogicalName` block) + RESEARCH.md Pattern 4
**Apply to:** `RigToggle.App.csproj` only
Two icons (`normal.ico`, `rig.ico`) stay on the existing `EmbeddedResource`+`LogicalName`+`GetManifestResourceStream` runtime-read path (unchanged filenames/LogicalNames per UI-SPEC Scope Notes). One icon (`app.ico`) is new and uses a structurally different mechanism (`<ApplicationIcon>`, compiled into the Win32 resource table, read by the OS shell — never by app code). Do not conflate the two mechanisms or write runtime code that reads `app.ico`.

### Null-check-with-descriptive-exception convention (IN-01)
**Source:** `src/RigToggle.App/MainForm.cs:241-248`
**Apply to:** Any new code that loads an embedded/generated resource and could fail on a missing file (e.g., if `IconWriter`-produced `.ico` files are ever read back at runtime for verification)
```csharp
_normalIcon = new System.Drawing.Icon(normalStream
    ?? throw new InvalidOperationException("Embedded resource 'normal.ico' not found."));
```
Explicit null-check + descriptive message, not the null-forgiving operator (`!`) — a missing/renamed resource must fail with a clear diagnostic, not an opaque `NullReferenceException`.

### Atomic file write (`.tmp` + `File.Move(overwrite: true)`)
**Source:** `src/RigToggle.Core/Persistence/JsonSettingsStore.cs:84-86`
**Apply to:** `IconWriter`/`Program.cs`'s final write of generated `.ico` bytes to `Resources/*.ico`
```csharp
var tempPath = _path + ".tmp";
File.WriteAllText(tempPath, ...);
File.Move(tempPath, _path, overwrite: true);
```
Adapt to binary (`File.WriteAllBytes`/`Stream` equivalent) rather than `WriteAllText`, but preserve the temp-then-atomic-move shape so a crashed/interrupted `IconGen` run never leaves a half-written `.ico` checked into git.

### Project isolation from the self-contained publish
**Source:** `src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj` (kept as its own `.csproj`, never merged into `RigToggle.Tests`, specifically so the cross-platform test suite doesn't inherit a Windows-only dependency) + RESEARCH.md Recommended Project Structure
**Apply to:** `RigToggle.IconGen.csproj`
`RigToggle.IconGen` must never appear as a `<ProjectReference>` anywhere in `RigToggle.App.csproj` — same isolation principle the test suite already uses for a different reason (there: keep `RigToggle.Tests` cross-platform-buildable; here: keep the dev-time-only generator out of the self-contained publish entirely).

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/RigToggle.IconGen/IconGeometry.cs` | utility | transform | No GDI+/`System.Drawing` drawing code exists anywhere in this repo today — use `13-RESEARCH.md` Pattern 1/Pattern 2 (full code examples, `13-RESEARCH.md:175-221`) as the primary source, driven by `13-UI-SPEC.md`'s locked "Icon Geometry Contract" fractional coordinates (`13-UI-SPEC.md:65-107`) |
| `src/RigToggle.IconGen/IconWriter.cs` | utility | file-I/O (binary write) | No binary file-format writer exists anywhere in this repo (all existing persistence is JSON via `System.Text.Json`, see `JsonSettingsStore`/`JsonSnapshotStore`) — use `13-RESEARCH.md` Pattern 3 (full `WriteIco`/`EncodeBmpInIco` implementation, `13-RESEARCH.md:237-329`) as the primary source; `JsonSettingsStore.cs` is cited above only for the atomic-write shape, not the byte-format logic |

## Metadata

**Analog search scope:** `src/RigToggle.App/`, `src/RigToggle.Windows/`, `src/RigToggle.Core/`, `src/RigToggle.Tests/`, `src/RigToggle.Windows.Tests/`, `RigToggle.sln` (all 5 existing `.csproj` files + all `.cs` files under those projects — see full file listing gathered via `find src -name "*.csproj" -o -name "*.cs"`)
**Files scanned:** 5 `.csproj` files, `Program.cs`, `MainForm.cs` (icon-loading section + class header), `JsonSettingsStore.cs`, `RigToggle.sln`
**Pattern extraction date:** 2026-08-03
