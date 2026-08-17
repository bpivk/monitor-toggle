---
phase: 23-manual-light-dark-override
reviewed: 2026-08-17T00:00:00Z
depth: standard
files_reviewed: 10
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MonitorConfirmDialog.cs
  - src/RigToggle.App/Program.cs
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
  - src/RigToggle.App/ThemeApplier.cs
  - src/RigToggle.Core/Models/AppSettings.cs
  - src/RigToggle.Core/OverridableThemeProvider.cs
  - src/RigToggle.Tests/Doubles/InMemoryStores.cs
  - src/RigToggle.Tests/OverridableThemeProviderTests.cs
findings:
  critical: 0
  warning: 3
  info: 1
  total: 4
status: issues_found
---

# Phase 23: Code Review Report

**Reviewed:** 2026-08-17T00:00:00Z
**Depth:** standard
**Files Reviewed:** 10
**Status:** issues_found

## Summary

Phase 23 adds a manual Light/Dark/System theme override: a new `OverridableThemeProvider`
decorator (preview ?? persisted ?? live-OS resolution), a `ThemeOverride` field on
`AppSettings`, a three-radio-button group in `SettingsForm`, and two new
`ThemeApplier` helpers (`ApplyEffectiveColorMode`, `ThemeFormSurface`) that pin the
process-wide `Application.SetColorMode` to the *effective* theme instead of always
following the OS. The core resolver (`OverridableThemeProvider`) is well covered by
its own unit tests, correctly memoizes the persisted override, correctly guards
against an out-of-range enum value via `Enum.IsDefined`, and correctly separates
preview vs. persisted state under a lock while raising events outside it.

The defects found are all in the consumer side (`SettingsForm`), where the same
validation/consistency guarantees the resolver itself provides are not mirrored:
a corrupted/out-of-range persisted `ThemeOverride` value can leave the Settings
dialog's theme radio group with nothing selected (and can round-trip that garbage
value back into `settings.json`), the live-preview call path is the one theme
touchpoint in the codebase that isn't defensively wrapped against a subscriber
exception, and `SettingsForm`'s constructor is inconsistent with
`MonitorConfirmDialog`'s in whether it defensively re-pins the process color mode
at construction time. None of these are crash-on-the-happy-path bugs, which is why
they are filed as warnings rather than blockers, but the first one directly
contradicts a "structural guarantee" the code's own comments assert.

## Warnings

### WR-01: Corrupted/out-of-range `ThemeOverride` leaves all three radios unchecked and can re-persist the garbage value

**File:** `src/RigToggle.App/SettingsForm.cs:375-386`
**Issue:**
`SettingsForm_Load` reads the persisted override directly from `_settingsStore.Load()`
and assigns it to `_pendingThemeOverride` with no validation:

```csharp
_pendingThemeOverride = _settings.ThemeOverride;
_updatingThemeRadiosProgrammatically = true;
try
{
    rdoThemeSystem.Checked = _pendingThemeOverride is null;
    rdoThemeLight.Checked = _pendingThemeOverride == AppTheme.Light;
    rdoThemeDark.Checked = _pendingThemeOverride == AppTheme.Dark;
}
finally { _updatingThemeRadiosProgrammatically = false; }
```

This is a different (and weaker) read path than the one `OverridableThemeProvider`
uses for the exact same field: `OverridableThemeProvider.ReadPersistedOverride()`
guards with `Enum.IsDefined(theme)` and degrades an out-of-range value (e.g. a
hand-edited or corrupted `settings.json` containing `"ThemeOverride": 99`) to
`null`/System — a case the phase's own test suite explicitly covers
(`OverridableThemeProviderTests.CurrentTheme_OutOfRangeOverride_ResolvesToLiveSignal`).
`SettingsForm_Load` has no equivalent guard, so for that same corrupted value:
`_pendingThemeOverride == 99`, and none of `rdoThemeSystem.Checked`,
`rdoThemeLight.Checked`, `rdoThemeDark.Checked` evaluate true — the group renders
with **no option selected**, directly contradicting the `rdoThemeSystem` Designer
comment's stated invariant ("D-07: pre-selected -- the structural guarantee that
the group is never rendered with all three options unselected").

