# Phase 25: Single-Instance Guard - Pattern Map

**Mapped:** 2026-08-20
**Files analyzed:** 6 (2 new, 4 modified/reference)
**Analogs found:** 6 / 6

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|-----------------|---------------|
| `src/RigToggle.Core/SingleInstanceGuard.cs` (new) | service/guard class | event-driven (cross-process signal) | `src/RigToggle.Core/ToggleOrchestrator.cs` | role-match (structural precedent for a dedicated single-purpose guard class) |
| `src/RigToggle.Core/StartupArgs.cs` (modified — add `TryGetApplyUpdateArgs`) | utility | transform (CLI arg parsing) | same file's existing `ShouldStartHidden(args)` | exact (same file, same convention) |
| `src/RigToggle.App/Program.cs` (modified — insert bypass check + mutex acquisition) | config/composition-root | request-response (startup branching) | itself (existing `StartupArgs.ShouldStartHidden` branch, lines 198-205) | exact (same file, same branching idiom) |
| `src/RigToggle.App/MainForm.cs` (modified — new `WndProc` case for activation signal, if message-based) | controller/UI | event-driven (Win32 message handling) | itself, existing `WndProc` WM_HOTKEY case (lines 247-255) and tray-restore sequence (lines 1525-1533) | exact (same file, same pattern) |
| `src/RigToggle.Tests/SingleInstanceGuardTests.cs` (new) | test | process/event-driven | `src/RigToggle.Tests/ToggleOrchestratorTests.cs` | role-match (deterministic concurrency test structure) |
| `src/RigToggle.Tests/StartupArgsTests.cs` (modified — add `TryGetApplyUpdateArgs` cases) | test | transform | same file's existing `ShouldStartHidden` theory-based tests | exact |

## Pattern Assignments

### `src/RigToggle.Core/StartupArgs.cs` — add `TryGetApplyUpdateArgs`

**Analog:** same file, existing `ShouldStartHidden` (the whole 23-line file)

**Full existing pattern to mirror** (`src/RigToggle.Core/StartupArgs.cs` lines 1-23):
```csharp
namespace RigToggle.Core;

public static class StartupArgs
{
    private const string TrayFlag = "--tray";

    public static bool ShouldStartHidden(string[]? args) =>
        args is not null && args.Contains(TrayFlag, StringComparer.OrdinalIgnoreCase);
}
```

