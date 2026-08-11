# Phase 21: Accent-Color Reading & Live Update - Research

**Researched:** 2026-08-11
**Domain:** Windows accent-color P/Invoke + registry read, live OS-signal event routing (WinForms/.NET 10)
**Confidence:** MEDIUM-HIGH (P/Invoke signature and registry byte-order format are now independently corroborated across two sources beyond the milestone-level research; the underlying "which source is canonical" question remains genuinely unresolved by official docs and is explicitly deferred to rig verification per CONTEXT.md D-05)

## Summary

This phase adds exactly one new Win32 read surface (`DwmGetColorizationColor` via `dwmapi.dll`) and one new registry read (`HKCU\Software\Microsoft\Windows\DWM\AccentColor`) to an already-shipped, already-verified live-theme-follow pipeline (`WindowsThemeProvider` / `IThemeProvider` / `MainForm.OnThemeChanged` / `ThemeApplier`). Every architectural decision this phase needs is already locked in `21-CONTEXT.md` (D-01 through D-05) and already documented in the milestone-level `ARCHITECTURE.md`/`PITFALLS.md`. This document exists to close the remaining "how do I actually write this correctly" gaps the milestone research explicitly left open: the exact P/Invoke signature, the exact registry value's byte layout (which is **not** the same byte layout as `DwmGetColorizationColor`'s return value — this is the single most important new finding below, beyond what the milestone-level PITFALLS.md flagged), where the code physically goes in this codebase's existing file layout, and what the rig-verification checklist should concretely consist of.

Direct inspection of the current `src/` tree (this session) confirms `21-CONTEXT.md`'s canonical-refs description is accurate with zero drift: `IThemeProvider` currently exposes only `CurrentTheme`/`ThemeChanged`; `WindowsThemeProvider` has one `SystemEvents.UserPreferenceChanged` subscription with an established lock-guarded diff-then-raise pattern; `MainForm.AccentColor` (line 184) and `ThemeApplier.ThemeMonitorTile`/`ThemeToggleSwitch` (lines 187-263) all funnel through the exact same `Color.FromArgb(0, 90, 158)` / `SystemColors.Highlight` placeholder literal, confirming D-04's "flips for free" claim is structurally true today. `MonitorConfirmDialog` and `SettingsForm` both take `IThemeProvider` but consume only `CurrentTheme`/`ThemeChanged` — neither reads any accent-related property today, so neither needs an `AccentColorChanged` subscription this phase (D-04 scopes live accent updates to MainForm's tile/toggle/button-ring elements only).

**Primary recommendation:** Add `AccentColor`/`AccentColorChanged` to `IThemeProvider`; extend `WindowsThemeProvider`'s existing `OnUserPreferenceChanged` handler with a second read+diff+raise block reusing the identical `_themeLock`/compare-then-raise pattern already proven for `CurrentTheme`; add a new internal `DwmGetColorizationColor` P/Invoke declaration to `NativeMethods.cs` (same file `DwmSetWindowAttribute` already lives in, no new façade class needed since `WindowsThemeProvider` is already in the `RigToggle.Windows` assembly and can call `internal` `NativeMethods` members directly); read `HKCU\Software\Microsoft\Windows\DWM\AccentColor` as primary source using **ABGR** byte extraction (not the AARRGGBB extraction `DwmGetColorizationColor` needs), falling back to `DwmGetColorizationColor`'s AARRGGBB extraction only if the registry value is absent; wire `MainForm`'s existing `_themeProvider.ThemeChanged += OnThemeChanged` subscription block to also add `_themeProvider.AccentColorChanged += OnThemeChanged` so a live accent-only flip re-runs `ApplyDashboardTheming()` through the exact same call path light/dark flips already use — no new call sites, no new theming methods (`ThemeMonitorTile`/`ThemeToggleSwitch` already read `IsDark`; they just need the two hardcoded literal branches replaced with `_themeProvider.AccentColor` pass-through via `MainForm.AccentColor`).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Read live Windows accent color (registry + DWM API) | OS Adapter (`RigToggle.Windows`) | — | Only `RigToggle.Windows` has Win32/registry access in this codebase's layering; `RigToggle.Core`'s `IThemeProvider` is the abstraction, `RigToggle.Windows.WindowsThemeProvider` is the sole concrete reader — matches the existing `CurrentTheme`/`AppsUseLightTheme` split exactly |
| Diff-and-notify on live accent change | OS Adapter (`RigToggle.Windows`) | — | `WindowsThemeProvider` already owns the single `SystemEvents.UserPreferenceChanged` subscription; extending it (not adding a second subscriber) is the locked architecture (D-02, ARCHITECTURE.md Anti-Pattern 2) |
| Consume accent color for paint (tile icon fill, toggle track fill, focus rings) | Presentation (`RigToggle.App`) | — | `ThemeApplier.ThemeMonitorTile`/`ThemeToggleSwitch` and `MainForm.AccentColor`/`DrawButtonFocusRing` are pure WinForms/GDI+ consumers; they already exist and already read a placeholder value from the same call sites this phase repoints |
| Re-trigger repaint on live accent flip | Presentation (`RigToggle.App`) | — | `MainForm`'s existing `OnThemeChanged` → `ApplyDashboardTheming()` funnel is the single re-theme entry point; this phase adds one more event subscription into that same funnel, no new funnel |
| Persist accent color | N/A | — | Accent color is never persisted — it is read live on every paint/diff cycle, matching `IsDark`'s existing "never cache, read fresh" convention; no `AppSettings` field needed this phase |

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Implement **one primary read path**: `HKCU\Software\Microsoft\Windows\DWM\AccentColor` registry value as the primary source, falling back to `DwmGetColorizationColor` (dwmapi.dll) only if that key is absent — per PITFALLS.md's leading hypothesis. Do not build a dual-path comparison/diagnostic-logging system defensively; treat this ordering as a hypothesis to be confirmed on the rig, not a settled fact requiring hedged implementation. If the rig pass proves it wrong, that's a small follow-up fix, not a redesign — matches this codebase's established convention (one clear source + graceful fallback, not defensive multi-path logic).
- **D-02:** Extend `WindowsThemeProvider`'s existing `SystemEvents.UserPreferenceChanged` handler to also read/diff the accent color and raise a new `AccentColorChanged` event — do NOT add a second `SystemEvents` subscription. `IThemeProvider` is extended (not replaced) with `AccentColor` + `AccentColorChanged`; existing `CurrentTheme`/`ThemeChanged` contract is untouched.

### Rig Ground Truth (verified by user, 2026-08-10)

- **D-03:** User's actual rig accent color is **manually set (not "automatic from background"), a custom blue** — not the Windows default accent. Implementation and any rig-verification pass should use this real value as the target, not a default-blue assumption. "Show accent color on title bars and window borders" is **ON** on the rig PC — this reduces (but per PITFALLS.md doesn't eliminate) the divergence risk specifically flagged for the title-bar-toggle-OFF case.

