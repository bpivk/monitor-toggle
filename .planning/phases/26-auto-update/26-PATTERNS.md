# Phase 26: Auto-Update - Pattern Map

**Mapped:** 2026-08-22
**Files analyzed:** 15
**Analogs found:** 15 / 15

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|-----------------|---------------|
| `src/RigToggle.Core/Abstractions/IReleaseFeed.cs` | interface (service) | request-response | `src/RigToggle.Core/Abstractions/IAppController.cs` | role-match |
| `src/RigToggle.Core/GitHubReleaseFeed.cs` | service (HTTP client) | request-response | `src/RigToggle.Windows/WindowsAppController.cs` (logging/error idiom) + none for HttpClient itself | no-analog (new capability) |
| `src/RigToggle.Core/Models/ReleaseInfo.cs` | model | CRUD (plain record) | `src/RigToggle.Core/Models/ToggleInProgressMarker.cs` | exact |
| `src/RigToggle.Core/UpdateVersionComparer.cs` | utility (pure logic) | transform | `src/RigToggle.Core/HotkeyFormatter.cs` (pure static utility) | role-match |
| `src/RigToggle.Core/UpdateOrchestrator.cs` | service (orchestrator) | request-response | `src/RigToggle.Core/ToggleOrchestrator.cs` | exact |
| `src/RigToggle.Core/Abstractions/IUpdateApplier.cs` | interface | request-response | `src/RigToggle.Core/Abstractions/IAppController.cs` | exact |
| `src/RigToggle.Windows/WindowsUpdateApplier.cs` | service (Windows adapter) | file-I/O + event-driven | `src/RigToggle.Windows/WindowsAppController.cs` (`LaunchOrFocus`) | exact |
| `src/RigToggle.App/UpdateApplyEntryPoint.cs` (MODIFIED — Phase 25 placeholder → real body) | entry-point / controller | file-I/O | itself (Phase 25 placeholder) + `src/RigToggle.App/StartupRecoveryChecker.cs` (marker/recovery idiom) | exact |
| `src/RigToggle.App/Program.cs` (MODIFIED) | composition root | request-response | itself (existing file, additive change) | exact |
| `src/RigToggle.App/UpdatePromptDialog.cs` (+ `.Designer.cs`) | component (Form) | request-response | `src/RigToggle.App/MonitorConfirmDialog.cs` + `.Designer.cs` | exact |
| `src/RigToggle.App/ThemeApplier.cs` (MODIFIED — add `ThemeRichTextBox`) | utility (theming) | transform | itself (`ThemeComboBox`/`ThemeMonitorGrid` methods) | exact |
| `src/RigToggle.App/MainForm.Designer.cs` / `.cs` (MODIFIED — tray menu item) | controller (UI wiring) | event-driven | itself (`traySettingsMenuItem` construction + `TraySettingsMenuItem_Click`) | exact |
| `src/RigToggle.App/SettingsForm.Designer.cs` / `.cs` (MODIFIED — new button) | controller (UI wiring) | event-driven | itself (`btnSaveSettings`/`btnDiscardChanges` construction) | exact |
| `src/RigToggle.Core/Models/UpdateAppliedMarker.cs` (or similar, D-09 marker) | model | CRUD (plain record) | `src/RigToggle.Core/Models/ToggleInProgressMarker.cs` | exact |
| `src/RigToggle.Core/Persistence/JsonUpdateAppliedMarkerStore.cs` (or similar) | service (persistence) | file-I/O | `src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs` | exact |
| `src/RigToggle.Core/Abstractions/IAutostartConfigurator.cs`-style additions — N/A, no new interface needed here | — | — | — | — |
| `src/RigToggle.App/RigToggle.App.csproj` (MODIFIED — add `<Version>`) | config | — | itself (existing file) | exact |
| `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` (MODIFIED) | config | — | itself | exact |
| `.github/workflows/release.yml` (MODIFIED — `-p:Version=`, `.sha256` step) | config (CI) | batch | itself | exact |
| `src/RigToggle.Tests/UpdateVersionComparerTests.cs` | test | transform | `src/RigToggle.Tests/HotkeyFormatterTests.cs` | exact |
| `src/RigToggle.Tests/UpdateOrchestratorTests.cs` | test | request-response | `src/RigToggle.Tests/ToggleOrchestratorTests.cs` | exact |

