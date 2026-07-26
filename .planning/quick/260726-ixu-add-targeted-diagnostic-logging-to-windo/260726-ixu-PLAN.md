---
phase: quick-260726-ixu
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/RigToggle.Windows/NativeMethods.cs
  - src/RigToggle.Windows/WindowsAppController.cs
autonomous: true
requirements: []
subsystem: app-control
tags: [win32, p-invoke, diagnostic-logging, minimize, toggle-back]

must_haves:
  truths:
    - "MinimizeIfRunning logs the target window's IsWindowVisible and IsIconic state immediately BEFORE ShowWindow(SW_MINIMIZE)"
    - "MinimizeIfRunning logs the target window's IsWindowVisible and IsIconic state AND the ShowWindow boolean return value immediately AFTER the call"
    - "The diagnostic output flows to %LOCALAPPDATA%\\RigToggle\\debug.log via the existing Trace/Log wiring (unchanged)"
    - "MinimizeIfRunning's actual behavior (unconditional ShowWindow(hWnd, SW_MINIMIZE) on the same FindBestMainWindow target) is byte-for-byte unchanged apart from the added log lines"
  artifacts:
    - path: "src/RigToggle.Windows/NativeMethods.cs"
      provides: "IsWindowVisible and IsIconic P/Invoke declarations re-added"
      contains: "extern bool IsIconic"
    - path: "src/RigToggle.Windows/WindowsAppController.cs"
      provides: "Before/after diagnostic logging around the ShowWindow(SW_MINIMIZE) call in MinimizeIfRunning"
      contains: "IsWindowVisible"
  key_links:
    - from: "src/RigToggle.Windows/WindowsAppController.cs"
      to: "src/RigToggle.Windows/NativeMethods.cs"
      via: "NativeMethods.IsWindowVisible / NativeMethods.IsIconic calls"
      pattern: "NativeMethods\\.(IsWindowVisible|IsIconic)"
---

<objective>
Add purely-additive diagnostic logging to `WindowsAppController.MinimizeIfRunning` to capture the target window's real state immediately before and after its `ShowWindow(hWnd, SW_MINIMIZE)` call, so the next rig test's `debug.log` reveals the true before/after window state for the toggle-back regression reported after quick task 260726-idx.

Purpose: The user rig-tested 260726-idx and reported a NEW regression on the toggle-BACK path (Moza window reappears and becomes un-closeable after toggling back to normal mode). The leading UNCONFIRMED hypothesis is that `ShowWindow(SW_MINIMIZE)` on a currently-hidden (tray-only, `IsWindowVisible == false`) window forces it to become a visible minimized taskbar icon and/or retriggers the H9 inert-close desync. This sandbox has no Windows runtime, so neither branch can be confirmed here. This plan ONLY instruments the code path — it does NOT fix anything.

Output: Two edited source files (P/Invoke re-additions + a handful of log lines). No behavior change, no docs updates.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.planning/quick/260726-idx-redesign-companion-app-launch-focus-mech/260726-idx-SUMMARY.md
@src/RigToggle.Windows/WindowsAppController.cs
@src/RigToggle.Windows/NativeMethods.cs

<interfaces>
<!-- Key contracts the executor needs. Extracted from the codebase — no exploration required. -->

The existing best-effort logging helper in WindowsAppController.cs (do NOT modify it, just call it):
```csharp
private static void Log(string message)  // routes to Trace.WriteLine → debug.log, never throws
```

The existing minimize call site in MinimizeIfRunning (WindowsAppController.cs, ~lines 232-236):
```
IntPtr hWnd = FindBestMainWindow((uint)p.Id);
if (hWnd != IntPtr.Zero)
{
    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE); // best-effort
    break;
}
```

Existing NativeMethods P/Invoke convention (NativeMethods.cs) — bool-returning user32 calls use:
```csharp
[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
```

NOTE on git history: `git show 09c758a^:.../NativeMethods.cs` does NOT actually contain
`IsWindowVisible`/`IsIconic` declarations — verified they were never committed to that file
(only prose comments referencing them were removed; `SetForegroundWindow` was the sole removed
`extern`). Do NOT rely on git to recover the signatures — use the standard Win32 signatures
specified in Task 1 below.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Re-add IsWindowVisible/IsIconic P/Invokes and log before/after the minimize call</name>
  <files>src/RigToggle.Windows/NativeMethods.cs, src/RigToggle.Windows/WindowsAppController.cs</files>
  <action>
Two purely-additive edits. Change NO existing behavior.