Because the theme field is deliberately excluded from the `btnSaveSettings.Enabled`
validation gate (`ValidateSettingsForm`'s doc comment: "the theme field is never
gated by that validation"), the user can still click Save without touching the
radio group, and `BtnSaveSettings_Click` persists `ThemeOverride = _pendingThemeOverride`
(the same out-of-range `99`) verbatim — the corrupted value survives every
subsequent Settings save indefinitely, even though the live app correctly falls
back to System via the resolver's own validation.

**Fix:** Normalize the same way the resolver does when seeding `_pendingThemeOverride`:
```csharp
_pendingThemeOverride = _settings.ThemeOverride is { } t && Enum.IsDefined(t) ? t : null;
```

### WR-02: Live-preview call is the one theme touchpoint not defensively try/caught

**File:** `src/RigToggle.App/SettingsForm.cs:402-427`
**Issue:** Every other place in this codebase that touches theming is wrapped in a
`try { ... } catch { /* cosmetic-only, must never crash */ }` block — see
`MainForm.OnThemeChanged`, `MonitorConfirmDialog.OnThemeChanged`,
`SettingsForm.OnThemeChanged`, and every method in `ThemeApplier.cs`. This
convention is explicit and repeated throughout (T-12-02: "a theming failure must
never crash the toggle/save flow"). `OnThemeRadioCheckedChanged`, however, calls
`_previewThemeOverride(_pendingThemeOverride);` with no try/catch. `SetPreviewOverride`
synchronously invokes the shared `ThemeChanged` multicast delegate
(`OverridableThemeProvider.SetPreviewOverride`); if any current or future subscriber's
handler throws before entering its own try/catch (or if a new subscriber is added
without one), `MulticastDelegate.Invoke` aborts remaining subscribers and the
exception propagates straight out of the radio button's `CheckedChanged` event and
into the WinForms message loop — the exact failure mode every other theme call site
in this file is written specifically to prevent.
**Fix:** Wrap the call the same way every other theme touchpoint in this codebase does:
```csharp
try
{
    _previewThemeOverride(_pendingThemeOverride);
}
catch
{
    // Cosmetic-only -- a preview failure must never crash Settings (T-12-02).
}
```

### WR-03: `SettingsForm`'s constructor doesn't pin `ApplyEffectiveColorMode` the way `MonitorConfirmDialog`'s does

**File:** `src/RigToggle.App/SettingsForm.cs:94-173` (vs. `src/RigToggle.App/MonitorConfirmDialog.cs:27-63`)
**Issue:** `MonitorConfirmDialog`'s constructor explicitly calls
`ThemeApplier.ApplyEffectiveColorMode(IsDark);` immediately before its DWM-chrome
call, with a comment explaining it "reads the resolver fresh each time it opens, so
the application color mode is pinned here too." `SettingsForm`'s constructor calls
`DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDarkTheme);` at the equivalent
point but has no matching `ApplyEffectiveColorMode` call — that only happens later,
in `SettingsForm_Load` (fired on `Form.Load`, after `InitializeComponent()` has
already created every native control). In the current architecture this is masked
because `MainForm` is always alive and keeps the process-wide color mode
continuously synced via its own `ThemeChanged` subscription, so in practice the
mode is usually already correct by the time `SettingsForm`'s constructor runs.
That reliance is implicit and undocumented in `SettingsForm.cs`, unlike
`MonitorConfirmDialog`, which defends itself independently. This is an
inconsistency between two structurally-parallel forms (both take
`IThemeProvider` and both theme themselves at construction) that a future editor
could easily miss when refactoring `MainForm`'s lifecycle.
**Fix:** Add the same explicit call `SettingsForm`'s constructor makes for DWM
chrome, mirroring `MonitorConfirmDialog`:
```csharp
ThemeApplier.ApplyEffectiveColorMode(IsDarkTheme);
DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDarkTheme);
```

## Info

### IN-01: `_applyThemeOverride()` is invoked twice on every successful Save

**File:** `src/RigToggle.App/SettingsForm.cs:1309, 126-130`
**Issue:** `BtnSaveSettings_Click` calls `_applyThemeOverride()` explicitly right
after persisting settings, and the constructor's `FormClosed` lambda calls it again
unconditionally when the dialog actually closes. The code comment acknowledges this
("After a successful Save this is a no-op") and the double-call is genuinely
harmless (`RefreshOverride()` is idempotent), so this is not a defect — but two
call sites doing the same state transition on the same user action is worth a
one-line note for future maintainers to avoid assuming a single source of truth
for "apply on save."
**Fix:** No action required; consider a one-line comment at the `BtnSaveSettings_Click`
call site cross-referencing the `FormClosed` lambda's own comment, for symmetry.

---

_Reviewed: 2026-08-17T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