## Pattern Assignments

### `src/RigToggle.Core/UpdateOrchestrator.cs` (service/orchestrator, request-response)

**Analog:** `src/RigToggle.Core/ToggleOrchestrator.cs`

**Constructor/DI pattern** (lines 31-46):
```csharp
public sealed class ToggleOrchestrator
{
    private readonly ToggleService _toggleService;
    private readonly Abstractions.IToggleInProgressStore _markerStore;

    public ToggleOrchestrator(ToggleService toggleService, Abstractions.IToggleInProgressStore markerStore)
    {
        _toggleService = toggleService ?? throw new ArgumentNullException(nameof(toggleService));
        _markerStore = markerStore ?? throw new ArgumentNullException(nameof(markerStore));
    }
```
`UpdateOrchestrator` should take `IReleaseFeed`, `IUpdateApplier`, and whatever marker/settings store it needs (skip-version, update-applied-not-yet-confirmed) via constructor injection the same way — never `new` a concrete adapter internally (composition-root-only construction, established codebase-wide rule).

**Marker save/clear-in-finally pattern** (lines 121-172) — directly informs D-09's "update applied, not yet confirmed" marker lifecycle:
```csharp
private ToggleResult RunGuarded(ToggleMode targetMode, Func<ToggleResult> pipeline)
{
    if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
    {
        throw new ToggleInProgressException(...);
    }

    try
    {
        _markerStore.Save(new ToggleInProgressMarker(targetMode, DateTimeOffset.UtcNow));
        return pipeline();
    }
    finally
    {
        try { _markerStore.Clear(); }
        catch { /* best-effort marker cleanup */ }

        Volatile.Write(ref _busy, 0);
    }
}
```
Copy this "save marker before risky operation → clear on confirmed success → best-effort catch around the clear" shape for D-09's confirmed-healthy marker (save before swap/relaunch in `WindowsUpdateApplier`/`UpdateApplyEntryPoint`, clear only once the new exe reaches a confirmed-running state — NOT in a blind `finally`, since the whole point is that an immediate crash must leave the marker in place for next-launch detection).

**Core-sequences-App/Windows-executes split:** `ToggleOrchestrator`/`ToggleService` never reference `Form`/`MessageBox`/`NotifyIcon` — `UpdateOrchestrator` must be equally UI-free; `UpdatePromptDialog` and toast calls live in `RigToggle.App`, invoked by the composition root or a thin App-layer wrapper around `UpdateOrchestrator`, never inside Core.

---

### `src/RigToggle.Core/Models/ReleaseInfo.cs` and update-applied marker model

**Analog:** `src/RigToggle.Core/Models/ToggleInProgressMarker.cs` (full file, 17 lines)

```csharp
namespace RigToggle.Core.Models;

public sealed record ToggleInProgressMarker(ToggleMode TargetMode, DateTimeOffset StartedAtUtc);
```
Follow this exact shape: a plain immutable `sealed record` with primary-constructor properties, no behavior, XML doc explaining what disk-persisted state it represents and why it exists (crash/failure detection). `ReleaseInfo` should be `public sealed record ReleaseInfo(string TagName, string AssetDownloadUrl, string HtmlUrl, DateTimeOffset PublishedAt, bool Prerelease, string? Body)` (Body added for D-01's release-notes rendering — ARCHITECTURE.md's `ReleaseInfo` list omits `Body` but D-01 requires it). The "update applied, not yet confirmed" marker (D-09) should be a sibling record, e.g. `public sealed record UpdateAppliedMarker(string NewVersion, string PreviousVersion, DateTimeOffset AppliedAtUtc)`.

---

### `src/RigToggle.Core/Persistence/*Store.cs` for the D-09 marker

**Analog:** `src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs` (full file, 93 lines) — copy verbatim shape:

