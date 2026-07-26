---
phase: quick-260726-j9y
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/RigToggle.Windows/WindowsAppController.cs
  - .planning/STATE.md
  - .planning/debug/knowledge-base.md
autonomous: true
requirements: [APP-02]

must_haves:
  truths:
    - "When Moza's window is already hidden/tray-only (IsWindowVisible=false) before toggle-back, MinimizeIfRunning does NOT call ShowWindow(SW_MINIMIZE) on it"
    - "When Moza's window is visible before toggle-back, MinimizeIfRunning still calls ShowWindow(SW_MINIMIZE) exactly as before (window minimizes as designed)"
    - "The pre-minimize diagnostic log line is still emitted on every pass, showing which branch was taken"
    - "The post-minimize log line (with post state + ShowWindowReturned) is emitted ONLY when ShowWindow actually ran; a distinct skip line is emitted when the call was skipped"
    - "MinimizeIfRunning still breaks after handling the first matched process with a real window, regardless of branch"
    - "STATE.md and knowledge-base.md reflect: rig-mode direction fixed by 260726-idx, toggle-back direction fixed here (260726-j9y), this round's fix not yet rig-verified"
  artifacts:
    - path: "src/RigToggle.Windows/WindowsAppController.cs"
      provides: "MinimizeIfRunning with conditional (preVisible-gated) ShowWindow call"
      contains: "if (preVisible)"
  key_links:
    - from: "MinimizeIfRunning"
      to: "NativeMethods.ShowWindow"
      via: "conditional call gated on preVisible"
      pattern: "if \\(preVisible\\)"
---

<objective>
Fix the confirmed toggle-back regression: `MinimizeIfRunning` unconditionally calls
`ShowWindow(hWnd, SW_MINIMIZE)` even when the target window is already hidden/tray-only.
Rig-captured `debug.log` evidence proved this forces an already-hidden window
(IsWindowVisible=False, IsIconic=False) back to a visible minimized taskbar icon
(both flip to True), which is the direct cause of "the Moza window is suddenly open"
after toggling back to normal mode — and the likely retrigger of the H9 close-inert symptom.

Fix: only call `ShowWindow(SW_MINIMIZE)` when the window is currently visible
(`preVisible == true`). When already hidden, skip the call (an already-hidden window is
already out of the way for toggle-back). Then update docs to reflect both toggle directions
now have applied fixes, with this round's fix pending rig verification.

Purpose: Stop RigToggle from resurrecting a tray-hidden Moza window on toggle-back.
Output: One conditional change in one method, log-line adjustments, and a docs update.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.planning/debug/knowledge-base.md
@src/RigToggle.Windows/WindowsAppController.cs

<interfaces>
<!-- Already declared in NativeMethods.cs (from 260726-ixu) — reuse, do NOT modify NativeMethods.cs -->
From src/RigToggle.Windows/NativeMethods.cs:
- IsWindowVisible(IntPtr hWnd) -> bool
- IsIconic(IntPtr hWnd) -> bool
- ShowWindow(IntPtr hWnd, int nCmdShow) -> bool
- const int SW_MINIMIZE

Current MinimizeIfRunning inner block (WindowsAppController.cs, inside `if (hWnd != IntPtr.Zero)`):
- bool preVisible = NativeMethods.IsWindowVisible(hWnd);
- bool preIconic  = NativeMethods.IsIconic(hWnd);
- Log($"MinimizeIfRunning: pre-minimize hWnd=0x{hWnd:X}, IsWindowVisible={preVisible}, IsIconic={preIconic}");
- bool showWindowReturned = NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
- bool postVisible = NativeMethods.IsWindowVisible(hWnd);
- bool postIconic  = NativeMethods.IsIconic(hWnd);
- Log($"MinimizeIfRunning: post-minimize hWnd=0x{hWnd:X}, IsWindowVisible={postVisible}, IsIconic={postIconic}, ShowWindowReturned={showWindowReturned}");
- break;
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Gate the minimize call on preVisible in MinimizeIfRunning</name>
  <files>src/RigToggle.Windows/WindowsAppController.cs</files>
  <action>