**What to copy:**
- Same static class, same file (do not create a new file — this is the established single home for CLI-arg parsing, and lives in `RigToggle.Core` specifically so it is unit-testable without a `RigToggle.App`-side test project).
- Same defensive-null contract: must never throw on `null`/empty/garbage `args` (documented in the class doc comment as "Security Domain V5" — an autostart-launched process with no UI yet must never crash on a malformed arg array).
- `TryGetApplyUpdateArgs(args)` is a `Try*`-shaped helper (per D-04's naming) rather than a bool predicate like `ShouldStartHidden`, since it needs to both detect the flag AND extract the trailing args. Follow the standard C# `TryGetX(string[] args, out X result)` shape, case-insensitive token match on `--apply-update` exactly like `TrayFlag`'s `StringComparer.OrdinalIgnoreCase` match, and treat everything after the flag token (or a defined sub-set) as the payload to hand back.
- Add a doc comment following the existing one's structure: purpose, why it lives in Core, and the never-throws contract.

**Test analog** (`src/RigToggle.Tests/StartupArgsTests.cs`, full file, lines 1-33): `[Theory]`/`[InlineData]` table-driven cases covering exact-token match, case-insensitivity, combined with other flags, empty array, unrelated tokens, near-miss tokens (`-tray`, `--tray-x`), plus a dedicated `[Fact]` for the null-args-does-not-throw contract. Mirror this exact structure for `TryGetApplyUpdateArgs`, adding cases for "flag present with trailing payload args" and "flag present with no trailing args."

---

### `src/RigToggle.App/Program.cs` — bypass check + mutex acquisition insertion

**Analog:** itself — the existing `StartupArgs.ShouldStartHidden(args)` branch pattern (lines 198-205) and the file's documented best-effort-vs-deliberate-exception idiom (lines 37-46, 76-91, 107-113)

**Insertion-point pattern — position-sensitive calls at top of `Main()`** (lines 34-46):
```csharp
[STAThread]
static void Main(string[] args)
{
    // ... must be the very first executable statement of Main(), before
    // ApplicationConfiguration.Initialize() and before any Form/control is
    // constructed (Pitfall 1) ...
    System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);

    // To customize application configuration such as set high DPI settings or default font,
    // see https://aka.ms/applicationconfiguration.
    ApplicationConfiguration.Initialize();
```
Per D-03/CONTEXT.md Pattern 1: the `--apply-update` bypass check comes immediately after these two position-sensitive calls but before mutex acquisition, before `settingsStore`/`modeStore` bootstrap, and before any `Form` is constructed. If `StartupArgs.TryGetApplyUpdateArgs(args)` matches, control transfers to the placeholder relaunch-helper entry point and returns — the mutex, tray, and hotkey path must never execute on that branch.

**Existing branch-on-parsed-flag idiom to mirror for both the bypass check and the mutex-fail early-return** (lines 198-205):
```csharp
if (StartupArgs.ShouldStartHidden(args))
{
    Application.Run(new ApplicationContext());
}
else
{
    Application.Run(mainForm);
}
```
Use this same "parse via `RigToggle.Core` static helper → branch in `Main()`" shape for both: (1) the `--apply-update` check as the first branch in `Main()`, returning before any further bootstrap; (2) the mutex-acquisition-fails branch, which sends the activation signal and then returns/exits before any Form is constructed.

**Deliberate-exception vs. best-effort idiom** (lines 107-113, comment on `StartupRecoveryChecker.Run`):
```csharp
// Pattern 3 (16-RESEARCH.md): the two blocking startup checks (mode
// corruption, crash-recovery) run after the bootstrap above and before any
// toggle-capable object is constructed ... Deliberately NOT wrapped
// in a try/catch: these are the one deliberate exception to this file's
// best-effort-swallow startup idiom (D-06/D-07).
StartupRecoveryChecker.Run(modeStore, markerStore);
```
Per CONTEXT.md's "Established Patterns" note, the mutex acquisition and bypass-flag check are correctness-critical (not diagnostic), so they should follow this deliberate-exception category rather than the best-effort try/catch-and-continue idiom used for the trace-listener block (lines 76-91) or hotkey registration.

**Composition-root construction idiom** (doc comment lines 16-23, and object construction at lines 115-118):
```csharp
var monitorController = new WindowsMonitorController();
var audioController = new WindowsAudioController();
var appController = new WindowsAppController();
var autostartConfigurator = new WindowsAutostartConfigurator();
```
`SingleInstanceGuard` (or equivalent) should be `new`'d here in `Program.cs` alongside the other adapters — never constructed inside `MainForm`/`SettingsForm` (Anti-Pattern 2, explicitly called out in the class doc comment).

---

### `src/RigToggle.Core/SingleInstanceGuard.cs` (new)

**Analog:** `src/RigToggle.Core/ToggleOrchestrator.cs` — structural precedent only, NOT a class to extend or reuse directly (Pitfall 7 explicitly rules out reusing `ToggleOrchestrator._busy`)

**What to copy — shape, not mechanism:**
- Small, single-purpose, sealed guard class in `RigToggle.Core` namespace (lines 31-46 show `ToggleOrchestrator`'s constructor-injection-of-dependencies + doc-comment-heavy style).
- Extensive XML doc comments explaining *why* each design decision was made (non-blocking vs blocking, what it's NOT reusing, the finally-release discipline) — this codebase's established documentation density; mirror it for `SingleInstanceGuard`.
- `IDisposable`-returning acquire pattern is present in `ToggleOrchestrator.BeginExclusiveMonitorAccess()` (lines 88-97) and its nested private lease class (`ExclusiveMonitorAccessLease`, lines 105-119) using `Interlocked.Exchange` to guard against double-release. If `SingleInstanceGuard`'s mutex-hold needs a scoped-release shape (e.g. released in `finally` around `Application.Run` per STACK.md), this nested-lease-with-double-dispose-guard shape is the direct precedent to copy.
- **Do NOT** reuse `_busy`/`Interlocked.CompareExchange` — that primitive is for same-process in-memory reentrancy (CORE-06), not a cross-process OS-level `Mutex`. `SingleInstanceGuard` needs `System.Threading.Mutex` (named, `Global\` prefix) per STACK.md — a genuinely different primitive, just the same "one dedicated class, constructor-injected where needed, released in a `finally`" shape.

---

### `src/RigToggle.App/MainForm.cs` — activation-signal `WndProc` case + restore call (if `RegisterWindowMessage` chosen)

**Analog:** itself — existing `WndProc` WM_HOTKEY case and existing tray-restore sequence

**WndProc pattern to mirror** (lines 239-255):
```csharp
/// <summary>
/// TRIG-01: intercepts WM_HOTKEY, the message user32.dll posts to this window
/// once GlobalHotkey.Register has bound GlobalHotkeyId to it. base.WndProc MUST
/// run unconditionally for every message, not just when the id doesn't match --
/// skipping it (e.g. inside an early-return branch) would silently break WinForms'
/// own focus/tray/paint message handling for this window. Do not "optimize" this
/// into a conditional base call.
/// </summary>
protected override void WndProc(ref Message m)
{
    if (m.Msg == GlobalHotkey.WmHotkey && (int)m.WParam == GlobalHotkeyId)
    {
        HandleHotkeyToggle();
    }

    base.WndProc(ref m);
}
```
A new custom activation message (registered via `RegisterWindowMessage`, per STACK.md's recommendation) should add a second `if` branch here in the same style — check `m.Msg == <registered message id>`, call a new `HandleActivationSignal()`-style private method, and critically **must still call `base.WndProc(ref m)` unconditionally** exactly as the existing comment warns.

**Restore sequence to reuse exactly, per D-01** (lines 1519-1533, `NotifyIcon_MouseClick`):
```csharp
/// <summary>
/// TRAY-05/D-02: NotifyIcon.MouseClick ... restores
/// and focuses the main window on a LEFT click only ...
/// </summary>
private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
{
    if (e.Button == MouseButtons.Left)
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }
}
```
Per D-01, the new activation-signal handler must call the exact same three-line sequence (`Show()`; `WindowState = FormWindowState.Normal`; `Activate()`) — either by extracting it into a small shared private helper both `NotifyIcon_MouseClick` and the new signal handler call, or by duplicating the three lines inline (matching this codebase's existing tolerance for small duplicated Win32-adjacent sequences, e.g. `MainForm_Resize`'s comment at line 1495-1496 explicitly says restore happens "through the existing NotifyIcon_MouseClick left-click path," implying that path is the canonical one to invoke/extract, not reinvent).

---

### `src/RigToggle.Windows/GlobalHotkey.cs` — cross-assembly P/Invoke wrapper convention (reference, if new P/Invoke is needed)

**Analog / convention to follow** if `SingleInstanceGuard` or the activation-signal plumbing needs new `user32.dll` P/Invoke (`RegisterWindowMessage`, `PostMessage(HWND_BROADCAST, ...)`):

Full file (`src/RigToggle.Windows/GlobalHotkey.cs`, 38 lines) — public static wrapper class in `RigToggle.Windows`, delegating to an `internal NativeMethods` class holding the actual `[DllImport]`. Doc comment explains *why* this indirection exists (no `InternalsVisibleTo` grant from `RigToggle.Windows` to `RigToggle.App`; every cross-assembly Windows surface — `WindowsAutostartConfigurator`, `WindowsAppController`, `WindowsAudioController`, `WindowsMonitorController` — follows the same public-facade pattern). Any new P/Invoke this phase needs (e.g. `RegisterWindowMessage`/`PostMessage`) should follow this exact shape: raw `DllImport` in `NativeMethods` (internal), thin public static wrapper class/method in `RigToggle.Windows` for `Program.cs`/`MainForm.cs` to call.

---

### `src/RigToggle.Tests/SingleInstanceGuardTests.cs` (new)

**Analog:** `src/RigToggle.Tests/ToggleOrchestratorTests.cs` (full file, 370 lines) — deterministic concurrency-test structure

**What to copy:**
- `IDisposable`-implementing test class with cleanup in `Dispose()` (lines 26, 51) — for `SingleInstanceGuardTests`, this would clean up any named mutex/process still alive after a test.
- `ManualResetEventSlim`-based deterministic synchronization instead of fixed-duration `Thread.Sleep`/timing guesses (lines 111-133 pattern: signal "entered guarded region," block, release, assert) — the existing project convention (explicitly cited as "07-RESEARCH.md Pitfall 2" in the doc comment) for proving race/reentrancy behavior without flakiness. Apply the same discipline to the rapid-relaunch and bypass-simulation tests (D-05), even though those tests will additionally spawn real child processes rather than just background `Task.Run` closures.
- A bounded wait constant like `GuardedRegionWaitTimeout = TimeSpan.FromSeconds(5)` (line 106) with a clear failure message on timeout (`"Blocking step was never entered."`) rather than an unbounded/hanging wait — apply the same bounded-wait-with-message discipline to any `Process.WaitForExit(timeout)` calls in the new child-process tests.
- Doc-comment-per-test explaining the specific behavior/decision-ID being proven (e.g. `// D-02: one shared flag guards BOTH directions...`) — every test in the analog file traces back to a specific decision (D-01, D-02, D-04, D-05, Pitfall 3, DISPLAY-13, CR-01, T-17-01, etc.). New tests should cite D-01/D-02/D-05/D-06/Pitfall 8 by ID in their doc comments the same way.
- `Assert.Throws<SpecificExceptionType>` pattern (line 124, 150, 285, 306-307) for proving rejection — if `SingleInstanceGuard` exposes a "second instance detected" signal as an exception or return value, prove both the positive and negative path exactly like `ToggleToRigMode_RejectsSecondCallWhileFirstInFlight_SameDirection`.

**Process-launching specifics (D-05/D-06) — no direct in-repo analog:** no existing test spawns a real child `dotnet`/exe process. Use `System.Diagnostics.Process.Start` with `ProcessStartInfo` (redirect nothing needed if only exit code / timing matters), `Process.WaitForExit(TimeSpan)` bounded exactly like the `ManualResetEventSlim.Wait(GuardedRegionWaitTimeout)` bound above, and clean up any surviving process handles in `Dispose()`. Target whichever build output `dotnet test` already runs against in CI (see `.github/workflows/build.yml` below) rather than requiring a separate publish step, unless single-file-publish-specific mutex/signal behavior needs proving.

---

## Shared Patterns

### Composition-root-only construction
**Source:** `src/RigToggle.App/Program.cs` doc comment (lines 16-23) and adapter construction block (lines 115-118, 144-149, 156, 167-170)
**Apply to:** `SingleInstanceGuard` construction — must be `new`'d in `Program.cs` only, never inside `MainForm`/`SettingsForm`.

### Best-effort vs. deliberate-exception startup idiom
**Source:** `src/RigToggle.App/Program.cs` — best-effort blocks at lines 76-91 (trace listener) and 177-183 (hotkey registration comment); deliberate-exception block at lines 107-113 (`StartupRecoveryChecker.Run`)
**Apply to:** The bypass-flag check and mutex acquisition in `Program.cs` — per CONTEXT.md's Established Patterns note, these are correctness-critical and should NOT be wrapped in a swallow-and-continue try/catch the way trace-listener setup or hotkey registration are.

### `RigToggle.Core` static-helper-parses / `Program.cs`-branches idiom
**Source:** `src/RigToggle.Core/StartupArgs.cs` (`ShouldStartHidden`) + `src/RigToggle.App/Program.cs` lines 198-205
**Apply to:** `StartupArgs.TryGetApplyUpdateArgs(args)` — parse in Core (testable, no throw on bad input), branch in `Program.cs Main()`.

### Nested-lease-with-double-dispose-guard for scoped resource release
**Source:** `src/RigToggle.Core/ToggleOrchestrator.cs` lines 88-119 (`BeginExclusiveMonitorAccess` / `ExclusiveMonitorAccessLease`)
**Apply to:** `SingleInstanceGuard`'s mutex-hold lifetime, if it needs an explicit acquire/release scope rather than a bare field held for `Program.cs`'s lifetime.

### Cross-assembly public P/Invoke facade
**Source:** `src/RigToggle.Windows/GlobalHotkey.cs` (full file)
**Apply to:** Any new `user32.dll` calls (`RegisterWindowMessage`, `PostMessage`) needed for the activation-signal mechanism — internal `NativeMethods` + public thin wrapper in `RigToggle.Windows`.

### Deterministic concurrency testing (no Thread.Sleep timing guesses)
**Source:** `src/RigToggle.Tests/ToggleOrchestratorTests.cs` (`ManualResetEventSlim` + bounded-wait-with-message pattern throughout)
**Apply to:** All new `SingleInstanceGuardTests` — including the rapid-relaunch (D-05/Pitfall 8) and bypass-simulation tests, which must avoid flaky fixed sleeps despite spawning real processes.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| Real child-process-spawning test infrastructure (`Process.Start` + `WaitForExit` harness for D-05/D-06) | test | process-lifecycle | No existing test in the codebase launches a real child process; nearest analog (`ToggleOrchestratorTests`'s `Task.Run` background-thread pattern) only proves in-process concurrency, not cross-process. Planner/implementer should build this net-new following STACK.md/PITFALLS.md guidance, using the bounded-wait discipline documented above. |
| `RegisterWindowMessage`/`PostMessage(HWND_BROADCAST,...)` P/Invoke declarations themselves | utility (P/Invoke) | event-driven | No existing `NativeMethods` entries for these two calls; only `RegisterHotKey`/`UnregisterHotKey` exist as precedent for shape (see `GlobalHotkey.cs` above), not content. |
| Named-`Mutex` cross-process guard construction | service/guard | event-driven | No existing code in the repo uses `System.Threading.Mutex`; `ToggleOrchestrator` is explicitly called out (Pitfall 7) as NOT reusable for this — genuinely new primitive for this codebase, use STACK.md's recommended `Global\RigToggle-{GUID}` naming and finally-around-`Application.Run` release directly. |

## Metadata

**Analog search scope:** `src/RigToggle.App/`, `src/RigToggle.Core/`, `src/RigToggle.Windows/`, `src/RigToggle.Tests/`, `.github/workflows/`
**Files scanned:** `Program.cs`, `StartupArgs.cs`, `StartupArgsTests.cs`, `MainForm.cs`, `ToggleOrchestrator.cs`, `ToggleOrchestratorTests.cs`, `GlobalHotkey.cs`, `build.yml`
**Pattern extraction date:** 2026-08-20