```csharp
public sealed class JsonToggleInProgressStore : IToggleInProgressStore
{
    private readonly string _path;
    public JsonToggleInProgressStore(string path) { _path = path; }
    private bool Exists() => File.Exists(_path);

    public void Save(ToggleInProgressMarker marker)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(marker));
        File.Move(tempPath, _path, overwrite: true);
    }

    public ToggleInProgressMarker? TryLoad()
    {
        if (!Exists()) return null;
        try { return JsonSerializer.Deserialize<ToggleInProgressMarker>(File.ReadAllText(_path)); }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public void Clear()
    {
        try { if (Exists()) File.Delete(_path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
```
Temp-file-then-atomic-`File.Move` write pattern, `TryLoad` degrading every plausible I/O failure to `null` rather than throwing, `Clear` as a best-effort no-op-if-absent delete — apply this identically for the D-09 update-applied marker store and, if `settings.json` isn't used for "skip this version" (D-02), for that marker too. `JsonSettingsStore.cs` (lines 76-87) shows the same temp+move pattern for `AppSettings.Save` if "skip this version" is added as a field there instead (simpler — see AppSettings analog below).

---

### `AppSettings.cs` — adding the "skip this version" field (D-02)

**Analog:** `src/RigToggle.Core/Models/AppSettings.cs` (full file)

Add a nullable field following the existing convention exactly — every field is nullable-or-bool-default with an explanatory doc-comment paragraph at the class level, not per-field:
```csharp
public string? MonitorDevicePath { get; set; }
...
public bool EnableDebugLogging { get; set; }
```
e.g. `public string? SkippedUpdateVersion { get; set; }` — a plain nullable string, loaded/saved through the existing `JsonSettingsStore` (no new store needed for this one, unlike the D-09 marker which needs its own file per the codebase's existing split between long-lived config (`AppSettings`) and short-lived crash-detection markers (`ToggleInProgressMarker`)).

---

### `src/RigToggle.Windows/WindowsUpdateApplier.cs` (service, file-I/O + event-driven)

**Analog:** `src/RigToggle.Windows/WindowsAppController.cs`

**Class-level doc-comment convention** (lines 1-38) — extensive rationale/citation-style comments explaining *why* an approach was chosen over alternatives, referencing debug-session history and CLAUDE.md's "What NOT to Use" — `WindowsUpdateApplier` should carry equally thorough doc comments citing ARCHITECTURE.md Pattern 2 and PITFALLS.md Pitfall 1/4/5/9.

**Relaunch-via-`Process.Start(UseShellExecute=true)` pattern** (lines 73-93):
```csharp
public void LaunchOrFocus(string companionAppPath)
{
    Log($"LaunchOrFocus: relaunch requested for '{companionAppPath}'.");

    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = companionAppPath,
        UseShellExecute = true,
    }) ?? throw new InvalidOperationException($"Failed to start '{companionAppPath}'.");
}
```
Reuse this exact `ProcessStartInfo { UseShellExecute = true }` shape for both (a) spawning the temp-copy helper with `--apply-update <args>` and (b) the helper relaunching the real exe path — this is the codebase's only existing rig-verified process-relaunch mechanism (chosen deliberately over window-handle tricks per Pitfall 4's analog note).

**Best-effort `Trace.WriteLine` logging convention** (lines 102-112), copy verbatim shape for every new Windows-layer type in this phase:
```csharp
private static void Log(string message)
{
    try
    {
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] WindowsAppController: {message}");
    }
    catch
    {
        // Logging is diagnostic-only; never let it affect toggle behavior.
    }
}
```
Replace `WindowsAppController` with the new type's own name (e.g. `WindowsUpdateApplier`) in the interpolated prefix.

**Disposal pattern for enumerated `Process` handles** — `WindowsAppController.IsRunning` (lines 41-71) wraps `Process.GetProcessesByName` results in `try/finally { foreach(...) p.Dispose(); }`; not directly needed for the updater's own process-start calls (`using var process = ...`), but apply the same `using`/dispose discipline to any `Process` object obtained.

---

### `src/RigToggle.App/UpdateApplyEntryPoint.cs` (MODIFIED — replace Phase 25 placeholder)

**Analog:** itself (Phase 25 placeholder, full file, 43 lines) — the exact contract to preserve:

