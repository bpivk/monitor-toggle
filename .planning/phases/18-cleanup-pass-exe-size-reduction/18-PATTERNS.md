# Phase 18: Cleanup Pass & Exe-Size Reduction - Pattern Map

**Mapped:** 2026-08-08
**Files analyzed:** 15 (10 code deletions/edits, 3 test edits, 3 MSBuild config edits — some files appear in both counts)
**Analogs found:** 15 / 15 (this phase is subtractive/config-only — every file's "analog" is either its own current content per RESEARCH.md's before/after diff, or a sibling file showing the target lean shape)

**Special note on this phase:** Unlike a typical feature phase, there are no *new* files. Every row below is an existing file being trimmed (dead-code deletion) or reconfigured (MSBuild property addition). Because of that, "analog" here means one of two things:
1. **Self-analog** — RESEARCH.md's own Code Examples section already gives the exact before/after diff for that file; the executor should follow that shape precisely.
2. **Sibling-analog** — a still-live sibling file in the same directory that already has the *lean, post-cleanup shape* the edited file should converge toward (e.g. `IModeStore`/`JsonModeStore` show what `IAudioController`/`IMonitorController`/their persistence classes should look like without a dead `Restore`-style round-trip).

## File Classification

| File | Role | Data Flow | Change Type | Closest Analog | Match Quality |
|------|------|-----------|-------------|-----------------|---------------|
| `src/RigToggle.Core/Abstractions/ISnapshotStore.cs` | interface (persistence contract) | CRUD | DELETE | `src/RigToggle.Core/Abstractions/IModeStore.cs` (sibling, shows lean post-removal shape of a store contract) | exact |
| `src/RigToggle.Core/Models/StateSnapshot.cs` | model | CRUD | DELETE | n/a — pure record, no analog needed, just delete | exact |
| `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` | persistence/service | file-I/O | DELETE | `src/RigToggle.Core/Persistence/JsonModeStore.cs` (sibling — same atomic temp-file+File.Move idiom, minus the dead round-trip) | exact |
| `src/RigToggle.Core/Abstractions/IAudioController.cs` | interface (controller contract) | request-response | EDIT (remove `Restore` member) | itself — self-analog, RESEARCH.md gives the exact member to delete | exact |
| `src/RigToggle.Core/Abstractions/IMonitorController.cs` | interface (controller contract) | request-response | EDIT (remove `Restore` member) | itself — self-analog | exact |
| `src/RigToggle.Windows/WindowsMonitorController.cs` | controller (Windows adapter) | request-response | EDIT (remove `Restore`/`RestoreViaReconstruction`/`_originalPathsCache`/`CopyOutputTechnology`/`AssignSource`; keep `AnyRectanglesOverlap`/`MergeAllMonitors`) | itself — self-analog; RESEARCH.md Pattern/Anti-Pattern sections give exact line ranges and what must survive | exact |
| `src/RigToggle.Windows/WindowsAudioController.cs` | controller (Windows adapter) | request-response | EDIT (remove `Restore` + its private stale-ID fallback usage; keep `TryResolveDevice`) | itself — self-analog | exact |
| `src/RigToggle.App/Program.cs` | composition root / bootstrap | request-response (startup) | EDIT (replace `JsonSnapshotStore`/`snapshotStore.Exists()` with bare `File.Exists()`) | itself — RESEARCH.md Pattern 2 gives the literal before/after diff | exact |
| `src/RigToggle.App/SettingsForm.cs` | UI form (WinForms) | request-response (event handlers) | EDIT (CLEANUP-02: remove dead `items.Count == 0` branch in `PopulateAudioCombo`; optionally fix sentinel-name persistence) | itself — 15-REVIEW.md IN-04/IN-03 give exact line numbers and fix text | exact |
| `src/RigToggle.Core/ToggleService.cs` | service (orchestration) | request-response | EDIT (CLEANUP-02, optional: collapse `ReconcileModeAfterMonitorFailure`'s functionally-identical branches per 16-REVIEW.md WR-03) | itself — 16-REVIEW.md WR-03 gives exact current code and two acceptable fixes | exact |
| `src/RigToggle.Tests/JsonStoreTests.cs` | test | CRUD (persistence tests) | EDIT (remove all `SnapshotStore_*` tests, keep `SettingsStore_*` tests) | itself — clear naming convention (`SnapshotStore_*` prefix) makes the removal set unambiguous | exact |
| `src/RigToggle.Tests/ToggleServiceTests.cs` | test | request-response | EDIT (remove `audioThrowsOnRestore` param/plumbing per 15-REVIEW.md IN-01; remove now-pointless `DoesNotContain(...Restore...)` assertions) | itself — 15-REVIEW.md IN-01 gives exact line numbers | exact |
| `src/RigToggle.Tests/Doubles/InMemoryStores.cs` | test double | CRUD | EDIT (remove `InMemorySnapshotStore` class) | `InMemoryModeStore` (same file, sibling class — shows the lean double shape without the removed store's `Save`/`Clear` call-log entries) | exact |
| `src/RigToggle.Tests/Doubles/FakeControllers.cs` | test double | request-response | EDIT (remove `FakeMonitorController.Restore`, `FakeAudioController.Restore` + `_throwOnRestore` field/ctor param) | `FakeAppController` (same file, sibling class — shows the lean 2-method-no-Restore double shape) | exact |
| `src/RigToggle.Tests/Doubles/BlockingMonitorController.cs` | test double | request-response | EDIT (remove `Restore` no-op stub) | itself — trivial one-method removal, no-op body already isolated | exact |
| `src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs` | test | request-response | EDIT (remove `CopyOutputTechnology_*` x2, `AssignSource_*` x4, their shared helpers `CreateFakeTarget`/`FakeSource` if unused elsewhere; keep `AnyRectanglesOverlap_*`/`MergeAllMonitors_*`) | itself — RESEARCH.md Pitfall 5 gives exact line ranges (~34-116) and confirms `FakeSource` is not reused by the surviving tests | exact |
| `src/RigToggle.App/RigToggle.App.csproj` | config (MSBuild) | config | EDIT (add `SatelliteResourceLanguages`/`InvariantGlobalization` properties) | itself — current `<PropertyGroup>` (lines 3-19) shows the existing property-addition convention (each property gets an inline `<!-- -->` comment explaining *why*, per this project's established `.csproj` documentation style) | exact |
| `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` | config (MSBuild publish profile) | config | EDIT (add `EnableCompressionInSingleFile`) | itself — the file's existing top-of-file `<!-- -->` block + `<PropertyGroup>` (lines 1-27) is the exact convention to extend | exact |
| `src/RigToggle.Windows/RigToggle.Windows.csproj` | config (MSBuild) | config | EDIT (swap `NAudio` PackageReference for `NAudio.Wasapi`) | itself — `<ItemGroup>` at lines 12-15 is the one-line swap target | exact |

## Pattern Assignments

### `src/RigToggle.Core/Abstractions/ISnapshotStore.cs` — DELETE

**Current content (whole file, 17 lines):**
```csharp
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Persistence contract for StateSnapshot. Snapshot-file presence itself is the
/// mode indicator (D-14): Mode == RigMode iff Exists() is true. Implemented by
/// RigToggle.Core.Persistence.JsonSnapshotStore (plain net10.0, no Windows API refs).
/// </summary>
public interface ISnapshotStore
{
    bool Exists();
    void Save(StateSnapshot snapshot);
    StateSnapshot? Load();
    void Clear();
}
```
**Action:** Delete the file outright. No replacement needed — `IModeStore` (kept, unrelated) already fills the "current mode" role this interface's doc comment describes as obsolete (D-14 note is itself stale, confirming this is dead).

**Confirm-dead-before-delete grep (run this exact command before deleting):**
```bash
grep -rn "ISnapshotStore\|JsonSnapshotStore\|StateSnapshot" src --include="*.cs"
```
Expected hits after this phase's edits are complete: zero, outside historical `.planning/` docs (which are not source and are never edited by this phase).

---

### `src/RigToggle.Core/Models/StateSnapshot.cs` — DELETE

**Current content (whole file, 9 lines):**
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Combined monitor + audio state captured immediately before a toggle mutation,
/// persisted via ISnapshotStore so toggle-back can restore the exact prior configuration.
/// Snapshot-file presence itself is what determines current mode (D-14): Mode == RigMode
/// iff ISnapshotStore.Exists() is true.
/// </summary>
public sealed record StateSnapshot(MonitorState Monitor, AudioState Audio);
```
**Action:** Delete the file outright. `MonitorState`/`AudioState` (the two record types it composes) are NOT deleted — both remain live (`MonitorState` via `CaptureState()`/`ReconcileModeAfterMonitorFailure`, `AudioState` via `CaptureState()`/`Restore` on `WindowsAudioController`... wait, `AudioState` is still used by `IAudioController.CaptureState()`'s return type even after `Restore` is removed — do not delete `AudioState`/`AudioRoleState`/`MonitorState`/`MonitorPathSnapshot`, only `StateSnapshot` itself.

---

### `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` — DELETE

**Current content (whole file, 66 lines) — already fully read; key structure to note for the executor:**
```csharp
public sealed class JsonSnapshotStore : ISnapshotStore
{
    private readonly string _path;
    public JsonSnapshotStore(string path) { _path = path; }
    public bool Exists() => File.Exists(_path);
    public void Save(StateSnapshot snapshot) { /* temp-file + File.Move atomic write */ }
    public StateSnapshot? Load() { /* JsonException -> null */ }
    public void Clear() { if (Exists()) File.Delete(_path); }
}
```
**Action:** Delete the file outright.

**Sibling-analog for what a lean, post-cleanup persistence class looks like** — `src/RigToggle.Core/Persistence/JsonModeStore.cs:19-86` (kept, unrelated store) uses the exact same atomic temp-file + `File.Move(..., overwrite: true)` write pattern this file used, minus any now-dead round-trip method. No new persistence class needs to be written — this is purely informative for reviewers confirming the deletion doesn't orphan the atomic-write idiom elsewhere (it doesn't; `JsonSettingsStore`/`JsonModeStore`/`JsonToggleInProgressStore` all keep their own independent copies of this idiom).

---

### `src/RigToggle.Core/Abstractions/IAudioController.cs` — EDIT (remove `Restore`)

**Current content (whole file, 26 lines):**
```csharp
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

public interface IAudioController
{
    IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
    AudioState CaptureState();
    void SetDefault(string deviceId);
    void Restore(AudioState previousState);   // <-- DELETE this line (line 16)

    AudioDeviceInfo? TryResolveDevice(string? deviceId);
}
```
**Action:** Delete line 16 (`void Restore(AudioState previousState);`) only. Update the class-doc-comment (lines 5-10) to stop describing `Restore` as part of the live contract — it currently says "SetDefault/Restore are no-op stubs until Phase 3," which is now doubly stale (both the "no-op stub" framing AND `Restore`'s continued existence).

**Sibling-analog for the resulting lean shape:** `src/RigToggle.Core/Abstractions/IModeStore.cs` (whole file, 3-method interface, no round-trip/restore member) — this is what `IAudioController` converges toward after this edit: a set of forward-only operations (`GetPlaybackDevices`, `CaptureState`, `SetDefault`, `TryResolveDevice`), no undo/restore member.

---

### `src/RigToggle.Core/Abstractions/IMonitorController.cs` — EDIT (remove `Restore`)

**Current content relevant excerpt (lines 13-45):**
```csharp
public interface IMonitorController
{
    IReadOnlyList<MonitorInfo> GetActiveMonitors();
    IReadOnlyList<MonitorInfo> GetAllMonitors();
    MonitorState CaptureState();
    void ActivateMonitors(IReadOnlySet<string> monitorDevicePaths);
    void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths);
    void Restore(MonitorState previousState);   // <-- DELETE this line (line 44)
}
```
**Action:** Delete line 44 only. Update the class-doc-comment (lines 5-12) — it currently lists `Restore` alongside `ActivateMonitors`/`DeactivateMonitors` as "real starting Phase 4/6"; remove that mention.

---

### `src/RigToggle.Windows/WindowsMonitorController.cs` — EDIT (large subtractive edit)

**What stays (verified live production code — do NOT touch):**
- `GetActiveMonitors()` (lines 69-87)
- `GetAllMonitors()` (lines 104-116)
- `MergeAllMonitors()` (lines 127-151) — **and its `WindowsMonitorControllerTests.cs` test coverage must also stay**
- `CaptureState()` (lines 162-186)
- `ActivateMonitors()` (lines 207-253)
- `DeactivateMonitors()` (lines 268-366) — calls `AnyRectanglesOverlap` internally, must stay
- `AnyRectanglesOverlap()` (lines 646-660) — **and its test coverage must also stay**

**What is deleted (confirmed zero production callers per RESEARCH.md's exhaustive grep):**
- `_originalPathsCache` field (line 67) and its doc comment (lines 58-66)
- `Restore(MonitorState previousState)` (lines 379-453)
- `RestoreViaReconstruction(MonitorState previousState)` (lines 455-637)
- `CopyOutputTechnology(...)` (lines 662-694, including its WR-02 doc comment)
- `AssignSource(...)` (lines 696-726, including its WR-02 doc comment)

**Also update:** the class-level doc comment (lines 10-55) currently describes `Restore`'s reconstruction strategy at length (three paragraphs) — trim it down to describe only the methods that remain, per RESEARCH.md's own framing that this doc comment is "framed entirely around Restore."

**Anti-pattern to avoid (RESEARCH.md, verified directly against this file):** Do NOT delete `AnyRectanglesOverlap` or `MergeAllMonitors` — both are called from still-live methods (`DeactivateMonitors` calls `AnyRectanglesOverlap` at line 354; `GetAllMonitors` calls `MergeAllMonitors` at line 115).

---

### `src/RigToggle.Windows/WindowsAudioController.cs` — EDIT

**What stays:** `GetPlaybackDevices()`, `CaptureState()`, `SetDefault()`/`SetDefaultForAllRoles()`, `ApplyAndVerify()` (shared helper — used by both `SetDefault` and the deleted `Restore`, but stays because `SetDefault` still needs it), `TryResolveDevice()`.

**What is deleted:** `Restore(AudioState previousState)` (lines 126-176) in its entirety — including its internal stale-ID-to-friendly-name fallback logic (lines 137-153), which exists ONLY to serve `Restore`.

**Anti-pattern to avoid (RESEARCH.md, explicit):** Do not confuse `TryResolveDevice` (lines 219-236, a distinct Phase 15/AUDIO-05 method still called by `WindowsMonitorController`... actually called by `Restore` itself at line 139, and by other Phase 15 code) with `Restore`'s own internal fallback — `TryResolveDevice` stays; only the `Restore` method itself and its call to `TryResolveDevice` go away together.

**Also update:** the class-level doc comment (lines 9-21) mentions "SetDefault/Restore apply the requested device... re-verify... throwing" — remove the `Restore`-specific half of that sentence.

---

### `src/RigToggle.App/Program.cs` — EDIT (Pattern 2, minimal-replacement)

**Before (lines 93, 102-105):**
```csharp
var snapshotStore = new JsonSnapshotStore(Path.Combine(basePath, "state.json"));
...
if (!modeStore.Exists())
{
    modeStore.Save(snapshotStore.Exists() ? ToggleMode.Rig : ToggleMode.Normal);
}
```
**After:**
```csharp
string legacyStateJsonPath = Path.Combine(basePath, "state.json");
...
if (!modeStore.Exists())
{
    modeStore.Save(File.Exists(legacyStateJsonPath) ? ToggleMode.Rig : ToggleMode.Normal);
}
```
**Also:** remove the now-unused `using RigToggle.Core.Persistence;` import IF `JsonSettingsStore`/`JsonModeStore`/`JsonToggleInProgressStore` (all still constructed in this same file, lines 52/94/95) don't need it anymore — they do, so this `using` stays; only the `JsonSnapshotStore` construction line itself is removed. **Why this is correct (RESEARCH.md Pattern 2):** `JsonSnapshotStore.Exists()` was itself just `File.Exists(_path)` (confirmed by reading `JsonSnapshotStore.cs:24` above) — the abstraction added zero logic beyond a raw file-existence check for this one call site, so inlining loses nothing.

---

### `src/RigToggle.App/SettingsForm.cs` — EDIT (CLEANUP-02, IN-04 required / IN-03 optional)

**IN-04 (recommended — dead branch):** `PopulateAudioCombo` at lines 702-720:
```csharp
private void PopulateAudioCombo(ComboBox combo, ErrorProvider errProvider, Label warningLabel, List<PickerItem> items, string? savedId)
{
    errProvider.SetError(combo, string.Empty);
    warningLabel.Visible = false;
    combo.SelectedIndexChanged -= OnPickerChanged;

    if (items.Count == 0)          // <-- unreachable since PopulateAudioPickers (line 695)
    {                                //     always prepends the sentinel; whole `if` block
        combo.DataSource = null;    //     (lines 713-720) can be deleted
        combo.Items.Clear();
        combo.Items.Add("No audio devices detected.");
        combo.SelectedIndex = -1;
        combo.Enabled = false;
    }
    else
    {
        // ... this else-branch body becomes the new unconditional body
    }
}
```
**Fix:** Delete the `if (items.Count == 0) { ... }` block (lines 713-720) and its surrounding `if/else`, promoting the `else` branch body (lines 722-750ish) to be the method's unconditional body. Per 15-REVIEW.md IN-04, this is optional-but-recommended, not required — "No action required if the defensive branch is intentionally kept."

**IN-03 (optional, lower priority — explicitly NOT required per RESEARCH.md anti-patterns):** `BtnSaveSettings_Click` at lines 1089-1092:
```csharp
NormalAudioDeviceId = audioNormalItem.Id,
NormalAudioDeviceName = audioNormalItem.DisplayLabel,   // <-- persists "(None — don't switch audio)" literally when sentinel selected
RigAudioDeviceId = audioRigItem.Id,
RigAudioDeviceName = audioRigItem.DisplayLabel,
```
**Fix (if attempted):** `NormalAudioDeviceName = audioNormalItem.Id is null ? null : audioNormalItem.DisplayLabel` (same for Rig pair). **Do not treat this as required** — both `15-REVIEW.md` and its own `15-03-SUMMARY.md` accepted this as a cosmetic, intentional limitation; RESEARCH.md's own Anti-Patterns section explicitly warns against over-scoping this.

---

### `src/RigToggle.Core/ToggleService.cs` — EDIT (CLEANUP-02, WR-03, optional)

**Current (lines 246-263):**
```csharp
private void ReconcileModeAfterMonitorFailure(Models.MonitorState before)
{
    try
    {
        if (MonitorStateUnchanged(before, _monitorController.CaptureState()))
        {
            return;
        }

        // Partial mutation: leave the mode flag at its prior value (Assumptions
        // Log A3, 16-RESEARCH.md) rather than guess a new mode.
    }
    catch
    {
        // Re-capture failed — can't confirm anything, same fail-safe posture as
        // the original CR-01 catch block: do nothing, leave the mode flag as-is.
    }
}
```
Both branches are no-ops (16-REVIEW.md WR-03: "Neither branch ever calls `_modeStore.Save(...)`"). **Fix options (either acceptable per 16-REVIEW.md):**
1. Add a `Trace.WriteLine` distinguishing the two cases for future debugging, keeping the branch structure, OR
2. Collapse to a single try/catch that just recaptures-and-discards, with a comment explaining the check is currently informational/for future extension only.
**Scope note:** this is the lowest-priority CLEANUP-02 candidate — 16-REVIEW.md itself marked it "not fixed... cosmetic only... deferred rather than risk touching CR-01-adjacent logic." Treat as optional; do not let it block the phase.

---

### Test files (`JsonStoreTests.cs`, `ToggleServiceTests.cs`, `Doubles/*.cs`, `WindowsMonitorControllerTests.cs`)

**`JsonStoreTests.cs`:** delete every `SnapshotStore_*`-prefixed `[Fact]` (confirmed test names: `SnapshotStore_Exists_IsFalseBeforeSave_TrueAfterSave` at line 261, `SnapshotStore_Clear_DeletesFile_SoExistsReturnsFalseAgain` at line 274, `SnapshotStore_Load_ReturnsNullWhenAbsent_AndSavedSnapshotWhenPresent` at line 286, `SnapshotStore_Load_OnMalformedFile_ReturnsNullInsteadOfThrowing` at line 305, `SnapshotStore_MonitorState_RoundTripsAllPathFields` at line 322 — verify with `grep -n "SnapshotStore_" JsonStoreTests.cs` at execution time since line numbers shift as earlier tests are edited). Every `SettingsStore_*` test (lines 31-259) stays untouched — different class under test (`JsonSettingsStore`, unrelated to this phase).

**`ToggleServiceTests.cs`:** per 15-REVIEW.md IN-01 — remove the `audioThrowsOnRestore` parameter from `CreateService` (line 50) and its pass-through to `FakeAudioController(..., throwOnRestore: audioThrowsOnRestore, ...)` (line 63). Also remove the two now-pointless `Assert.DoesNotContain(callLog, entry => entry.StartsWith("audio.Restore"))` / `"monitor.Restore"` assertions (lines 142-143, 529) — once `Restore` no longer exists anywhere, asserting its absence from a call log is vacuous, not a regression guard.

**`Doubles/InMemoryStores.cs`:** delete `InMemorySnapshotStore` (lines 11-33) in its entirety. `InMemorySettingsStore`, `InMemoryModeStore`, `InMemoryToggleInProgressStore`, `ThrowingClearToggleInProgressStore` (the rest of the file) all stay untouched.

**`Doubles/FakeControllers.cs`:** delete `FakeMonitorController.Restore` (lines 77-80) and `FakeAudioController.Restore` (lines 120-131) plus `FakeAudioController`'s `_throwOnRestore` field (line 87) and its constructor parameter (line 93) — but keep `_deviceExists`/`deviceExists` (still used by `TryResolveDevice`, an unrelated live feature). `FakeAppController` (lines 146-183) is untouched — sibling-analog showing the lean per-double shape (no `Restore`-style member) to converge toward.

**`Doubles/BlockingMonitorController.cs`:** delete the `Restore(MonitorState previousState)` no-op stub (lines 63-66) only; the rest of the file (blocking `DeactivateMonitors` mechanism) is the entire point of this double and stays untouched.

**`WindowsMonitorControllerTests.cs`:** delete `CopyOutputTechnology_DefaultsToOther_BeforePatch` + `CopyOutputTechnology_PatchesBackingField_ToRequestedValue` (lines 34-50), `CreateFakeTarget` helper (lines 27-32, used only by the two CopyOutputTechnology tests), `AssignSource_*` (4 tests, lines 55-116), and `FakeSource` helper (lines 52-53, used only by the four AssignSource tests) — confirmed via direct read that neither helper is referenced by any `AnyRectanglesOverlap_*`/`MergeAllMonitors_*` test. Update the file's header comment (lines 12-18, currently framed entirely around "Restore()'s reconstruction logic") to describe what remains: the pure `AnyRectanglesOverlap`/`MergeAllMonitors` dedup/geometry helpers. **Warning sign (RESEARCH.md Pitfall 5):** exactly 6 tests should be removed from this file — a larger `dotnet test` count regression signals over-deletion.

---

### `src/RigToggle.App/RigToggle.App.csproj` — EDIT (PERF-01)

**Current `<PropertyGroup>` (lines 3-19):**
```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- Intentionally no elevation manifest ... -->
    <!-- Embeds app.ico ... -->
    <ApplicationIcon>Resources\app.ico</ApplicationIcon>
</PropertyGroup>
```
**Add (matching this project's existing convention of an inline `<!-- -->` comment per non-obvious property):**
```xml
<!-- PERF-01/18-RESEARCH.md: excludes localized satellite resource assemblies from
     WindowsDisplayAPI/NAudio.Wasapi/the BCL. Value must be "en", not "en-US" — see
     18-RESEARCH.md Pitfall 2 for why "en-US" alone would understate the savings. -->
<SatelliteResourceLanguages>en</SatelliteResourceLanguages>
<!-- PERF-01/18-RESEARCH.md: disables ICU culture-data loading. Confirmed safe for
     this app — no user-facing culture-dependent date/number formatting exists
     anywhere in src/ (18-RESEARCH.md Pitfall 3, verified by direct grep). -->
<InvariantGlobalization>true</InvariantGlobalization>
```
**Where:** inside the existing `<PropertyGroup>`, alongside the other properties — do not create a second `<PropertyGroup>`.

---

### `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` — EDIT (PERF-01)

**Current (whole file, 27 lines) — already fully read above.** Add `EnableCompressionInSingleFile` inside the existing `<PropertyGroup>` (lines 20-26), alongside `PublishTrimmed` (which must remain literally `false` — do not touch that line):
```xml
<PropertyGroup>
    <PublishDir>bin\publish\win-x64\</PublishDir>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```
**Also:** extend the file's existing top-of-file `<!-- -->` doc block (lines 1-18) with a note on the startup-decompression tradeoff (RESEARCH.md Pitfall 1), matching this file's existing convention of documenting *why* each property is set, not just *that* it is set.

---

### `src/RigToggle.Windows/RigToggle.Windows.csproj` — EDIT (PERF-01, NAudio split)

**Current (lines 12-15):**
```xml
<ItemGroup>
    <PackageReference Include="WindowsDisplayAPI" Version="1.3.0.13" />
    <PackageReference Include="NAudio" Version="2.3.0" />
</ItemGroup>
```
**After:**
```xml
<ItemGroup>
    <PackageReference Include="WindowsDisplayAPI" Version="1.3.0.13" />
    <!-- PERF-01/18-RESEARCH.md: NAudio.Wasapi (not the NAudio meta-package) — this
         project's only NAudio usage is `using NAudio.CoreAudioApi;` in
         WindowsAudioController.cs, which lives in this sub-package; the meta-package's
         other backends (WinMM/Midi/Asio/WinForms/Dmo) were always dead weight here.
         Zero source-code changes required — namespace is unchanged. -->
    <PackageReference Include="NAudio.Wasapi" Version="2.3.0" />
</ItemGroup>
```
**Verification (already confirmed working live in RESEARCH.md's own research session):** `grep -rln "NAudio" src` shows exactly one file (`WindowsAudioController.cs`) references NAudio at all — no other file in the solution needs updating for this swap.

## Shared Patterns

### Confirm-dead-before-delete (applies to every CLEANUP-01 file)
**Source:** RESEARCH.md Pattern 1, methodologically identical to `15-REVIEW.md` WR-01's own approach.
**Apply to:** `ISnapshotStore.cs`, `StateSnapshot.cs`, `JsonSnapshotStore.cs`, both `Restore` methods, `CopyOutputTechnology`, `AssignSource`, and every test/double file touched.
```bash
grep -rn "\.Restore(" src --include="*.cs"
grep -rn "ISnapshotStore\|JsonSnapshotStore\|StateSnapshot" src --include="*.cs"
```
Run this BEFORE deleting anything in a given file, and again AFTER all edits in that wave, to confirm zero remaining references outside `.planning/` docs.

### Minimal-replacement for a live bootstrap read (Program.cs only)
**Source:** RESEARCH.md Pattern 2 — see full excerpt in the `Program.cs` Pattern Assignment above. This is the ONLY production call site anywhere in the solution that needs a replacement (not just a deletion) as part of CLEANUP-01.

### MSBuild property-addition convention (applies to all three config file edits)
**Source:** This project's own existing `.csproj`/`.pubxml` files (`RigToggle.Core.csproj` lines 7-11, `RigToggle.Windows.csproj` lines 8-9, `RigToggle.App.csproj` lines 13-14/16-17, `win-x64.pubxml` lines 1-18) — every non-obvious property in this codebase already carries an inline `<!-- -->` comment explaining *why*, often citing the research doc that justified it (e.g. `05-RESEARCH.md Pitfall 1`). PERF-01's new properties should follow the exact same citation style, referencing `18-RESEARCH.md` by name.

### Regression gate: `PublishTrimmed` must stay `false`
**Source:** RESEARCH.md Pitfall 6, `win-x64.pubxml` line 24 (already `false`, already commented).
**Apply to:** Any diff touching `win-x64.pubxml` in this phase.
```bash
grep -n "PublishTrimmed" src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml
# must print: <PublishTrimmed>false</PublishTrimmed>  -- never absent, never true
```

### Cross-target build/test/publish verification (non-rig checkpoint, every wave except final)
**Source:** RESEARCH.md Pattern 3, already this project's established convention per Phase 16/17 SUMMARY.md files.
```bash
PATH="$HOME/.dotnet:$PATH" dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
PATH="$HOME/.dotnet:$PATH" dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj
PATH="$HOME/.dotnet:$PATH" dotnet test src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj
PATH="$HOME/.dotnet:$PATH" dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64 -p:EnableWindowsTargeting=true
ls -la src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe   # compare against 116946229 baseline
```

## No Analog Found

None. Every file in this phase's scope is an edit or deletion of an existing file with either (a) an exact self-diff already specified in RESEARCH.md's Code Examples, or (b) a still-live sibling file in the same directory showing the target post-cleanup shape.

## Metadata

**Analog search scope:** `src/RigToggle.Core/`, `src/RigToggle.Windows/`, `src/RigToggle.App/`, `src/RigToggle.Tests/`, `src/RigToggle.Windows.Tests/` (all five projects — matches RESEARCH.md's Recommended Project Structure exactly)
**Files scanned:** 19 source files read directly (all files listed in File Classification), plus 3 prior REVIEW.md files (`15-REVIEW.md`, `16-REVIEW.md`, `17-REVIEW.md`) for CLEANUP-02 candidate line references, plus `git log --diff-filter=D` for historical deletion-commit precedent (none directly analogous — this is the codebase's first dead-code cleanup phase; prior deletions were template-scaffolding removal, e.g. `Form1.cs`/`Form1.Designer.cs`/`Form1.resx` in commit `9a9c239`)
**Pattern extraction date:** 2026-08-08
