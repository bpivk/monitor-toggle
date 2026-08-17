---
phase: 21-accent-color-reading-live-update
reviewed: 2026-08-11T08:54:06Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/ThemeApplier.cs
  - src/RigToggle.Core/Abstractions/IThemeProvider.cs
  - src/RigToggle.Tests/Doubles/FakeThemeProvider.cs
  - src/RigToggle.Tests/ThemeProviderContractTests.cs
  - src/RigToggle.Windows/NativeMethods.cs
  - src/RigToggle.Windows/WindowsThemeProvider.cs
findings:
  critical: 0
  warning: 2
  info: 2
  total: 4
status: issues_found
---

# Phase 21: Code Review Report

**Reviewed:** 2026-08-11T08:54:06Z
**Depth:** standard
**Files Reviewed:** 7
**Status:** issues_found

## Summary

Reviewed the accent-color-reading-live-update diff (`c4ec6e9..HEAD`) across the seven listed
files. The actual changed surface is small and disciplined: `IThemeProvider` gains an
`AccentColor`/`AccentColorChanged` pair, `WindowsThemeProvider` adds a registry-primary /
DWM-fallback live reader guarded by its own lock, `ThemeApplier`/`MainForm` thread the live
value through the existing two-call-site theming funnel instead of the old hardcoded
light/dark literal, and the test doubles/contract tests were extended in lockstep. I traced
the byte-order arithmetic in both extraction paths by hand against the worked example in the
code's own comments and it is internally consistent (and the team separately confirmed it on
real hardware per `21-03-SUMMARY.md`, so I am not re-flagging the byte-order question itself).

No BLOCKER-level defects found in the reviewed diff. The two WARNING items below are real,
provable risks: the single highest-risk piece of logic this phase introduces (the byte-order
extraction math) has no automated test coverage reachable outside a live Windows registry/DWM
call, and the fallback-vs-primary `Color` comparison used for change detection is not doing
what a casual reader would assume (RGB equality) because `System.Drawing.Color`'s `==`
compares more than the pixel value. Neither causes incorrect end-state behavior today, but
both are exactly the kind of thing a future edit could silently break.

## Warnings

### WR-01: Byte-order-critical accent extraction logic has zero automated test coverage

**File:** `src/RigToggle.Windows/WindowsThemeProvider.cs:137-183`
**Issue:** `ReadAccentColorFromRegistry()` and `ReadAccentColorFromDwm()` are `private static`
methods that mix registry/DWM I/O with the actual bit-shift/mask arithmetic that determines
whether R and B end up swapped. This is documented in this same file's comments as the single
most uncertain piece of this phase — uncertain enough that a dedicated verification plan
(`21-03-PLAN.md`/`21-03-SUMMARY.md`) was spun up solely to have a human manually set the
Windows accent to pure red and pure blue on real hardware and eyeball the result, because
static analysis alone could not resolve the byte-order contradiction between the research doc
and the implementation.

That manual, one-time hardware confirmation is currently the *only* thing guarding this logic.
None of the 82 automated tests (including the two new ones in
`ThemeProviderContractTests.cs`) exercise the real bit arithmetic — `FakeThemeProvider`
bypasses it entirely by taking a `Color` directly. Because the math is entangled with
`Registry.CurrentUser.OpenSubKey(...)` and `NativeMethods.DwmGetColorizationColor(...)`, it
cannot be unit-tested as written; a future refactor of either method (e.g. touching the mask
order while "just" changing the registry key or adding a new fallback tier) has no regression
net and would only be caught by another manual hardware pass, if anyone remembers to run one.

**Fix:** Extract the pure bit-math into small internal, side-effect-free helpers that a unit
test can call directly with a synthetic `uint`, e.g.:
```csharp
internal static Color FromAbgrRegistryDword(uint v) =>
    Color.FromArgb(
        (byte)(v & 0xFF),
        (byte)((v >> 8) & 0xFF),
        (byte)((v >> 16) & 0xFF));

internal static Color FromArgbDwmDword(uint v) =>
    Color.FromArgb(
        (byte)((v >> 16) & 0xFF),
        (byte)((v >> 8) & 0xFF),
        (byte)(v & 0xFF));
```
`ReadAccentColorFromRegistry`/`ReadAccentColorFromDwm` then call these after the I/O succeeds.
Add a couple of `[Theory]` tests asserting e.g. `FromAbgrRegistryDword(0xffc77e35)` equals
`Color.FromArgb(0x35, 0x7e, 0xc7)`, pinning the exact worked example already cited in the
code comment so a future edit that flips a mask fails the build instead of waiting for the
next manual rig pass.

