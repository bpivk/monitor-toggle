---
phase: 21-accent-color-reading-live-update
fixed_at: 2026-08-11T10:43:32Z
review_path: .planning/phases/21-accent-color-reading-live-update/21-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 21: Code Review Fix Report

**Fixed at:** 2026-08-11T10:43:32Z
**Source review:** .planning/phases/21-accent-color-reading-live-update/21-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 2 (WR-01, WR-02 — fix_scope: critical_warning; no CR/BL findings existed)
- Fixed: 2
- Skipped: 0

## Fixed Issues

### WR-01: Byte-order-critical accent extraction logic has zero automated test coverage

**Files modified:** `src/RigToggle.Windows/WindowsThemeProvider.cs`, `src/RigToggle.Windows.Tests/WindowsThemeProviderTests.cs`
**Commit:** a990e46
**Applied fix:** Extracted the pure ABGR/ARGB bit-shift/mask arithmetic out of
`ReadAccentColorFromRegistry`/`ReadAccentColorFromDwm` into two new `internal static`
side-effect-free helpers, `FromAbgrRegistryDword(uint)` and `FromArgbDwmDword(uint)`,
matching the fix suggestion in the review exactly (byte order verified by hand against
both the review's suggested code and the surrounding code comments' worked example
before applying). `ReadAccentColorFromRegistry`/`ReadAccentColorFromDwm` now call these
helpers after their I/O succeeds, unchanged otherwise. Added a new test file,
`src/RigToggle.Windows.Tests/WindowsThemeProviderTests.cs` (this project already has an
`InternalsVisibleTo` grant from `RigToggle.Windows`, confirmed by reading
`AssemblyInfo.cs` and the existing `WindowsMonitorControllerTests.cs`, which follows the
identical "unit test pure internal helpers, no live hardware" pattern used here), with
`[Theory]` tests pinning the exact worked example already cited in the code's own
comments (`0xffc77e35` -> `#357EC7` on the registry/ABGR path, `#C77E35` on the DWM/ARGB
path) plus edge cases (`0x00000000`, `0xFFFFFFFF`, and an asymmetric byte pattern).
Verified via `dotnet build src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj`
(build succeeded, 0 warnings/0 errors) — this compiled cleanly on Linux since it's a
pure compile step; `dotnet test` itself cannot run on this non-Windows agent host
because the test host requires the `Microsoft.WindowsDesktop.App` runtime, so the new
assertions were additionally verified by hand-tracing the bit arithmetic against the
worked example before commit.

### WR-02: `Color` equality used for accent-change detection is not RGB-only, so fallback transitions can fire spurious "changed" events

**Files modified:** `src/RigToggle.Windows/WindowsThemeProvider.cs`
**Commit:** bcf47dd
**Applied fix:** Changed the accent-change detection in `OnUserPreferenceChanged` from
`resolvedAccent != _accentColor` (full `Color` struct equality, which includes
`KnownColor`/state flags) to `resolvedAccent.ToArgb() != _accentColor.ToArgb()` (pure
32-bit pixel-value comparison), exactly as suggested in the review's Fix section. This
makes the comparison immune to the `SystemColors.Highlight`-vs-`Color.FromArgb(...)`
state mismatch the review identified, so a transient DWM failure/recovery with a
numerically-matching RGB no longer fires a spurious `AccentColorChanged` event. Added an
inline comment explaining the rationale for future readers. Verified via `dotnet build`
(succeeded, 0 warnings/0 errors) and by re-reading the modified block to confirm the
lock scope and surrounding logic (change detection, previous-value capture, assignment)
are unchanged.

## Skipped Issues

None — both in-scope findings (WR-01, WR-02) were fixed. IN-01 and IN-02 were out of
scope for this run (`fix_scope: critical_warning` excludes Info-tier findings).

---

_Fixed: 2026-08-11T10:43:32Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