```csharp
internal static class UpdateApplyEntryPoint
{
    internal static int Run(string[] applyUpdateArgs)
    {
        return StartupArgs.ApplyUpdateBypassExitCode;
    }
}
```
Keep the method signature (`internal static int Run(string[] applyUpdateArgs)`) and the class name/visibility exactly as-is (D-04, Phase 25 CONTEXT.md — one-way locked contract). Replace only the body with: wait-for-writable-poll on the original exe path (optionally fast-pathed via a PID passed in `applyUpdateArgs`), rename-to-`.bak`, move-staged-exe-into-place, checksum-verify-before-swap (D-11), best-effort `.bak` cleanup, relaunch via `WindowsUpdateApplier`'s `Process.Start(UseShellExecute=true)` pattern (preserving `--tray` if the pre-update session was hidden — forward that flag through `applyUpdateArgs`), then return a (possibly new) exit code — do not change `StartupArgs.ApplyUpdateBypassExitCode`'s existing meaning/value without also updating `src/RigToggle.Tests/StartupArgsTests.cs` and `src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs`, both of which assert on it today.

**Recovery/marker "persist before risky op" idiom** — cross-reference `src/RigToggle.App/StartupRecoveryChecker.cs` (full file, 70 lines) for the sibling "detect an uncleared marker at next launch, clear it first, then show a dialog/toast" shape:
```csharp
var marker = markerStore.TryLoad();
if (marker is not null)
{
    markerStore.Clear();  // clear FIRST, before surfacing
    ... show recovery UI ...
}
```
Apply this identically for D-09's auto-rollback check — but note this phase's check must run on the *next normal launch* (inside `Program.cs`'s ordinary startup path, not inside `UpdateApplyEntryPoint`, since the marker records "did the LAST update's exe fail to reach confirmed-healthy," which can only be known one launch later), likely as a new sibling static class alongside `StartupRecoveryChecker`, invoked at the same point in `Program.cs`.

---

### `src/RigToggle.App/Program.cs` (MODIFIED — real `UpdateOrchestrator.CheckOnLaunchAsync` wiring)

**Analog:** itself (existing file, 387 lines) — this phase's insertion points are already documented inline:

**Bypass-branch pattern already in place** (lines 108-121) — no change needed to this shape, only to what runs inside it (via `UpdateApplyEntryPoint.Run`'s new body):
```csharp
if (StartupArgs.TryGetApplyUpdateArgs(args, out var applyUpdateArgs))
{
    Environment.ExitCode = UpdateApplyEntryPoint.Run(applyUpdateArgs);
    return;
}
```

**Composition-root construction convention** (lines 260-315) — construct `GitHubReleaseFeed`, `WindowsUpdateApplier`, and `UpdateOrchestrator` here, alongside the other Windows adapters, following the exact same flat sequential-`var` style already used for `monitorController`/`audioController`/`appController`/`autostartConfigurator`:
```csharp
var monitorController = new WindowsMonitorController();
var audioController = new WindowsAudioController();
var appController = new WindowsAppController();
var autostartConfigurator = new WindowsAutostartConfigurator();
```

**New on-launch check trigger point** — per ARCHITECTURE.md's data-flow diagram, insert `mainForm.BeginInvoke(UpdateOrchestrator.CheckOnLaunchAsync)` after `mainForm.RegisterHotkeyAtStartup()` (line 328) and after `guard.MarkReady()` (line 339), before either `Application.Run` branch (lines 354-361) — verify `mainForm.Handle` already exists under `--tray` per the open verification question in ARCHITECTURE.md (fallback: a one-shot `System.Windows.Forms.Timer` if `BeginInvoke` proves unsafe pre-handle).

**Best-effort vs. deliberate-exception idiom** — this file's own doc comments (lines 197-208, 252-258) distinguish "best-effort, wrapped in try/catch, never blocks startup" (trace listener, hotkey registration) from "deliberate exception, unwrapped, must fail loudly" (`StartupRecoveryChecker.Run`, the single-instance guard). The on-launch update check belongs in the **best-effort** category per PITFALLS.md's UX Pitfalls table ("update check silently blocks/delays app startup" must never happen) — fire-and-forget via `BeginInvoke`, swallow all exceptions inside `UpdateOrchestrator.CheckOnLaunchAsync` itself, never surface a dialog/toast for a failed *automatic* check (D-07 explicitly contrasts this with the *manual* check, which must show a Warning toast on failure).

---

### `src/RigToggle.App/UpdatePromptDialog.cs` + `.Designer.cs` (component, request-response)

**Analog:** `src/RigToggle.App/MonitorConfirmDialog.cs` (full file, 99 lines) + `MonitorConfirmDialog.Designer.cs` (full file, 121 lines) — D-04 explicitly locks this as the structural precedent.

**Constructor pattern** (`.cs` lines 27-63):
```csharp
public MonitorConfirmDialog(IReadOnlyList<string> disableNames, IReadOnlyList<string> enableNames, IThemeProvider themeProvider)
{
    _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));
    InitializeComponent();

    ... populate control text from constructor args ...

    this.AcceptButton = btnContinue;
    this.CancelButton = btnCancel;

    _themeProvider.ThemeChanged += OnThemeChanged;
    this.FormClosed += (_, _) => _themeProvider.ThemeChanged -= OnThemeChanged;

    ThemeApplier.ApplyEffectiveColorMode(IsDark);
    DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
    ThemeApplier.ThemeButton(btnContinue, IsDark);
    ThemeApplier.ThemeButton(btnCancel, IsDark);
}
```
`UpdatePromptDialog`'s constructor takes `(ReleaseInfo releaseInfo, IThemeProvider themeProvider)`, does the identical `ArgumentNullException` guard, `InitializeComponent()`, sets `lblHeadline.Text`/populates `rtbReleaseNotes` from `releaseInfo`, wires `AcceptButton`/`CancelButton` (`btnUpdateNow`/`btnLater` per D-04/UI-SPEC), subscribes `ThemeChanged` with the same `FormClosed`-based unsubscribe, then calls `ApplyEffectiveColorMode`/`ApplyRoundedCornersAndMica`/`ThemeButton` for all three buttons — plus the new `ThemeApplier.ThemeRichTextBox(rtbReleaseNotes, IsDark)` call (see ThemeApplier section below).

**`IsDark` + live theme-flip handler pattern** (`.cs` lines 65-94):
```csharp
private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;

private void OnThemeChanged(object? sender, EventArgs e)
{
    if (InvokeRequired)
    {
        BeginInvoke(new Action(() => OnThemeChanged(sender, e)));
        return;
    }

    try
    {
        ThemeApplier.ApplyEffectiveColorMode(IsDark);
        DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
        ThemeApplier.ThemeButton(btnContinue, IsDark);
        ThemeApplier.ThemeButton(btnCancel, IsDark);
        Refresh();
    }
    catch
    {
        // Cosmetic-only -- a theming failure must never crash the confirm flow.
    }
}
```
Copy verbatim, extending the theme-button/re-theme list to all three buttons plus `rtbReleaseNotes`.

**Designer `Dispose(bool)` backstop-unsubscribe pattern** (`.Designer.cs` lines 14-34):
```csharp
protected override void Dispose(bool disposing)
{
    if (disposing && (components != null)) { components.Dispose(); }
    if (disposing) { _themeProvider.ThemeChanged -= OnThemeChanged; }
    base.Dispose(disposing);
}
```

**Designer control-construction/layout pattern** (`.Designer.cs` lines 42-121) — same declarative shape: field declarations at top of `InitializeComponent`, `SuspendLayout()`/`ResumeLayout(false)` bracketing, per-control `Location`/`Size`/`Name` assignment, `this.FlatStyle = System.Windows.Forms.FlatStyle.Flat` set declaratively on every button (re-asserted at runtime by `ThemeApplier.ThemeButton`), form-level properties set in a trailing block (`AutoScaleDimensions`, `AutoScaleMode`, `ClientSize`, `FormBorderStyle = FixedDialog`, `MaximizeBox/MinimizeBox = false`, `ShowInTaskbar = false`, `StartPosition = CenterParent`, `Text`, `Name`), then `Controls.Add(...)` calls in declaration order. `UpdatePromptDialog.Designer.cs` follows this exact skeleton with `ClientSize(440, 460)` per UI-SPEC, `lblHeadline`/`rtbReleaseNotes`/`btnSkip`/`btnLater`/`btnUpdateNow` as the five new fields.

---

### `src/RigToggle.App/ThemeApplier.cs` (MODIFIED — add `ThemeRichTextBox`)

**Analog:** itself — `ThemeComboBox` (lines 181-193) is the closest existing sibling (a control WinForms dark-mode doesn't reach, needing `FlatStyle`/`BackColor`/`ForeColor` override):
```csharp
public static void ThemeComboBox(ComboBox combo, bool dark)
{
    try
    {
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Window;
        combo.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.WindowText;
    }
    catch
    {
        // Cosmetic-only — leave the control unchanged on failure.
    }
}
```
Add `ThemeRichTextBox(RichTextBox rtb, bool dark)` using the identical `try/catch`-swallow, same literal color pair as `ThemeMonitorGrid`'s cell background per UI-SPEC's Color table (`Color.FromArgb(45,45,48)` dark / `SystemColors.Window` light for background; `Color.FromArgb(240,240,240)` dark / `SystemColors.ControlText` light for text) — do not set `rtb.BorderStyle` here (already `FixedSingle` from the Designer per UI-SPEC).

---

### `src/RigToggle.App/MainForm.Designer.cs` / `.cs` (MODIFIED — `trayCheckUpdatesMenuItem`)

**Analog:** itself — `traySettingsMenuItem` construction (`.Designer.cs` lines 68-71, 196-200, 231-235):
```csharp
this.traySettingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
...
this.traySettingsMenuItem.Text = "Settings";
this.traySettingsMenuItem.Name = "traySettingsMenuItem";
this.traySettingsMenuItem.Click += new System.EventHandler(this.TraySettingsMenuItem_Click);
...
this.trayContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
    this.trayToggleMenuItem,
    this.traySettingsMenuItem,
    this.traySeparator,
    this.trayExitMenuItem});
```
Add `trayCheckUpdatesMenuItem` with identical field/property/event-wiring shape, and reorder the `AddRange` array per UI-SPEC's locked new order: `trayToggleMenuItem, traySettingsMenuItem, trayCheckUpdatesMenuItem, traySeparator, trayExitMenuItem`. Handler in `MainForm.cs` should follow `TraySettingsMenuItem_Click`'s dispatch-to-a-shared-method shape (see `TrayToggleMenuItem_Click` → `PerformBackgroundToggle()`, lines 1929-1949) — dispatch to a shared `PerformManualUpdateCheck()`-style method callable from both the tray item and `SettingsForm`'s new button (D-05).

---

### Toasts (all D-06/D-07/D-08/D-09 notifications)

**Analog:** `src/RigToggle.App/MainForm.cs` `PerformBackgroundToggle` (lines 1949-1997) — the exact `notifyIcon.ShowBalloonTip` call shape and its `ToggleResultFormatter.TruncateForBalloon` wrapping:
```csharp
notifyIcon.ShowBalloonTip(
    3000,
    "Rig Toggle",
    ToggleResultFormatter.TruncateForBalloon(ex.Message),
    ToolTipIcon.Warning);
```
Every new toast in this phase (D-06 Info, D-07 Warning, D-08 Warning, D-09 Warning, "Updating…" Info) must use this identical 4-argument call, `title="Rig Toggle"` (never `ToggleResultFormatter.FormatModeTitle`, which is toggle-specific per UI-SPEC), and always pass the body through `ToggleResultFormatter.TruncateForBalloon(...)`. Never call `MessageBox.Show` for any of these (matches the file's own D-08 "no-chrome guarantee" doc comment, lines 1919-1927, extended by this phase to update toasts).

**`ToggleResultFormatter` reuse** — `src/RigToggle.Core/ToggleResultFormatter.cs` (full file, 64 lines): `TruncateForBalloon` (lines 53-62) is called as-is, no change needed. `FormatModeTitle`/`FormatChecklist` are toggle-specific and not reused by this phase's copy, but the file's own doc-comment convention (why each method exists, what pitfall it closes) is the pattern to follow if a new `UpdateResultFormatter`-style helper is added for the phase's own copy strings (Claude's Discretion, D-02/D-06/D-07/D-08/D-09 wording).

---

### `src/RigToggle.Core/UpdateVersionComparer.cs` (utility, transform)

**Analog:** `src/RigToggle.Core/HotkeyFormatter.cs` — pure static utility class with no I/O, fully unit-testable. Not read in full this pass (small, standard shape); the closest structural precedent already read in full is `ToggleResultFormatter.cs`'s `TruncateForBalloon` (lines 53-62) for "pure function, defensive against degenerate input, never throws." `UpdateVersionComparer.IsNewer(...)` must explicitly parse only `Major.Minor` (per Anti-Pattern 4 in ARCHITECTURE.md and PITFALLS.md Pitfall 3) — do not use raw `Version.CompareTo` on mismatched-component-count versions.

**Test analog:** `src/RigToggle.Tests/HotkeyFormatterTests.cs` — xUnit `[Theory]`/`[InlineData]` style pure-function tests; `UpdateVersionComparerTests.cs` should follow the same shape, seeded with this project's real tag history (`v1.0`→`v2.1`) plus a synthetic `v2.9`/`v2.10` case per PITFALLS.md Pitfall 3's explicit warning.

---

### `.github/workflows/release.yml` (MODIFIED)

**Analog:** itself (full file, 30 lines) — add `-p:Version=<tag-without-v>` to the existing publish step:
```yaml
- name: Publish self-contained single-file exe
  run: dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64
```
becomes (illustrative): `run: dotnet publish ... -p:Version=${{ github.ref_name }}` (stripped of leading `v`). Add a new step after publish, before the `action-gh-release@v3` step, to compute a `.sha256` alongside the exe (D-10) and include it in the `files:` list of the existing release-attach step:
```yaml
- name: Attach exe to GitHub Release
  uses: softprops/action-gh-release@v3
  with:
    files: src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
```
extend `files:` to a multi-line list including the new `.sha256` file.

---

## Shared Patterns

### Composition-root-only construction
**Source:** `src/RigToggle.App/Program.cs` (whole file's established discipline, doc comment lines 17-24)
**Apply to:** `GitHubReleaseFeed`, `WindowsUpdateApplier`, `UpdateOrchestrator` — all constructed only in `Program.cs`'s `Main()`, never `new`'d inside `MainForm`/`SettingsForm`/`UpdatePromptDialog`.

### Best-effort Trace.WriteLine diagnostic logging
**Source:** `src/RigToggle.Windows/WindowsAppController.cs` lines 102-112 (identical copies also in `WindowsAutostartConfigurator.cs` lines 61-71 and `Program.cs`'s `TryLog`, lines 374-384)
```csharp
private static void Log(string message)
{
    try { Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {TypeName}: {message}"); }
    catch { /* Logging is diagnostic-only; never let it affect behavior. */ }
}
```
**Apply to:** every new Windows-layer type (`WindowsUpdateApplier`) and the `UpdateApplyEntryPoint` replacement body.

### Toast notification (never MessageBox for background-triggered feedback)
**Source:** `src/RigToggle.App/MainForm.cs` lines 1955-1996, `src/RigToggle.Core/ToggleResultFormatter.cs` lines 53-62
**Apply to:** all D-06/D-07/D-08/D-09 toasts — `notifyIcon.ShowBalloonTip(3000, "Rig Toggle", ToggleResultFormatter.TruncateForBalloon(body), icon)`, `icon` ∈ `{Info, Warning}` only (never `Error`, per UI-SPEC).

### Themed Form, never MessageBox, for a confirm dialog
**Source:** `src/RigToggle.App/MonitorConfirmDialog.cs` (whole file) + `.Designer.cs` (whole file)
**Apply to:** `UpdatePromptDialog` — constructed with `themeProvider`, subscribes/unsubscribes `ThemeChanged` via `FormClosed`, applies `ThemeApplier.ApplyEffectiveColorMode`/`DwmTitleBar.ApplyRoundedCornersAndMica`/`ThemeApplier.ThemeButton` at construction and on live theme flip.

### Marker persistence: save-before-risky-op, clear-on-confirmed-success, best-effort catch
**Source:** `src/RigToggle.Core/ToggleOrchestrator.cs` lines 121-172 (`RunGuarded`), `src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs` (whole file), `src/RigToggle.App/StartupRecoveryChecker.cs` (whole file)
**Apply to:** D-09's update-applied-not-yet-confirmed marker — persisted before the swap/relaunch, checked and cleared-first-then-shown at the next launch's recovery pass (parallel to, but a new, separate marker/store from, `ToggleInProgressMarker`/`JsonToggleInProgressStore` — do not reuse or extend those types directly, mirroring the codebase's established discipline of keeping distinct crash-detection concerns in separate types, per Pitfall 7's analogous "don't reuse `_busy` for a different concern" lesson).

### Atomic JSON write (temp file + File.Move overwrite)
**Source:** `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` lines 76-87, `JsonToggleInProgressStore.cs` lines 28-39
```csharp
var tempPath = _path + ".tmp";
File.WriteAllText(tempPath, JsonSerializer.Serialize(data, Options));
File.Move(tempPath, _path, overwrite: true);
```
**Apply to:** any new JSON store this phase adds (D-09 marker store, or `AppSettings`'s new `SkippedUpdateVersion` field via the existing `JsonSettingsStore`).

### Rename-based exe replacement, never direct overwrite
**Source:** PITFALLS.md Pitfall 1, ARCHITECTURE.md Pattern 2 (no direct codebase analog exists — first time this project replaces its own running binary)
**Apply to:** `WindowsUpdateApplier`/`UpdateApplyEntryPoint`'s swap sequence — rename original to `.bak` → move staged exe into place → best-effort-delete `.bak` only after next-launch confirmed-healthy signal (D-09), never `File.Copy(overwrite: true)` onto the running exe's own path.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/RigToggle.Core/GitHubReleaseFeed.cs` | service | request-response (HTTP) | This is the app's first-ever `HttpClient` usage — no existing HTTP client code anywhere in the codebase to pattern-match against. Follow STACK.md's exact endpoint/`User-Agent`-header guidance and the general "Core = platform-neutral, pure BCL I/O" placement rule (`JsonSettingsStore` precedent) for where it lives, but the HTTP call shape itself has no in-repo precedent — use plain `HttpClient.GetFromJsonAsync<T>`/`GetByteArrayAsync` per STACK.md, not a bespoke wrapper. |
| SHA256 checksum verification logic (D-10/D-11) | utility | transform | No existing checksum/hash-verification code anywhere in this codebase — use `System.Security.Cryptography.SHA256` directly (BCL, no new package), following the same "pure function, defensive, never throws unexpectedly" discipline as `UpdateVersionComparer`/`ToggleResultFormatter.TruncateForBalloon`, but there's no prior in-repo hash-comparison code to copy structurally. |
| Markdown-lite parser for `rtbReleaseNotes` (D-01) | utility (UI formatter) | transform | No Markdown renderer of any kind exists in this codebase today (UI-SPEC.md explicitly notes this). Hand-roll using `RichTextBox.SelectionFont`/`SelectionBullet` runs per UI-SPEC's Component Specification section — no structural precedent to copy beyond the general "pure, defensive, never-throws utility" discipline shared by every Core formatter in this codebase. |

## Metadata

**Analog search scope:** `src/RigToggle.Core/`, `src/RigToggle.App/`, `src/RigToggle.Windows/`, `src/RigToggle.Tests/`, `.github/workflows/`
**Files scanned:** ~20 read in full or targeted excerpt (`Program.cs`, `StartupArgs.cs`, `UpdateApplyEntryPoint.cs`, `StartupRecoveryChecker.cs`, `MonitorConfirmDialog.cs`/`.Designer.cs`, `MainForm.cs` (balloon section) / `MainForm.Designer.cs` (tray menu section), `ThemeApplier.cs`, `WindowsAppController.cs`, `WindowsAutostartConfigurator.cs`, `JsonSettingsStore.cs`, `JsonToggleInProgressStore.cs`, `AppSettings.cs`, `ToggleInProgressMarker.cs`, `ToggleOrchestrator.cs`, `ToggleResultFormatter.cs`, `SettingsForm.Designer.cs` (grep), `RigToggle.App.csproj`, `release.yml`)
**Pattern extraction date:** 2026-08-22