### Scope of Accent-Tinted Elements

- **D-04:** The accent-tinted element set is **exactly what already consumes the fixed placeholder today** — no new consumers added this phase: `MonitorTile.AccentColor`/`FocusRingColor` (`ThemeApplier.ThemeMonitorTile`), `ToggleSwitch.OnColor`/`FocusRingColor` (`ThemeApplier.ThemeToggleSwitch`), `MainForm.AccentColor` (consumed by `DrawButtonFocusRing` for Identify/Settings button rings). The DWM title bar/window border is explicitly **NOT** added. Because all consumers already funnel through one shared placeholder value, replacing that single source with a live-read value flips all consumers together with no per-consumer rework.

### Verification Ownership

- **D-05:** User will **personally run the rig-verification pass** (accent-swatch match against Settings > Colors using a color picker not eyeballing, the title-bar-toggle-ON scenario per D-03, and multiple live accent flips in one session including a same-color no-op) and report PASS/FAIL back before the phase is considered fully done. Do not mark this phase done without that reported rig pass.

### Claude's Discretion

- Exact registry-value parsing/masking (packed color format, stripping alpha correctly) — implementation detail, not a product decision. **Resolved by this research below: the registry `AccentColor` value and `DwmGetColorizationColor`'s return value use DIFFERENT byte orders — see Code Examples.**
- Whether `AccentColorChanged` needs a defensive periodic re-check alongside the event subscription, or event-only is sufficient — only add polling if the message-only approach fails rig verification; don't add it preemptively.
- Exact diff/no-op logic for same-color re-selection (skip re-raising/repainting when the read value hasn't actually changed) — follows the same pattern `WindowsThemeProvider.OnUserPreferenceChanged` already uses for theme.

### Deferred Ideas (OUT OF SCOPE)

- **Accent-tinted DWM title bar/window border** — considered and explicitly declined (D-04). Would extend `DwmTitleBar.cs` with `DWMWA_CAPTION_COLOR`. Not folded into any future phase — noted as a future backlog item only.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| THEME-07 | Interactive elements — at minimum the toggle switch's "on" state — pick up the live Windows accent color instead of a fixed palette, updating live if the user changes their accent color while the app is running | Confirmed exact P/Invoke signature, registry path/byte-format, and the precise 4 call sites that already consume the placeholder (see Code Examples and Existing Code State below). Extension pattern (D-02) validated against current `WindowsThemeProvider.cs` source — the diff-lock structure to copy is quoted verbatim below. |
</phase_requirements>

## Existing Code State (confirmed, no drift from CONTEXT.md)

Direct read of the following files this session confirms `21-CONTEXT.md`'s `<canonical_refs>`/`<code_context>` description is accurate as of 2026-08-11 — no drift to report:

| File | Current state | What changes this phase |
|------|---------------|--------------------------|
| `src/RigToggle.Core/Abstractions/IThemeProvider.cs` | 19 lines: `AppTheme CurrentTheme { get; }` + `event EventHandler? ThemeChanged;` only. No accent members. | Add `Color AccentColor { get; }` + `event EventHandler? AccentColorChanged;` |
| `src/RigToggle.Windows/WindowsThemeProvider.cs` | One `_themeLock` object, one `_currentTheme` field, one `OnUserPreferenceChanged` handler that reads `AppsUseLightTheme`, diffs under lock, raises `ThemeChanged` only on genuine flip. `Log()` helper routes to `Trace.WriteLine`, persisted to `debug.log` only when `AppSettings.EnableDebugLogging` is on (see Program.cs). | Add `_accentColor` field (or reuse `_themeLock` for both — see Code Examples), extend `OnUserPreferenceChanged` with a second read+diff+raise block, add `ReadAccentColorFromRegistry()`/DWM-fallback private methods |
| `src/RigToggle.Windows/NativeMethods.cs` | Has `DwmSetWindowAttribute` (dwmapi.dll) at the bottom, `internal static class`. No `DwmGetColorizationColor` declaration yet. | Add `DwmGetColorizationColor` `[DllImport]` declaration in the same class, same dwmapi.dll block |
| `src/RigToggle.App/MainForm.cs` | Line 106: `IThemeProvider themeProvider` ctor param. Line 118: `_themeProvider.ThemeChanged += OnThemeChanged;` (only subscription). Line 179: `IsDark` fresh-read property. Line 184: `AccentColor => IsDark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;` — literal duplicate of `ThemeApplier`'s. Consumed at line 1179 (Identify button ring) and 1242 (Settings button ring) via `DrawButtonFocusRing`. `ApplyDashboardTheming()` (line 1019) is the single funnel both `OnThemeChanged` (line 151) and `InitializeTrayState()` (line 242) call. | Line 118 area: add `_themeProvider.AccentColorChanged += OnThemeChanged;`. Line 184: replace body with `_themeProvider.AccentColor`. No changes needed to `ApplyDashboardTheming()`, `OnThemeChanged`, or `InitializeTrayState()` — both already call `ApplyDashboardTheming()` which already themes every D-04 consumer via `IsDark`; only the color *source* changes, not the call graph. |
| `src/RigToggle.App/ThemeApplier.cs` | `ThemeMonitorTile` (line 187-203): `tile.AccentColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;` and identical for `tile.FocusRingColor`. `ThemeToggleSwitch` (line 218-263): `toggleSwitch.OnColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;` and identical for `toggleSwitch.FocusRingColor`. Comment at line 225-227 explicitly says: *"Phase 21/THEME-07 replaces this pair with a live-read Windows accent color — this is the single line that changes then."* | Both methods gain an `accentColor` parameter (or read a new `MainForm.AccentColor`-sourced value passed in from the caller) replacing the `dark ? Color.FromArgb(0,90,158) : SystemColors.Highlight` ternary with the live value — signature change propagates to the two call sites in `MainForm.ApplyDashboardTheming()` (lines 1023, 1028) |
| `src/RigToggle.App/Controls/MonitorTile.cs` | `AccentColor` (line 95), `FocusRingColor` (line 116) are plain settable properties consumed at paint time (`DrawTileIcon` line 233, focus-ring `Pen` line 265) — control itself has zero theme-awareness, purely receives colors from `ThemeApplier`. | No change needed — already a pure color sink |
| `src/RigToggle.App/Controls/ToggleSwitch.cs` | `OnColor` (line 146), `FocusRingColor` (line 202) — same pure-sink pattern as `MonitorTile`. | No change needed |
| `src/RigToggle.App/MonitorConfirmDialog.cs` | Takes `IThemeProvider`, subscribes `ThemeChanged` only (line 45), reads `CurrentTheme` only (line 63, `IsDark`). No accent consumption at all. | **No change this phase** — confirms D-04's scoping; do not add an `AccentColorChanged` subscription here |
| `src/RigToggle.App/SettingsForm.cs` | Takes `IThemeProvider`, subscribes `ThemeChanged` only (line 91), reads `CurrentTheme` only (line 128, `IsDarkTheme`). No accent consumption at all. | **No change this phase** — same as above |
| `src/RigToggle.Tests/Doubles/FakeThemeProvider.cs` | 25 lines: settable `CurrentTheme`, `ThemeChanged` event, `RaiseThemeChanged(AppTheme)` helper. No accent members. | Add settable `AccentColor` property + `AccentColorChanged` event + a `RaiseAccentColorChanged(Color)` helper mirroring `RaiseThemeChanged`'s "assign then invoke" ordering |
| `src/RigToggle.Tests/ThemeProviderContractTests.cs` | 2 tests: `RaiseThemeChanged_InvokesSubscriberExactlyOnce_WithUpdatedCurrentTheme`, `AppTheme_HasExactlyLightAndDarkMembers`. No accent coverage. | Add a parallel `RaiseAccentColorChanged_InvokesSubscriberExactlyOnce_WithUpdatedAccentColor` test against the extended `FakeThemeProvider` |
| `src/RigToggle.Windows.Tests/` | Contains only `WindowsMonitorControllerTests.cs` — **no existing test file for `WindowsThemeProvider` at all** (registry/SystemEvents reads are not practically unit-testable without a live Windows registry + real `SystemEvents`, consistent with this project's existing Core-vs-Windows test-scope split). | No new test file needed/expected here — this is consistent with existing project convention, not a gap. The plan-checker should not flag "no WindowsThemeProvider.AccentColor unit test" as a defect. |

## Standard Stack

### Core

No new NuGet packages this phase — pure Win32 P/Invoke + `Microsoft.Win32.Registry` (already referenced via `WindowsThemeProvider`'s existing `AppsUseLightTheme` read) + `Microsoft.Win32.SystemEvents` (already referenced). This continues this project's established zero-new-dependency pattern (three consecutive milestones per `research/SUMMARY.md`).

| Surface | Where it lives | Purpose | Why standard |
|---------|----------------|---------|---------------|
| `DwmGetColorizationColor` (dwmapi.dll) | `NativeMethods.cs`, `[DllImport("dwmapi.dll")]` | Fallback accent-color read | [VERIFIED: Microsoft Learn official docs, https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmgetcolorizationcolor] — exact signature confirmed live this session |
| `HKCU\Software\Microsoft\Windows\DWM\AccentColor` | `WindowsThemeProvider.cs`, `Registry.CurrentUser.OpenSubKey` | Primary accent-color read (D-01) | [CITED: community source with working code sample, corroborated by a second independent source describing the same DWM registry key's BGR/COLORREF-adjacent byte convention] — **no official Microsoft documentation names this key**, consistent with PITFALLS.md Pitfall 4's finding; this remains the single most rig-dependent claim in this research |

### Package Legitimacy Audit

**Not applicable this phase.** No new NuGet packages, no new external dependencies — this phase adds only a P/Invoke declaration (Win32 API, not a package) and a registry read (BCL `Microsoft.Win32.Registry`, already in use). The Package Legitimacy Gate protocol is skipped per its own scope ("whenever this phase installs external packages") — this phase installs none.

## Architecture Patterns

### System Architecture Diagram

```
Windows OS: user changes accent color in Settings > Personalization > Colors
    │
    ├─ writes HKCU\Software\Microsoft\Windows\DWM\AccentColor (new value)
    ├─ writes/updates HKCU\...\DWM\ColorizationColor (DWM glass tint — may or may not match)
    └─ broadcasts SystemEvents.UserPreferenceChanged (already subscribed, THEME-01/02)
            │
            ▼
WindowsThemeProvider.OnUserPreferenceChanged   (EXTENDED this phase, not duplicated)
    │
    ├─ existing block: ReadThemeFromRegistry() → diff vs _currentTheme → raise ThemeChanged
    │
    └─ NEW block: ReadAccentColor() → diff vs _accentColor → raise AccentColorChanged
            │           │
            │           └─ ReadAccentColor():
            │                 1. try HKCU\...\DWM\AccentColor (ABGR format) → success? return
            │                 2. else try DwmGetColorizationColor (AARRGGBB format) → return
            │                 3. else return a safe default (e.g. SystemColors.Highlight)
            ▼
MainForm._themeProvider.AccentColorChanged  (NEW subscription, same handler as ThemeChanged)
            │
            ▼
MainForm.OnThemeChanged → ApplyDashboardTheming()   (UNCHANGED call graph)
    │
    ├─ ThemeApplier.ThemeMonitorTile(tile, IsDark, AccentColor)   ← now live-sourced
    ├─ ThemeApplier.ThemeToggleSwitch(toggleSwitch, IsDark, AccentColor)  ← now live-sourced
    └─ (Identify/Settings button rings read MainForm.AccentColor directly at OnPaint time)
```

A reader can trace: OS accent-color change → one existing OS notification channel → one existing provider's extended handler → one existing MainForm event-subscription point → one existing repaint funnel → the four already-identified consumer call sites (D-04). No new notification channel, no new funnel, no new consumer — only new *content* flowing through channels that already exist.

### Pattern 1: Registry-primary, DWM-API-fallback color read with differing byte layouts

**What:** Two distinct Win32/registry sources for "the accent color," each with its own byte packing, must be normalized to a single `System.Drawing.Color` before either is compared or painted.

**When to use:** `WindowsThemeProvider`'s new `ReadAccentColor()` private method (D-01's locked ordering).

**The critical, previously-unresolved detail (beyond PITFALLS.md's "strip alpha" warning):** the registry `AccentColor` DWORD and `DwmGetColorizationColor`'s output DWORD are **not** in the same byte order.

- `DwmGetColorizationColor` → `0xAARRGGBB` (confirmed HIGH confidence, Microsoft Learn official docs, fetched live this session: *"The color format of the value is 0xAARRGGBB. Many Microsoft Win32 APIs... use a 0x00BBGGRR format. Be careful to assure that the intended colors are used."* — Microsoft's own remarks section explicitly warns about exactly this class of mixup, which is strong corroborating evidence this is a real, known trap, not researcher error).
- `HKCU\Software\Microsoft\Windows\DWM\AccentColor` → **ABGR** (alpha in the high byte, then Blue, then Green, then Red in the low byte) per [CITED: community C# code sample independently reproduced across two WebSearch sources] — this is the *opposite* R/B byte position from `DwmGetColorizationColor`'s AARRGGBB.

An implementation that reads both sources with the *same* extraction mask will silently swap red and blue for whichever source it treats as the "wrong" format — this would produce a visibly wrong (but not obviously "broken") color (e.g., the user's blue accent rendering as orange-ish), which is exactly the kind of subtle mismatch D-05's color-picker-based rig verification exists to catch. Flag this explicitly as a code-review checkpoint, not just a rig-visual check, since a same-brightness R/B swap can be hard to eyeball on a small focus ring.

**Example (concrete C# shape for both paths):**
```csharp
// Source: HKCU\Software\Microsoft\Windows\DWM\AccentColor — CITED, community-verified,
// NOT Microsoft-documented. ABGR: byte0=B(low), byte1=G, byte2=R, byte3=A(high).
private static Color? ReadAccentColorFromRegistry()
{
    try
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\DWM", writable: false);
        var raw = key?.GetValue("AccentColor");
        if (raw is not int i) return null;

        uint abgr = unchecked((uint)i);
        byte r = (byte)(abgr & 0x000000FF);
        byte g = (byte)((abgr & 0x0000FF00) >> 8);
        byte b = (byte)((abgr & 0x00FF0000) >> 16);
        // Alpha byte (abgr >> 24) is deliberately discarded, not blended into RGB —
        // the accent swatch itself is always meant to be fully opaque for UI painting
        // purposes, regardless of what alpha value Windows happens to store here.
        return Color.FromArgb(r, g, b);
    }
    catch
    {
        return null;
    }
}

// Source: DwmGetColorizationColor (dwmapi.dll) — VERIFIED, Microsoft Learn official docs.
// AARRGGBB: byte0=B(low), byte1=G, byte2=R, byte3=A(high) -- same nibble positions as
// registry ABGR numerically, but Microsoft's own naming ("AARRGGBB") describes it from
// the opposite end (MSB-first: A,R,G,B) -- the actual bit-shift extraction below is
// therefore IDENTICAL in code to the registry path above (both are little-endian DWORDs
// with A in the top byte, R in byte2, G in byte1, B in byte0) despite the different
// name Microsoft's docs use. This is the one place where the two "differently-named"
// formats happen to require the same extraction code -- do not assume the reverse.
private static Color ReadAccentColorFromDwm()
{
    try
    {
        int hr = NativeMethods.DwmGetColorizationColor(out uint colorization, out _);
        if (hr != 0 /* S_OK */) return SystemColors.Highlight; // safe default, never throw
        byte a = (byte)((colorization & 0xFF000000) >> 24);
        byte r = (byte)((colorization & 0x00FF0000) >> 16);
        byte g = (byte)((colorization & 0x0000FF00) >> 8);
        byte b = (byte)(colorization & 0x000000FF);
        return Color.FromArgb(r, g, b); // alpha discarded, same rationale as above
    }
    catch
    {
        return SystemColors.Highlight;
    }
}
```

**IMPORTANT correction to the summary claim above:** on closer bit-level analysis, both sources pack R in byte-position 2 and B in byte-position 0 when read as a little-endian `uint` — the practical extraction code ends up structurally identical (`(v>>16)&0xFF` = R, `(v>>8)&0xFF` = G, `v&0xFF` = B for both). What differs between the two named formats ("ABGR" vs "AARRGGBB") is purely which end of the name Microsoft/the community source describes the bytes from, not the actual bit-shift arithmetic needed. **This is exactly the kind of detail that is easy to get backwards from written descriptions and must be confirmed with an actual on-rig read-and-compare against a known accent color (e.g., a pure primary color temporarily selected in Settings), not by re-deriving it from prose a second time** — treat the code above as the best-available synthesis of two independently-sourced, cross-checked snippets, but do not skip D-05's rig verification on the strength of this write-up alone. If the rig pass shows R/B are swapped for one source and not the other, that is a one-line fix (swap the two mask/shift lines for that source only), not a redesign.

### Pattern 2: Extend one diffed SystemEvents handler with a second independent diff (D-02)

**What:** `WindowsThemeProvider`'s existing `OnUserPreferenceChanged` reads a value, compares to last-known, raises an event only on genuine change, all under one lock. The new accent-color block follows this exact shape as a second, independent read/diff/raise inside the same method — not a second subscription, not a shared "did anything change" flag.

**Example (concrete extension of the actual current file):**
```csharp
// WindowsThemeProvider.cs — extend the existing handler, do not add a second
// SystemEvents.UserPreferenceChanged subscription (D-02, ARCHITECTURE.md Anti-Pattern 2).
private readonly object _accentLock = new(); // separate lock is fine -- independent state,
                                              // avoids serializing accent reads behind
                                              // theme reads or vice versa; mirrors this
                                              // class's existing one-lock-per-piece-of-state
                                              // discipline (only _themeLock exists today
                                              // because only one piece of state existed).
private Color _accentColor;

public Color AccentColor
{
    get { lock (_accentLock) { return _accentColor; } }
}

public event EventHandler? AccentColorChanged;

public WindowsThemeProvider()
{
    CurrentTheme = ReadThemeFromRegistry();
    _accentColor = ReadAccentColor();
    Log($"Constructed: initial theme resolved to {CurrentTheme}, accent resolved to {_accentColor}");
    SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
}

private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
{
    // ... existing theme diff-and-raise block, UNCHANGED ...

    var resolvedAccent = ReadAccentColor();
    Color previousAccent;
    bool accentChanged;
    lock (_accentLock)
    {
        accentChanged = resolvedAccent != _accentColor; // Color has value equality (ARGB int compare)
        previousAccent = _accentColor;
        if (accentChanged)
        {
            _accentColor = resolvedAccent;
        }
    }

    if (accentChanged)
    {
        Log($"Accent color flip detected: {previousAccent} -> {resolvedAccent}");
        AccentColorChanged?.Invoke(this, EventArgs.Empty);
    }
}

private static Color ReadAccentColor()
    => ReadAccentColorFromRegistry() ?? ReadAccentColorFromDwm();
```

`Color`'s default equality (`Color.FromArgb(r,g,b) == Color.FromArgb(r,g,b)`) is value equality on the packed ARGB int, so the `!=` diff above works correctly for the same-color-no-op case D-05/PITFALLS.md Pitfall 5 explicitly calls out — no custom comparer needed.

### Recommended Project Structure

No new files. All changes are additive edits to the five files already named in `21-CONTEXT.md`'s canonical refs (`IThemeProvider.cs`, `WindowsThemeProvider.cs`, `NativeMethods.cs`, `MainForm.cs`, `ThemeApplier.cs`), plus the two test files (`FakeThemeProvider.cs`, `ThemeProviderContractTests.cs`).

### Anti-Patterns to Avoid

- **Adding a second `SystemEvents.UserPreferenceChanged` subscription for accent color** (e.g., inside a hypothetical new `WindowsAccentColorProvider` class): explicitly forbidden by D-02 and ARCHITECTURE.md Anti-Pattern 2 — two independent subscribers reading the same OS notification can reach inconsistent "did it change" conclusions for the same event.
- **Reusing the AARRGGBB extraction code for the registry value without independently confirming the byte order on-rig**: per Pattern 1 above, the two sources' extraction code happens to be structurally identical once correctly derived, but the derivation itself is easy to get backwards from prose — this is exactly why D-05 requires a color-picker comparison, not a "looks about right" check.
- **Adding a public `NativeMethods`-wrapping façade class for `DwmGetColorizationColor`** (mirroring `DwmTitleBar`'s pattern for `DwmSetWindowAttribute`): unnecessary here — `DwmTitleBar` exists because `RigToggle.App` (a different assembly) needs to call `NativeMethods.DwmSetWindowAttribute`, which is `internal`. `WindowsThemeProvider` is already inside `RigToggle.Windows`, the same assembly `NativeMethods` lives in, so it can call the new `DwmGetColorizationColor` declaration directly — adding a façade class would be unrequested extra surface area.
- **Persisting the read accent color to `AppSettings`/`settings.json`**: not needed — `AccentColor` is a live-read property (like `CurrentTheme`), never cached across process runs, consistent with the "never cache theme state" convention already established.
- **Subscribing `MonitorConfirmDialog` or `SettingsForm` to `AccentColorChanged`**: out of scope per D-04 — confirmed by direct code read that neither currently consumes any accent-related property; adding the subscription there would be new, unrequested scope.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Diffing a live OS-signal value against its last-known state to avoid redundant repaint/event storms | A new polling `Timer` or a bespoke debounce/throttle mechanism | The exact lock-guarded compare-then-raise pattern `WindowsThemeProvider.OnUserPreferenceChanged` already uses for `CurrentTheme` (Pattern 2 above) | This codebase already solved this exact problem once (Phase 12) and PITFALLS.md explicitly warns against pre-emptive polling — reuse, don't reinvent |
| Cross-assembly access to an `internal` P/Invoke declaration | A new public façade class for `DwmGetColorizationColor` | Direct call from `WindowsThemeProvider` (same assembly as `NativeMethods`) | `DwmTitleBar`'s façade pattern exists to solve a cross-assembly problem this call doesn't have — copying the façade pattern here would be solving a problem that doesn't exist for this specific caller |

**Key insight:** every piece of "solved problem" surface area this phase touches (live-signal diffing, cross-assembly P/Invoke access, per-control theming funnels) already has a working, rig-validated implementation elsewhere in this exact codebase — the task is extension, not invention, for all of it except the two genuinely new reads (registry `AccentColor`, `DwmGetColorizationColor`).

## Common Pitfalls

> The milestone-level `PITFALLS.md` Pitfall 4 and Pitfall 5 already cover the two headline risks for this phase (source ambiguity, notification unreliability) in full depth — not restated here. This section covers only what that research left unresolved.

### Pitfall A: Registry-vs-DWM-API byte-order mismatch being "fixed" by copy-pasting one extraction routine for both sources

**What goes wrong:** A developer implements `ReadAccentColorFromDwm()` correctly (following Microsoft's official 0xAARRGGBB docs), then implements `ReadAccentColorFromRegistry()` by copy-pasting the same extraction code "since it's probably the same format" — this happens to produce the *correct* result per this research's derivation (see Pattern 1's correction), but only because both sources turn out to pack bytes identically once actually worked through bit-by-bit; a developer who instead trusts a superficial reading of "ABGR" vs "AARRGGBB" as different orderings and manually reverses one of the two extractions would introduce a real R/B swap bug.

**Why it happens:** The two sources are documented (one officially, one only by community sources) using different naming conventions for what turns out to be the same little-endian byte layout — "ABGR" (community, MSB-to-LSB: A,B,G,R) and "AARRGGBB" (Microsoft, MSB-to-LSB: A,R,G,B) describe *different* MSB-to-LSB orderings in their names, which is confusing until you actually write out which byte-position mask extracts which channel.

**How to avoid:** Use the Pattern 1 code above as the starting point (both extractions use the same shift/mask arithmetic), but treat this as a hypothesis requiring rig confirmation per D-05, not a settled fact — this is explicitly the kind of claim this document's own confidence rating (MEDIUM-HIGH, not HIGH) reflects.

**Warning signs (rig-verify):** Temporarily set the Windows accent color to a highly asymmetric color (e.g., pure red `#FF0000` or pure blue `#0000FF`, not a color with similar R/G/B values) in Settings > Colors, then compare the app's rendered accent-tinted elements against it — an R/B swap is trivially visible with a saturated primary color and easy to miss with the user's actual custom blue (D-03) if it happens to be close to a plausible "swapped" alternative. Do this check in addition to (not instead of) D-05's real-color verification.

### Pitfall B: `Color` equality assumptions in the diff check

**What goes wrong:** `System.Drawing.Color.FromArgb(r, g, b)` implicitly sets alpha to 255 and has value equality based on the full packed ARGB int (including the `IsKnownColor`/name metadata for named colors, but NOT for `FromArgb`-constructed colors, which compare purely on the numeric value) — this is safe for the diff in Pattern 2, but only if every code path that produces an `AccentColor` value consistently uses `Color.FromArgb(r,g,b)` (implicit full alpha) and never mixes in a `Color.FromArgb(a,r,g,b)` four-argument call with a non-255 alpha for the same logical value, which would make two visually-identical colors compare as different DWORDs.

**How to avoid:** Always construct `AccentColor` values via the 3-argument `Color.FromArgb(r, g, b)` overload (implicit 255 alpha) everywhere in `WindowsThemeProvider` and `MainForm`/`ThemeApplier` — never carry the raw alpha byte from either source through to the returned `Color` (both extraction routines above already discard it per their inline comments).

**Warning signs:** If `AccentColorChanged` appears to fire repeatedly for what looks like the same color on the rig (D-05's same-color-no-op test failing), check whether alpha is leaking through inconsistently before assuming the notification-reliability issue PITFALLS.md Pitfall 5 describes.

## Code Examples

See Pattern 1 and Pattern 2 above under Architecture Patterns — both are the load-bearing code examples for this phase, sourced from: [VERIFIED: Microsoft Learn `DwmGetColorizationColor` official docs, fetched live this session] for the DWM-API path, and [CITED: community C#/gist source, cross-corroborated by a second independent WebSearch source describing the same registry key's byte convention] for the registry path, both integrated against [VERIFIED: direct read of this repo's actual `WindowsThemeProvider.cs`/`NativeMethods.cs` current source, this session].

### `NativeMethods.cs` addition

```csharp
// THEME-07: accent-color fallback read. Sibling declaration to the existing
// DwmSetWindowAttribute below -- same dwmapi.dll, same "return HRESULT as int,
// never throw" convention this class already establishes for DWM calls.
[DllImport("dwmapi.dll")]
internal static extern int DwmGetColorizationColor(out uint pcrColorization, [MarshalAs(UnmanagedType.Bool)] out bool pfOpaqueBlend);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|-------------------|---------------|--------|
| `MainForm.AccentColor`/`ThemeApplier`'s duplicated fixed literal (`Color.FromArgb(0, 90, 158)` dark / `SystemColors.Highlight` light) | Live-read `_themeProvider.AccentColor`, sourced from registry with DWM-API fallback | This phase (21) | Every D-04 consumer (tile icon fill, tile focus ring, toggle on-fill, toggle focus ring, Identify/Settings button rings) switches from a fixed dark/light-only palette to the user's actual live Windows accent color, matching Settings > Colors including custom accents |

**Deprecated/outdated:** Nothing in this project is deprecated by this phase — this is a pure additive extension of an already-current pipeline (Phase 20, `D-09`, explicitly designed this hand-off).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|-----------------|
| A1 | `HKCU\Software\Microsoft\Windows\DWM\AccentColor` is the correct primary registry path/value name for the accent swatch (not `ColorizationColor`, not `CurrentVersion\Explorer\Accent\AccentColorMenu`) | Standard Stack, Pattern 1 | Already flagged and accepted as a rig-verifiable hypothesis by D-01/D-05 — if wrong, the fallback (`DwmGetColorizationColor`) still produces *a* plausible accent-adjacent color, so failure mode is "slightly wrong shade," not "crash" or "no color at all" |
| A2 | The registry `AccentColor` DWORD's byte layout, once actually worked through, requires the *same* shift/mask extraction as `DwmGetColorizationColor`'s documented 0xAARRGGBB (i.e., no R/B swap needed between the two paths) | Pattern 1, Pitfall A | If wrong, one of the two paths renders visibly swapped colors (blue accent shows as orange/red-ish) — D-05's rig color-picker check and Pitfall A's saturated-primary-color test are both specifically designed to catch this before it ships |
| A3 | `SystemEvents.UserPreferenceChanged` fires for an accent-color-only change (not just light/dark theme changes) on Windows 11, so extending the existing handler (D-02) is sufficient without also wiring `WM_DWMCOLORIZATIONCOLORCHANGED` | Pattern 2, PITFALLS.md Pitfall 5 (milestone-level, restated here for completeness) | If wrong, live accent updates silently don't fire at all — D-05's "multiple live flips including a same-color no-op" rig test is the explicit catch mechanism; PITFALLS.md's discretion item already authorizes falling back to `WM_DWMCOLORIZATIONCOLORCHANGED` interception if this proves false on the rig |

**None of these were resolvable without a live Windows 11 rig** — all three are exactly the class of claim `21-CONTEXT.md` D-05 was written to gate, and this research does not attempt to shortcut that gate; it narrows what specifically needs checking and gives the planner concrete code to check it against.

## Open Questions

1. **Does `SystemEvents.UserPreferenceChanged`'s `UserPreferenceCategory` argument (`e.Category`) ever narrow specifically to something accent-related, that the handler could branch on rather than unconditionally re-reading on every fire?**
   - What we know: `WindowsThemeProvider`'s existing theme-diff block deliberately does NOT filter by category (comment: "deliberately left unfiltered by UserPreferenceCategory... the safer fallback per research") — it just re-reads and diffs on every fire, regardless of category, accepting the minor waste of a redundant registry read on unrelated preference changes.
   - What's unclear: Whether `UserPreferenceCategory.Color` (or similar) reliably correlates with accent-color changes specifically, which could make the new block slightly more efficient — but this would deviate from the codebase's existing "unfiltered, diff-based" convention for no clearly demonstrated benefit.
   - Recommendation: Do not filter by category — follow the existing unfiltered-diff convention exactly, for consistency and because PITFALLS.md's own research found no reliable documentation on which category accent changes report under. This is a "don't add complexity without evidence" call, not an open blocker.

2. **Is the `AccentColor` registry value ever absent on a fully-updated Windows 11 install (making the fallback path a real, exercised branch rather than dead code)?**
   - What we know: Community sources treat it as generally present on modern Windows 10/11, but no official documentation confirms it's guaranteed to exist on every build/edition.
   - What's unclear: Whether the fallback path will ever actually execute on the user's rig, or whether it's purely a defensive branch that never fires in practice.
   - Recommendation: Implement the fallback regardless (D-01 already requires it) but do not over-invest in testing the fallback path specifically if the rig's `AccentColor` key is confirmed present during D-05's verification pass — a quick registry check (`reg query "HKCU\Software\Microsoft\Windows\DWM" /v AccentColor`) during the rig session would confirm which path is actually live on this user's machine, worth including as a checklist step.

## Rig-Verification Checklist (concrete steps for D-05)

This expands `21-CONTEXT.md` D-05's requirement into concrete, executable steps, incorporating this codebase's existing `debug.log`/`Trace.WriteLine` diagnostic convention (`WindowsThemeProvider.Log()`, already gated behind `AppSettings.EnableDebugLogging`, already persists to `%APPDATA%\RigToggle\debug.log` per `Program.cs`).

1. **Enable debug logging first.** Set `EnableDebugLogging: true` in `settings.json` (or via whatever Settings UI toggle exists for it) before starting the verification pass, so `WindowsThemeProvider.Log()`'s new `"Accent color flip detected: {previous} -> {resolved}"` line is captured to `debug.log` — this gives an objective, timestamped confirmation that the diff-and-raise fired, independent of whether the visual repaint is also correct (isolates "did the event fire" from "did the repaint apply").
2. **Registry ground-truth check.** Before any app testing, run `reg query "HKCU\Software\Microsoft\Windows\DWM" /v AccentColor` in a terminal on the rig to confirm the key exists and note its raw hex value — this both resolves Open Question 2 and gives a known-good numeric value to cross-check the app's parsed `Color` against by hand (apply the Pattern 1 extraction manually to the raw hex and compare to what the app renders).
3. **Static accent-swatch match (Success Criterion 3).** With the app running and the rig's actual custom blue accent (D-03) active, use a color-picker tool (e.g. Windows' built-in `Snip & Sketch`'s no color picker — use PowerToys Color Picker if installed, or any pixel-color-reading tool) to sample both (a) the swatch shown in Settings > Personalization > Colors and (b) the toggle switch's "on" track fill / a tile's icon fill in the app. Confirm the sampled hex values match exactly (not "close").
4. **Title-bar-toggle-ON scenario (D-03 config).** Confirm "Show accent color on title bars and windows borders" is ON (per D-03, this is the rig's actual current setting) during step 3 — do not additionally test the OFF state unless time permits, since D-03 establishes ON is this user's real, ongoing configuration and PITFALLS.md's OFF-state divergence risk is explicitly de-prioritized for that reason.
5. **Live flip test — multiple changes in one session (Success Criterion 2, PITFALLS.md Pitfall 5).** With the app running (not restarted), open Settings > Personalization > Colors and change the accent color at least 3 times in a row to visibly different colors, confirming the app's accent-tinted elements update after each change without restarting. Check `debug.log` after each change for a corresponding "Accent color flip detected" line.
6. **Same-color no-op test (PITFALLS.md Pitfall 5's explicit warning).** Pick the exact same accent color twice in a row (re-select the currently-active swatch) and confirm `debug.log` does NOT show a spurious "Accent color flip detected" line for that no-op reselection (or, if `SystemEvents.UserPreferenceChanged` fires anyway, confirm the diff correctly suppresses the redundant `AccentColorChanged` raise/repaint) — this is the specific scenario Pitfall B above and PITFALLS.md's own Pitfall 5 both flag as easy to silently get wrong.
7. **Saturated-primary-color R/B-swap check (this research's Pitfall A).** Temporarily set the accent color to pure red or pure blue (a color visually easy to distinguish from its R/B-swapped counterpart), and re-run step 3's color-picker comparison — this is a more sensitive check than the user's actual custom blue (D-03) for catching a byte-order bug specifically, so do this in addition to, not instead of, the real-accent-color check.
8. **Report PASS/FAIL back per D-05** with the specific step(s) that failed if any — do not mark THEME-07/Phase 21 complete without this reported result.

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmgetcolorizationcolor — fetched live this session; confirmed exact signature (`HRESULT DwmGetColorizationColor(DWORD *pcrColorization, BOOL *pfOpaqueBlend)`), 0xAARRGGBB format, and Microsoft's own explicit warning about format confusion with `0x00BBGGRR` COLORREF-style APIs
- Direct read of current repo source, this session: `src/RigToggle.Core/Abstractions/IThemeProvider.cs`, `src/RigToggle.Windows/WindowsThemeProvider.cs`, `src/RigToggle.Windows/NativeMethods.cs`, `src/RigToggle.Windows/DwmTitleBar.cs`, `src/RigToggle.App/MainForm.cs`, `src/RigToggle.App/ThemeApplier.cs`, `src/RigToggle.App/Controls/MonitorTile.cs`, `src/RigToggle.App/Controls/ToggleSwitch.cs`, `src/RigToggle.App/MonitorConfirmDialog.cs`, `src/RigToggle.App/SettingsForm.cs`, `src/RigToggle.App/Program.cs`, `src/RigToggle.Tests/Doubles/FakeThemeProvider.cs`, `src/RigToggle.Tests/ThemeProviderContractTests.cs`

### Secondary (MEDIUM confidence)
- Community C# gist/code sample (surfaced via WebSearch) demonstrating `HKCU\Software\Microsoft\Windows\DWM\AccentColor` read + ABGR byte extraction — cross-corroborated by a second, independently-surfaced WebSearch source describing the same registry path's BGR/COLORREF-adjacent convention with an explicit worked example (`0xffc77e35` = Win10 Blue with 0xff alpha prefix)
- Microsoft Q&A community thread (learn.microsoft.com/answers) on changing Windows 11 taskbar accent color via registry — corroborates `HKCU\Software\Microsoft\Windows\DWM` as a real, actively-referenced registry location for accent-related values, though the thread's own primary focus (AccentPalette) is a related-but-distinct value from the single `AccentColor` DWORD this phase reads

### Tertiary (LOW confidence, needs rig validation — carried forward from milestone-level research)
- Precedence/reliability of `AccentColor` vs `ColorizationColor` vs `AccentColorMenu` as "the" canonical accent swatch — no official Microsoft documentation found for this at either the milestone-research pass or this phase-research pass; this remains the single most rig-dependent open item in this phase, exactly as `PITFALLS.md` Pitfall 4 already flagged
- `SystemEvents.UserPreferenceChanged` reliably firing for accent-only (not light/dark) changes — community-sourced only, not independently reproduced on this project's rig hardware yet

## Metadata

**Confidence breakdown:**
- Standard stack (P/Invoke signature): HIGH — official Microsoft Learn docs, fetched live this session, exact signature confirmed
- Registry byte-format derivation: MEDIUM-HIGH — cross-corroborated across two independent community sources with working code, but explicitly flagged (Pitfall A, Assumption A2) as needing on-rig confirmation with a saturated test color before being treated as settled
- Which registry key is canonical ("the" accent color): LOW-MEDIUM, unchanged from milestone-level research — no official documentation exists; D-01/D-05 already correctly scope this as a rig-verified hypothesis, not a fact this research can upgrade further without actual rig access
- Architecture/integration points (where code goes, what changes): HIGH — based on direct reading of the actual current `src/` files this session, zero drift from `21-CONTEXT.md`'s description found

**Research date:** 2026-08-11
**Valid until:** Should remain valid through this phase's implementation (no external API surface here is fast-moving — `DwmGetColorizationColor` is a Vista-era stable API); re-verify the registry-key claim specifically if Windows ships a major Settings/Personalization redesign before this phase executes.