In `MinimizeIfRunning`, inside the existing `if (hWnd != IntPtr.Zero)` block, keep the
pre-minimize state capture and its log line exactly as-is (still compute `preVisible` and
`preIconic`, still Log the "pre-minimize ..." line on every pass — this stays unconditional
so the log shows which branch was taken).

Then replace the unconditional `ShowWindow` + post-state + post-log with a conditional:

- `if (preVisible)` branch (window currently visible/on-screen — the normal case where the
  user left Moza's dashboard open and toggle-back should minimize it, as originally designed):
  call `NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE)` capturing the return into
  `showWindowReturned`, then compute `postVisible`/`postIconic`, then Log the existing
  "post-minimize ..." line (with postVisible, postIconic, ShowWindowReturned). Move the
  declarations of `showWindowReturned`, `postVisible`, `postIconic` inside this branch so they
  are only computed/logged when the call actually ran — do NOT log stale post values on the
  skip path.
- `else` branch (preVisible == false — window already hidden/tray-only, the confirmed
  problem case): do NOT call ShowWindow at all. Log a distinct short line noting the minimize
  call was skipped because the window was already hidden (e.g.
  "MinimizeIfRunning: skipped minimize hWnd=0x{hWnd:X} — window already hidden (IsWindowVisible=false)").

Keep the `break;` AFTER/OUTSIDE the if/else so it runs unconditionally (still stop after the
first matched process with a real window, regardless of which branch was taken). This is the
evidence-backed fix for the toggle-back regression (rig log: pre IsWindowVisible=False forced
to post True by the unconditional SW_MINIMIZE — .planning/quick/260726-ixu SUMMARY).

Do NOT change `FindBestMainWindow`, `IsRunning`, `LaunchOrFocus`, or anything else in this
file. Do NOT touch NativeMethods.cs (all needed P/Invokes already declared there). Do NOT
touch Program.cs.
  </action>
  <verify>
    <automated>cd /home/bpivk/moza && grep -q 'if (preVisible)' src/RigToggle.Windows/WindowsAppController.cs && grep -q 'skipped minimize' src/RigToggle.Windows/WindowsAppController.cs && [ "$(grep -c 'NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE)' src/RigToggle.Windows/WindowsAppController.cs)" = "1" ] && echo PASS</automated>
    <human-check>No .NET SDK / Windows runtime in this Linux sandbox, so `dotnet build` cannot run here. After the automated grep gate passes, manually re-read MinimizeIfRunning to confirm: (a) pre-minimize Log line is still unconditional; (b) ShowWindow + post-log live only inside `if (preVisible)`; (c) the else branch logs the skip line and makes NO ShowWindow call; (d) `break;` is outside the if/else. Then build + rig-test per the SUMMARY's rig-test checklist.</human-check>
  </verify>
  <done>MinimizeIfRunning calls ShowWindow(SW_MINIMIZE) only when preVisible is true; when preVisible is false it logs a distinct skip line and makes no ShowWindow call; the pre-minimize log is unconditional; break remains unconditional. NativeMethods.cs, Program.cs, and the other methods are unchanged.</done>
</task>

<task type="auto">
  <name>Task 2: Update STATE.md and knowledge-base.md to reflect the two-direction H9 status</name>
  <files>.planning/STATE.md, .planning/debug/knowledge-base.md</files>
  <action>
Update the H9 close-button framing in both docs from 260726-idx's "believed resolved pending
rig verification" to the now-more-precise two-direction status:

(a) Toggle-TO-rig-mode direction: fixed by 260726-idx's relaunch (ShellExecute) redesign —
    RigToggle no longer foregrounds a window it doesn't own on that path.
(b) Toggle-TO-normal-mode direction: had a SEPARATE, now-confirmed-and-fixed bug —
    `MinimizeIfRunning` was doing an unconditional raw Win32 `ShowWindow(SW_MINIMIZE)` that,
    when the window was already hidden/tray-only, forced it back to a visible minimized state
    (rig log evidence: pre IsWindowVisible=False → post True) and retriggered the close-inert
    symptom. Fixed this round (260726-j9y) by skipping that call when the window is already
    hidden.
(c) Current overall H9 status: fix applied for BOTH directions, but THIS round's specific fix
    (skipping minimize on an already-hidden window) has NOT itself been rig-tested. The user
    still needs to verify: toggle to rig mode → close Moza to tray via its X button → toggle
    back to normal mode → confirm the window STAYS hidden (does not reappear) and no
    close-inert symptom occurs. Keep the wording honest about fixed-and-verified vs.
    fixed-but-pending-this-round's-verification.

In STATE.md:
- Update the "Known Limitations" H9 entry with the (a)/(b)/(c) framing above.
- Update the "Pending Todos" entry: 260726-ixu's diagnostic evidence is now captured and a fix
  has been applied — replace the "no fix applied yet, gather evidence" todo with the new
  rig-verify-this-round's-fix todo.
- Add a row to "Quick Tasks Completed" for 260726-j9y (Description: skip ShowWindow(SW_MINIMIZE)
  in MinimizeIfRunning when the window is already hidden — fixes toggle-back regression;
  Date 2026-07-26; Directory link to
  ./quick/260726-j9y-fix-minimizeifrunning-to-skip-showwindow/). Leave the Commit cell as a
  placeholder to be filled at commit time (or the closing metadata commit hash).
- Update `last_activity` / `last_updated` frontmatter to note 260726-j9y.

In .planning/debug/knowledge-base.md:
- Update the `moza-foreground-focus` entry's known-limitation paragraph with the same (a)/(b)/(c)
  two-direction framing.

Cross-reference all three quick task directories in both docs: 260726-idx (rig-mode fix),
260726-ixu (diagnostic evidence), 260726-j9y (toggle-back fix, this task).
  </action>
  <verify>
    <automated>cd /home/bpivk/moza && grep -q '260726-j9y' .planning/STATE.md && grep -q '260726-j9y' .planning/debug/knowledge-base.md && grep -q '260726-ixu' .planning/debug/knowledge-base.md && grep -q 'already hidden' .planning/STATE.md && echo PASS</automated>
  </verify>
  <done>Both docs describe the two-direction H9 status: rig-mode fixed by 260726-idx, toggle-back fixed by 260726-j9y (skip minimize when already hidden), this round's fix pending rig verification. All three quick task dirs (idx/ixu/j9y) are cross-referenced. STATE.md has a 260726-j9y Quick Tasks row and an updated pending todo.</done>
</task>

</tasks>

<verification>
- No .NET SDK / Windows runtime exists in this Linux sandbox (confirmed in 260726-idx and
  260726-ixu SUMMARYs), so `dotnet build` cannot be run here. Verification is grep-based plus
  a manual re-read of MinimizeIfRunning. The user must build and rig-test the fix.
- Rig-test (user, post-merge): toggle to rig mode → close Moza to tray via its X button →
  toggle back to normal mode → confirm the Moza window STAYS hidden (does not reappear as a
  minimized taskbar icon) and no close-inert symptom occurs. Read back
  %LOCALAPPDATA%\RigToggle\debug.log and confirm the "skipped minimize ... already hidden"
  line appears for that run.
</verification>

<success_criteria>
- ShowWindow(SW_MINIMIZE) is called only when the target window is currently visible.
- An already-hidden window is left untouched and a distinct skip line is logged.
- Visible-window minimize behavior is unchanged (still minimizes, still logs post state).
- break remains unconditional; FindBestMainWindow/IsRunning/LaunchOrFocus/NativeMethods.cs/
  Program.cs are untouched.
- STATE.md and knowledge-base.md honestly reflect the two-direction H9 status with all three
  quick tasks cross-referenced.
</success_criteria>

<output>
Create `.planning/quick/260726-j9y-fix-minimizeifrunning-to-skip-showwindow/260726-j9y-SUMMARY.md` when done
</output>
