# Phase 23: Manual Light/Dark Override - Pattern Map

**Mapped:** 2026-08-16
**Files analyzed:** 9
**Analogs found:** 9 / 9

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.Core/Models/AppSettings.cs` | model | CRUD (nullable field add) | itself (existing nullable-field convention) | exact |
| `src/RigToggle.Core/Models/AppTheme.cs` | model | n/a (referenced as-is, no changes) | itself | exact — no change needed |
| `src/RigToggle.Core/OverridableThemeProvider.cs` (new) | service (decorator) | event-driven | `src/RigToggle.Windows/WindowsThemeProvider.cs` (diff-against-last-known pattern) + `ARCHITECTURE.md` Pattern 2 sketch | role-match (no existing decorator in Core; closest is the OS-signal provider it wraps) |
| `src/RigToggle.App/MainForm.cs` (IsDark + OnThemeChanged) | controller/view | event-driven | `src/RigToggle.App/MonitorConfirmDialog.cs` (smallest clean instance of the same 3-copy pattern) | exact (this file is itself one of the 3 copies) |
| `src/RigToggle.App/SettingsForm.cs` (IsDarkTheme + radio group + Save/Discard wiring) | controller/view | request-response (Save/Discard) + event-driven (live preview) | `SettingsForm.cs` itself — `chkCloseMinimizesToTray`/tray-visibility Save flow is the direct analog for the new radio group's persistence + live-apply callback | exact |
| `src/RigToggle.App/SettingsForm.Designer.cs` (radio group in `pnlThemeReserved`) | view (generated layout) | n/a (static layout) | `chkCloseMinimizesToTray`/`chkStartWithWindows` declarations (lines ~738-764) | exact |
| `src/RigToggle.App/MonitorConfirmDialog.cs` (IsDark + OnThemeChanged) | controller/view | event-driven | itself — one of the 3 copies; also mirrors `MainForm`/`SettingsForm`'s identical shape | exact |
| `src/RigToggle.App/Program.cs` (composition root, line 124) | config (DI wiring) | n/a | itself — existing `new WindowsThemeProvider()` call site | exact |
| `src/RigToggle.Core/Abstractions/ISettingsStore.cs` | interface | CRUD | itself — no change required, `ThemeOverride` flows through existing `Load()`/`Save()` | exact — no change needed |

## Pattern Assignments

### `src/RigToggle.Core/Models/AppSettings.cs` (model, CRUD)

**Analog:** itself — existing nullable "unset" field convention

**Core pattern** (lines 19-37, full class):
```csharp
public sealed class AppSettings
{
    public string? MonitorDevicePath { get; set; }
    ...
    public int? HotkeyModifiers { get; set; }
    public int? HotkeyKey { get; set; }
}
```
**Action:** add `public AppTheme? ThemeOverride { get; set; }` as one more nullable field, following the exact same "null = unset/first-run" convention documented in the class doc-comment (lines 3-18). Needs `using RigToggle.Core.Models;` already present (self-referential — same file). No new namespace/using needed since `AppTheme` is already in `RigToggle.Core.Models`.

---

### `src/RigToggle.Core/OverridableThemeProvider.cs` (new — service/decorator, event-driven)

**Analog:** `ARCHITECTURE.md` Pattern 2 (authoritative design, already vetted) + `WindowsThemeProvider`'s diff-against-last-known-value discipline for the "only fire on genuine flip" refinement mentioned in ARCHITECTURE.md line 184.

**Interface being implemented** (`src/RigToggle.Core/Abstractions/IThemeProvider.cs` lines 26-35):
```csharp
public interface IThemeProvider
{
    AppTheme CurrentTheme { get; }
    event EventHandler? ThemeChanged;
    Color AccentColor { get; }
    event EventHandler? AccentColorChanged;
}
```

**Core pattern** (ARCHITECTURE.md lines 159-184, use as the starting skeleton):
```csharp
public sealed class OverridableThemeProvider : IThemeProvider
{
    private readonly IThemeProvider _inner;
    private readonly ISettingsStore _settingsStore;
    public event EventHandler? ThemeChanged;
    public event EventHandler? AccentColorChanged { add => _inner.AccentColorChanged += value; remove => _inner.AccentColorChanged -= value; }