**Edit 1 — NativeMethods.cs:** Re-add two standard user32 P/Invoke declarations (their only prior caller, the deleted H9-era `FocusWindow`, was removed in 260726-idx; they are needed again for this new investigation on the `MinimizeIfRunning` path). Place them alongside the other `[return: MarshalAs(UnmanagedType.Bool)]` bool-returning declarations (e.g. near `ShowWindow`). Use exactly these signatures — do NOT reinvent the marshalling:
  - `IsWindowVisible(IntPtr hWnd)` returning `bool`, with `[DllImport("user32.dll")]` and `[return: MarshalAs(UnmanagedType.Bool)]`.
  - `IsIconic(IntPtr hWnd)` returning `bool`, with `[DllImport("user32.dll")]` and `[return: MarshalAs(UnmanagedType.Bool)]`.
Add a brief comment noting these are read-only diagnostic queries used by MinimizeIfRunning's toggle-back investigation (matching the file's existing comment style).

**Edit 2 — WindowsAppController.cs, inside MinimizeIfRunning:** Wrap the existing `NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE)` call (the one guarded by `if (hWnd != IntPtr.Zero)`) with before/after `Log(...)` calls using the existing `Log` helper:
  - IMMEDIATELY BEFORE the ShowWindow call: log the window's pre-minimize state — call `NativeMethods.IsWindowVisible(hWnd)` and `NativeMethods.IsIconic(hWnd)` and emit one line including both values (and the hWnd for correlation), e.g. a message like `MinimizeIfRunning: pre-minimize hWnd=0x..., IsWindowVisible=..., IsIconic=...`.
  - Capture the ShowWindow return value into a local `bool` instead of discarding it (this is the only structural change to the call — it still calls `ShowWindow(hWnd, SW_MINIMIZE)` unconditionally on the same target).
  - IMMEDIATELY AFTER the ShowWindow call: log the post-minimize state — `NativeMethods.IsWindowVisible(hWnd)`, `NativeMethods.IsIconic(hWnd)`, and the captured `ShowWindow` boolean return value, e.g. `MinimizeIfRunning: post-minimize hWnd=0x..., IsWindowVisible=..., IsIconic=..., ShowWindowReturned=...`.
  - Keep the existing `break;` after the call. Keep the `// best-effort` intent.

DO NOT: add any conditional/skip logic (do not skip minimize when already hidden), add retry logic, change the target window selection, alter FindBestMainWindow, or touch Program.cs / the Trace listener wiring. This is diagnostic-only. No speculative fix.
  </action>
  <verify>
    <automated>test $(grep -c "extern bool IsIconic" src/RigToggle.Windows/NativeMethods.cs) -eq 1 && test $(grep -c "extern bool IsWindowVisible" src/RigToggle.Windows/NativeMethods.cs) -eq 1 && grep -q "NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE)" src/RigToggle.Windows/WindowsAppController.cs && test $(grep -cE "NativeMethods\.(IsWindowVisible|IsIconic)\(hWnd\)" src/RigToggle.Windows/WindowsAppController.cs) -ge 4 && echo PASS</automated>
  </verify>
  <done>
Both `IsWindowVisible` and `IsIconic` are declared in NativeMethods.cs with correct DllImport + Bool marshalling. MinimizeIfRunning still calls `ShowWindow(hWnd, SW_MINIMIZE)` unconditionally on the FindBestMainWindow target, now bracketed by a before Log (IsWindowVisible + IsIconic) and an after Log (IsWindowVisible + IsIconic + ShowWindow return value). No conditional/retry/skip logic added. Program.cs untouched.
  </done>
</task>

</tasks>

<verification>
- `dotnet build` is NOT runnable in this sandbox (Linux, Windows-only .NET project — confirmed in 260726-idx SUMMARY). Fall back to grep verification (the `<verify>` block above) and a manual code re-read confirming: (a) the two P/Invokes exist with correct attributes, (b) the ShowWindow call is unchanged apart from capturing its return, (c) exactly two new Log lines bracket it, (d) no new control flow was introduced.
- The user must build and rig-test to capture the actual debug.log output; this plan only instruments the path.
</verification>

<success_criteria>
- NativeMethods.cs exposes `IsWindowVisible` and `IsIconic` with `[DllImport("user32.dll")]` + `[return: MarshalAs(UnmanagedType.Bool)]`.
- MinimizeIfRunning emits a pre- and post-minimize diagnostic line to debug.log via the existing `Log` helper, capturing IsWindowVisible, IsIconic (both), and the ShowWindow return (post).
- MinimizeIfRunning's runtime behavior is otherwise identical to pre-change: same target, same unconditional SW_MINIMIZE call, same break.
- No fix, no defensive code, no Program.cs change, no docs change.
</success_criteria>

<output>
Create `.planning/quick/260726-ixu-add-targeted-diagnostic-logging-to-windo/260726-ixu-SUMMARY.md` when done.
</output>