### WR-02: `Color` equality used for accent-change detection is not RGB-only, so fallback transitions can fire spurious "changed" events

**File:** `src/RigToggle.Windows/WindowsThemeProvider.cs:97-112, 168-183`
**Issue:** `OnUserPreferenceChanged` detects an accent change with
`accentChanged = resolvedAccent != _accentColor;`. `ReadAccentColorFromDwm()`'s failure paths
return the literal `SystemColors.Highlight` (a known/system `Color`, i.e.
`Color.FromKnownColor(KnownColor.Highlight)`), while every successful read on both paths
returns a `Color` built via `Color.FromArgb(r, g, b)` (a plain ARGB-state color).
`System.Drawing.Color`'s equality operator compares the full internal state — including
`KnownColor`/state flags, not just the RGB channel bytes — so a `SystemColors.Highlight`
value and an ARGB-constructed value with numerically identical R/G/B will still compare
`!=`. Concretely: if the DWM call transiently fails once (`hr != 0`) and then succeeds on the
next `UserPreferenceChanged` tick with a color whose RGB happens to match the system
highlight color, `AccentColorChanged` fires even though nothing visually changed, and the same
false "changed" transition can occur the other way around when the DWM call starts failing
after previously succeeding. This doesn't produce wrong on-screen color (the fired event just
triggers a redundant, visually-identical repaint), but it silently violates this class's own
documented contract ("`AccentColorChanged` only raise on a genuine flip") and is a subtle trap
for anyone who later assumes `!=` here means "different color to the eye."
**Fix:** Compare by ARGB value explicitly rather than relying on `Color`'s full struct
equality, e.g. `resolvedAccent.ToArgb() != _accentColor.ToArgb()`, which only compares the
32-bit pixel value and is immune to `KnownColor`/state differences between a system-color
fallback and an explicitly-constructed color.

## Info

### IN-01: `CurrentTheme`'s private setter bypasses the lock the accent code introduces as the correct pattern right beside it

**File:** `src/RigToggle.Windows/WindowsThemeProvider.cs:46-50`
**Issue:** `AccentColor`'s getter (`get { lock (_accentLock) { return _accentColor; } }`) and
every accent read/write site added this phase correctly go through `_accentLock`. Immediately
above it, `CurrentTheme`'s private setter (`private set { _currentTheme = value; }`) still
writes the backing field with no lock at all, even though the getter right next to it does
lock. It's currently harmless only because the sole caller (`CurrentTheme = ReadThemeFromRegistry();`
in the constructor) runs before `SystemEvents.UserPreferenceChanged` is subscribed. Not
introduced by this phase, but the newly-added, consistently-locked accent code sitting right
next to it makes the inconsistency more visible and worth cleaning up while the file is
already being touched.
**Fix:** `private set { lock (_themeLock) { _currentTheme = value; } }`, matching the pattern
already used for `_accentColor` throughout this same class.

### IN-02: No contract test asserts `ThemeChanged` and `AccentColorChanged` are independent

**File:** `src/RigToggle.Tests/ThemeProviderContractTests.cs`
**Issue:** The two new/existing tests each check `RaiseThemeChanged`/`RaiseAccentColorChanged`
in isolation, but nothing in this file (or in `FakeThemeProvider`) asserts that raising one
does *not* also raise the other. Since `MainForm` wires both events to the exact same handler
(`OnThemeChanged`), the fake's independence is easy to assume but currently unverified;
a regression that accidentally coupled the two raise calls in `FakeThemeProvider` (e.g. a
future edit that has `RaiseThemeChanged` also fire `AccentColorChanged` "for convenience")
would not be caught by this suite.
**Fix:** Add a small assertion, e.g. subscribe a counter to `AccentColorChanged` before calling
`RaiseThemeChanged` and assert it stays `0` (and the symmetric case), documenting the
independence this class's real counterpart (`WindowsThemeProvider`) already relies on.

---

_Reviewed: 2026-08-11T08:54:06Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