    public OverridableThemeProvider(IThemeProvider inner, ISettingsStore settingsStore)
    {
        _inner = inner; _settingsStore = settingsStore;
        _inner.ThemeChanged += (_, _) => ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public AppTheme CurrentTheme => ReadOverride() ?? _inner.CurrentTheme;
    public Color AccentColor => _inner.AccentColor;

    private AppTheme? ReadOverride()
    {
        try { return _settingsStore.Load().ThemeOverride; } catch { return null; }
    }

    // Called by SettingsForm right after _settingsStore.Save(), mirroring ApplyTrayVisibility().
    public void RefreshOverride() => ThemeChanged?.Invoke(this, EventArgs.Empty);
}
```

**Fail-silent pattern to apply** (per PITFALLS.md and CONTEXT.md D-05's referenced convention): a corrupt/unreadable `ThemeOverride` degrades to System (`null`), never throws — the `try/catch { return null; }` above already encodes this; do not "improve" it into a logged error, matching Phase 12/21's cosmetic-path-fails-silently posture.

**Diff-before-raise refinement** (recommended by ARCHITECTURE.md line 184, modeled on the existing diff pattern) — check `src/RigToggle.Windows/WindowsThemeProvider.cs`'s `OnUserPreferenceChanged` handler for the "only fire ThemeChanged if the resolved value actually changed" shape and replicate it in `RefreshOverride()`/the inner `ThemeChanged` relay so a Save that doesn't actually change the effective theme doesn't force an unnecessary repaint cascade (not strictly required by CONTEXT.md, but consistent with established codebase discipline — apply only if trivial, do not over-engineer).

**Placement:** `src/RigToggle.Core/` (no subfolder — `ToggleOrchestrator.cs` and other root-level Core services live directly in this folder, not `Abstractions/`).

---

### `src/RigToggle.App/MainForm.cs` (controller/view, event-driven)

**Analog:** `src/RigToggle.App/MonitorConfirmDialog.cs` (cleanest of the 3 copies)

**Current pattern to collapse** (`MainForm.cs` line 199):
```csharp
// (registry-primary, DWM-fallback). Unlike IsDark-derived colors, this does
// correct across live flips -- mirrors SettingsForm.IsDarkTheme.
private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;
```
**No change to this line's body is required** — since `_themeProvider` becomes an `OverridableThemeProvider` at the composition root (Program.cs), `CurrentTheme` on that decorator already resolves override-or-live transparently. Per D-04/Pitfall 6, this property must call through the shared resolver rather than reading `WindowsThemeProvider` directly — which is exactly what changing `Program.cs`'s injected instance accomplishes, with zero code change needed in `MainForm.cs` itself beyond confirming the constructor parameter type stays `IThemeProvider` (already true).

**Constructor subscription pattern** (`MainForm.cs` lines 129-138, unchanged, still correct against the decorator):
```csharp
_themeProvider.ThemeChanged += OnThemeChanged;
...
_themeProvider.AccentColorChanged += OnThemeChanged;
```

**OnThemeChanged handler** (`MainForm.cs` lines 171-183, unchanged pattern, must remain reachable when `OverridableThemeProvider.RefreshOverride()` fires `ThemeChanged`):
```csharp
private void OnThemeChanged(object? sender, EventArgs e)
{
    if (InvokeRequired)
    {
        BeginInvoke(new Action(() => OnThemeChanged(sender, e)));
        return;
    }
    ...
    DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
    ThemeApplier.ThemeButton(btnSettings, IsDark);
    ...
}
```

**Action:** no logic change required in `MainForm.cs` for D-04 reach — it is satisfied entirely by Program.cs's composition-root swap. Verify (do not skip) that `IsDark` is read fresh, never cached (Anti-Pattern 3 in ARCHITECTURE.md) — already true today.

---

### `src/RigToggle.App/MonitorConfirmDialog.cs` (controller/view, event-driven)

**Analog:** itself / `MainForm.cs` (identical shape, smaller file)

**Full pattern** (lines 45-82):
```csharp
_themeProvider.ThemeChanged += OnThemeChanged;
this.FormClosed += (_, _) => _themeProvider.ThemeChanged -= OnThemeChanged;
...
DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
ThemeApplier.ThemeButton(btnContinue, IsDark);
ThemeApplier.ThemeButton(btnCancel, IsDark);
...
// read fresh every call (never cached) -- mirrors SettingsForm.IsDarkTheme /
// MainForm.IsDark.
private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;
...
private void OnThemeChanged(object? sender, EventArgs e)
{
    if (InvokeRequired)
    {
        BeginInvoke(new Action(() => OnThemeChanged(sender, e)));
        return;
    }
    ...
}
```
**Action:** same as MainForm — no code change needed inside this file. D-05 (read fresh each time a dialog opens) is already satisfied by this existing "never cached" property + the fact that `MonitorConfirmDialog` is constructed fresh per open (per CONTEXT.md's own note that this matches "today's `IsDark` pattern"). The only wiring change is at the call site that constructs this dialog (still passes `_themeProvider` straight through — that reference is the decorator once Program.cs is updated).

---

### `src/RigToggle.App/SettingsForm.cs` (controller/view, request-response + event-driven)

**Analog:** itself — existing constructor-injected-callback idiom (`_applyTrayVisibility`) is the direct precedent for the new `applyThemeOverride`-style callback ARCHITECTURE.md's Internal Boundaries table calls for.

**Constructor + field pattern to extend** (lines 22, 73, 81):
```csharp
private readonly Action _applyTrayVisibility;
...
public SettingsForm(IMonitorController monitorController, IAudioController audioController, ISettingsStore settingsStore, IAutostartConfigurator autostartConfigurator, IThemeProvider themeProvider, Func<bool> tryRegisterConfiguredHotkey, Action applyTrayVisibility)
...
_applyTrayVisibility = applyTrayVisibility ?? throw new ArgumentNullException(nameof(applyTrayVisibility));
```
**Action:** per ARCHITECTURE.md's Internal Boundaries table ("`SettingsForm` ↔ composition root: Constructor-injected callback delegates ... + new `applyThemeOverride`") add a new constructor parameter, e.g. `Action applyThemeOverride`, following this exact null-guarded assignment pattern — do NOT invent an `AppSettings`-level event.

**Theme subscription/property to collapse** (lines 91-92, 210):
```csharp
_themeProvider.ThemeChanged += OnThemeChanged;
this.FormClosed += (_, _) => _themeProvider.ThemeChanged -= OnThemeChanged;
...
private bool IsDarkTheme => _themeProvider.CurrentTheme == AppTheme.Dark;
```
**Action:** unchanged code, resolves through the decorator automatically once Program.cs wiring changes (same "zero code change, only DI swap" pattern as MainForm/MonitorConfirmDialog).

**Save flow analog for the new radio group's persistence** (lines 1146-1184, `BtnSaveSettings_Click`):
```csharp
var settingsToSave = new AppSettings
{
    ...
    EnableDebugLogging = chkEnableDebugLogging.Checked,
    CloseMinimizesToTray = chkCloseMinimizesToTray.Checked,
    MinimizeToTray = chkMinimizeToTray.Checked,
    HotkeyModifiers = _pendingHotkeyModifiers,
    HotkeyKey = _pendingHotkeyKey,
};

// Persist before the declarative DialogResult.OK closes the dialog.
// Discard/close requires no handler — CancelButton wiring (constructor)
// produces DialogResult.Cancel with nothing persisted.
_settingsStore.Save(settingsToSave);

// D-08: apply the derived tray-icon visibility live, the moment settings
// persist — must run here...
_applyTrayVisibility();
```
**Action:** add `ThemeOverride = _pendingThemeOverride` (or equivalent radio-group-derived nullable `AppTheme?`) to the `settingsToSave` object literal, then call the new `_applyThemeOverride()` callback in the same spot `_applyTrayVisibility()` is called (line 1190) — this is the exact "explicit live-apply callback... same slot as `_applyTrayVisibility()`" idiom ARCHITECTURE.md's Pattern 2 description calls for (line 151).

**Discard/close-without-save analog** (line 106):
```csharp
this.CancelButton = btnDiscardChanges;
```
Comment at line 1182: "Discard/close requires no handler — CancelButton wiring (constructor) produces DialogResult.Cancel with nothing persisted." — this is the existing mechanism D-03 says the new theme-preview-revert should ride, unmodified. **Action:** D-02/D-03 (revert live preview on Discard/close-without-save) requires a *new* explicit handler for the theme case specifically — unlike every other field, the theme radio group applies **immediately on click** (D-01), so on Discard/Close the live preview must be actively reverted (re-invoke the theme-refresh path with the last-persisted `ThemeOverride`, e.g. re-read via `_settingsStore.Load()` or a cached pre-open value per CONTEXT.md's "Claude's Discretion" note) — attach this revert to whatever event already fires for `CancelButton`/`btnDiscardChanges.Click` (grep for `BtnDiscardChanges_Click` — the existing wiring is declarative via `CancelButton`, so an explicit `Click`/`FormClosing` handler needs adding for this one new behavior; do not disturb the existing "no handler" pattern for other fields).

**Live-preview-on-click wiring (D-01, new):** follow the same `SelectedIndexChanged`/`Click`-based wiring already used for `cboAudioNormal.SelectedIndexChanged += OnPickerChanged;` (line 113-114) — attach a `RadioButton.CheckedChanged` handler to each of the 3 new radio buttons that immediately persists nothing but instead updates a pending in-memory override value and re-invokes the same repaint path `OnThemeChanged` uses (or directly calls `_applyThemeOverride`-style refresh) — this is the one field in the form that intentionally bypasses the "only take effect on Save" convention (CONTEXT.md D-01), so do not gate it behind `ValidateSettingsForm()`/`btnSaveSettings.Enabled`.

---

### `src/RigToggle.App/SettingsForm.Designer.cs` (view, static layout)

**Analog:** `chkCloseMinimizesToTray` declaration (lines 738-743) and `pnlThemeReserved`'s own existing reservation comment (lines 778-799)

**Insertion point** (lines 792-798, already exists, do not modify slot's own outer margin/size behavior per UI-SPEC):
```csharp
this.pnlThemeReserved.AutoSize = true;
this.pnlThemeReserved.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
this.pnlThemeReserved.Size = new System.Drawing.Size(0, 0);
this.pnlThemeReserved.Margin = new System.Windows.Forms.Padding(0);
this.pnlThemeReserved.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
this.pnlThemeReserved.TabIndex = 8;
this.pnlThemeReserved.Name = "pnlThemeReserved";
```

**Sibling control declaration pattern to copy for each RadioButton + the "Theme:" caption Label** (lines 738-743):
```csharp
this.chkCloseMinimizesToTray.Text = "Closing the window (X) minimizes to tray";
this.chkCloseMinimizesToTray.AutoSize = true;
this.chkCloseMinimizesToTray.Anchor = System.Windows.Forms.AnchorStyles.Left;
this.chkCloseMinimizesToTray.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
this.chkCloseMinimizesToTray.TabIndex = 4;
this.chkCloseMinimizesToTray.Name = "chkCloseMinimizesToTray";
```
**Action:** add 3 `RadioButton` fields (`rdoThemeSystem`/`rdoThemeLight`/`rdoThemeDark`) plus 1 caption `Label` (`lblThemeCaption`) as children added to `pnlThemeReserved`'s `Controls` collection (mirroring how other sections' children are added to their FlowLayoutPanel parent — grep the file for `pnlSharedSection.Controls.Add` for the exact addition idiom). Apply UI-SPEC's spacing tokens verbatim: caption→first radio = `Margin(0,0,0,4)` (matches `lblMonitorCaption` precedent, UI-SPEC line 46), each radio's own bottom margin = `Margin(0,0,0,8)` (matches every `CheckBox`'s `Margin(0,0,0,8)` above). Labels exactly `"System (default)"` / `"Light"` / `"Dark"` per D-06/D-07 — do not rewrite. No `Font` override on any new control (inherit Form default, per UI-SPEC Typography section and this file's zero-`Font`-override convention). No `ThemeApplier` call needed for these controls (per UI-SPEC Color section — native controls auto-theme via `Application.SetColorMode`).

---

### `src/RigToggle.App/Program.cs` (composition root, config)

**Analog:** itself — existing single wiring line

**Current pattern** (line 124):
```csharp
var themeProvider = new WindowsThemeProvider();
```
Consumers (lines 150, 152):
```csharp
SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore, autostartConfigurator, themeProvider, mainForm.TryRegisterConfiguredHotkey, mainForm.ApplyTrayVisibility);
mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory, themeProvider);
```
**Action:** wrap the concrete provider — construct `OverridableThemeProvider` around `WindowsThemeProvider` and pass the decorator (typed as `IThemeProvider`) everywhere `themeProvider` is currently referenced. Also add the new `applyThemeOverride` callback argument to `SettingsFormFactory`'s `new SettingsForm(...)` call, following the exact same pattern `mainForm.ApplyTrayVisibility` already uses (an existing `MainForm`/decorator method reference, or a small local lambda calling `overridableThemeProvider.RefreshOverride()`):
```csharp
var innerThemeProvider = new WindowsThemeProvider();
var themeProvider = new OverridableThemeProvider(innerThemeProvider, settingsStore);
...
SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore, autostartConfigurator, themeProvider, mainForm.TryRegisterConfiguredHotkey, mainForm.ApplyTrayVisibility, themeProvider.RefreshOverride);
mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory, themeProvider);
```
`MonitorConfirmDialog`'s constructor call site(s) (wherever `new MonitorConfirmDialog(..., themeProvider)` is invoked — inside `MainForm.cs`, since `MonitorConfirmDialog` is constructed per-show, not at Program.cs) need no change, since `MainForm` already holds the `themeProvider` reference passed in and simply forwards it — confirm by grepping `MainForm.cs` for `new MonitorConfirmDialog(`.

---

## Shared Patterns

### "Never cache CurrentTheme, always read fresh" (Anti-Pattern 3, ARCHITECTURE.md)
**Source:** `MainForm.cs` line 199, `SettingsForm.cs` line 210, `MonitorConfirmDialog.cs` line 63 — all three already follow this.
**Apply to:** `OverridableThemeProvider.CurrentTheme` getter — must call `_settingsStore.Load()` fresh each read (not cache the override at construction), exactly mirroring the discipline already established on the three consumer sides.

### Two-call-site theming rule
**Source:** `PITFALLS.md` Pitfall 1, `MainForm.cs` `OnThemeChanged` (line 171) + `InitializeTrayState()` (line ~1035-1070).
**Apply to:** N/A for this phase's new controls specifically (native `RadioButton`/`Label` need zero `ThemeApplier` calls per UI-SPEC), but relevant if any theme-driven repaint call is added — verify both call sites still exist and both remain correct after the `IThemeProvider` swap to `OverridableThemeProvider` (no new call site should be needed, since `IsDark`/`IsDarkTheme` properties are unchanged).

### Fail-silent cosmetic degrade
**Source:** established Phase 12/21 convention, restated in CONTEXT.md line 80 and UI-SPEC's Error State row.
**Apply to:** `OverridableThemeProvider.ReadOverride()` — corrupt/missing `ThemeOverride` degrades to `null` (System/live-follow), never throws, never shows user-facing error UI.

### Constructor-injected callback delegate (not an event)
**Source:** `SettingsForm.cs` `_applyTrayVisibility` (lines 22, 73, 81, 1190).
**Apply to:** the new `applyThemeOverride` callback threaded from `Program.cs` composition root into `SettingsForm`'s constructor — ARCHITECTURE.md's Internal Boundaries table explicitly names this as the pattern to follow, warning against inventing a new mechanism (e.g., an `AppSettings`-level event).

### `ThemeChanged` marshal-to-UI-thread guard
**Source:** `MainForm.cs` lines 171-176, `SettingsForm.cs` lines 221-226, `MonitorConfirmDialog.cs` lines 69-74 — identical `InvokeRequired`/`BeginInvoke` triple across all three.
**Apply to:** No new code needed in these three files (unchanged), but relevant if `OverridableThemeProvider.RefreshOverride()` is ever called from a non-UI thread — currently it will only be called from `SettingsForm`'s Save handler (already UI thread), so this guard requirement is satisfied by the existing subscriber-side handling, not by the provider itself.

## No Analog Found

None — all 9 files have a clear same-codebase analog (several are the file itself, since this phase mostly extends/wires existing structures rather than introducing new UI surfaces).

## Metadata

**Analog search scope:** `src/RigToggle.Core/`, `src/RigToggle.App/`, `src/RigToggle.Windows/` (read-only grep + targeted Read of `MainForm.cs`, `SettingsForm.cs`, `SettingsForm.Designer.cs`, `MonitorConfirmDialog.cs`, `Program.cs`, `AppSettings.cs`, `AppTheme.cs`, `ISettingsStore.cs`, `IThemeProvider.cs`)
**Files scanned:** 9 target files + their direct existing-code neighbors (no unrelated directories explored — this phase's `code_context`/`canonical_refs` in CONTEXT.md already enumerated exact line numbers, so search was targeted rather than exploratory)
**Pattern extraction date:** 2026-08-16
