using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Diagnostics;
using RigToggle.Core.Models;
using RigToggle.Windows;

namespace RigToggle.App
{
    /// <summary>
    /// Settings modal dialog (D-03): three labeled sections (Monitor, Audio Devices,
    /// Target App — D-09) bound to real enumerated hardware (D-05) through the
    /// Core interfaces only — never instantiates concrete Windows adapters directly
    /// (02-RESEARCH.md Anti-Pattern: "Putting P/Invoke/COM calls in ... code-behind").
    /// </summary>
    public partial class SettingsForm : Form
    {
        private readonly IMonitorController _monitorController;
        private readonly IAudioController _audioController;
        private readonly ISettingsStore _settingsStore;
        private readonly IAutostartConfigurator _autostartConfigurator;
        private readonly IThemeProvider _themeProvider;
        private readonly Func<bool> _tryRegisterConfiguredHotkey;
        private readonly Action _applyTrayVisibility;

        // THEME-09/23-02: two constructor-injected callback delegates from the
        // composition root's OverridableThemeProvider (never a new AppSettings-level
        // event) — one previews an unsaved radio selection immediately (D-01), the
        // other drops any active preview and re-resolves from whatever is persisted
        // (D-02/D-03). Mirrors the _applyTrayVisibility idiom above exactly.
        private readonly Action<AppTheme?> _previewThemeOverride;
        private readonly Action _applyThemeOverride;

        private AppSettings _settings = new();

        // TRIG-01/D-01: the working (not-yet-saved) hotkey combo, initialized from
        // _settings on Load and mutated only by the capture state machine below. Null
        // means "no hotkey configured" (D-02 — no default is pre-filled).
        private int? _pendingHotkeyModifiers;
        private int? _pendingHotkeyKey;

        // 15-03/D-01: the working (not-yet-saved) app-launch-target path, mirroring the
        // _pendingHotkeyModifiers/_pendingHotkeyKey idiom above. txtAppPath.Text is a
        // pure DISPLAY concern (shows the real path when set, a friendly "not
        // configured" sentence when null) -- it must never be read back as the
        // persisted value, or the literal placeholder text round-trips into
        // AppSettings as a bogus "path" (RESEARCH.md Landmine). Null means "cleanly
        // unset" (APP-04); set-but-invalid is a distinct, still-blocking state (D-06).
        private string? _pendingAppPath;

        // Reentrancy/mode-tracking guard for the txtHotkey capture state machine —
        // mirrors the _updatingMonitorGridProgrammatically boolean-flag idiom already
        // established for the monitor grid's own programmatic-write guard.
        private bool _recordingHotkey;

        // Enumerated (active + OS-disabled) monitors backing the grid — cached from the
        // last PopulateMonitorGrid() call so validation/save can re-read it without a
        // second GetAllMonitors() round trip mid-interaction (D-03/D-04/D-05).
        private IReadOnlyList<MonitorInfo> _allMonitors = Array.Empty<MonitorInfo>();

        // Device paths the user has explicitly told Settings to stop preserving (via the
        // stale-warning's "Forget" link) — a genuinely-gone device path (renamed/
        // re-enumerated by Windows, not merely unplugged) would otherwise be re-merged back
        // into MonitorsToDisable/MonitorsToEnable on every Save forever, since the app has
        // no way to distinguish "temporarily disconnected" from "permanently gone". Session-
        // scoped only (not persisted itself) — takes effect the moment Save is next clicked.
        private readonly HashSet<string> _forgottenStaleDevicePaths = new();
        private readonly HashSet<string> _forgottenStaleDevicePathsNormal = new();

        // Reentrancy guard around the D-04 programmatic sibling-checkbox write — without
        // this, unchecking the sibling column would itself re-fire CellValueChanged
        // (06-UI-SPEC.md Grid Spec § D-04 mechanism, RESEARCH.md Pitfall 5).
        private bool _updatingMonitorGridProgrammatically;

        // 16-02: independent reentrancy guard for the Normal grid's own sibling-checkbox
        // write — deliberately NOT shared with _updatingMonitorGridProgrammatically above,
        // since the two grids are edited/committed independently (16-PATTERNS.md).
        private bool _updatingMonitorGridNormalProgrammatically;

        // THEME-09/23-02: the working (not-yet-saved) theme override, mirroring the
        // _pendingHotkeyModifiers/_pendingHotkeyKey idiom — null means System/live-follow,
        // matching AppSettings.ThemeOverride's own "null = unset" convention.
        private AppTheme? _pendingThemeOverride;

        // Reentrancy guard for the programmatic radio-selection write in
        // SettingsForm_Load — mirrors _updatingMonitorGridProgrammatically. Without this,
        // assigning .Checked at load time would raise CheckedChanged and be
        // mistaken for a user click, firing a spurious live preview. Task 3's
        // OnThemeRadioCheckedChanged reads _updatingThemeRadiosProgrammatically first,
        // returning immediately while it is set.
        private bool _updatingThemeRadiosProgrammatically;

        /// <summary>
        /// Display/value wrapper for ComboBox binding (DisplayMember/ValueMember) —
        /// 02-RESEARCH.md Pattern 2. 15-03/D-02: Id widened to string? so a sentinel
        /// instance (Id = null, DisplayLabel = "(None — don't switch audio)") can
        /// represent "deliberately unset" as a real, always-present list entry rather
        /// than a blank SelectedIndex = -1 state.
        /// </summary>
        private sealed record PickerItem(string? Id, string DisplayLabel);

        // 12-02: ctor param + field only in this plan -- SettingsForm's own
        // subscribe/OnThemeChanged/per-control theming lands in plan 12-03. Threaded
        // here so the composition root (Program.cs SettingsFormFactory) compiles.
        public SettingsForm(IMonitorController monitorController, IAudioController audioController, ISettingsStore settingsStore, IAutostartConfigurator autostartConfigurator, IThemeProvider themeProvider, Func<bool> tryRegisterConfiguredHotkey, Action applyTrayVisibility, Action<AppTheme?> previewThemeOverride, Action applyThemeOverride)
        {
            _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
            _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _autostartConfigurator = autostartConfigurator ?? throw new ArgumentNullException(nameof(autostartConfigurator));
            _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));
            _tryRegisterConfiguredHotkey = tryRegisterConfiguredHotkey ?? throw new ArgumentNullException(nameof(tryRegisterConfiguredHotkey));
            _applyTrayVisibility = applyTrayVisibility ?? throw new ArgumentNullException(nameof(applyTrayVisibility));
            _previewThemeOverride = previewThemeOverride ?? throw new ArgumentNullException(nameof(previewThemeOverride));
            _applyThemeOverride = applyThemeOverride ?? throw new ArgumentNullException(nameof(applyThemeOverride));

            InitializeComponent();

            // 12-03/D-05: live theme-follow, mirroring MonitorConfirmDialog's (12-02)
            // marshal-then-try/catch pattern. SettingsForm is transient (fresh-per-open
            // via `using var settingsForm = ...ShowDialog()`, see OpenSettingsDialog in
            // MainForm.cs) while WindowsThemeProvider is an app-lifetime singleton --
            // FormClosed unsubscribe (not Dispose) is REQUIRED here or every dialog open
            // leaks a handler onto the outliving provider (T-12-05).
            _themeProvider.ThemeChanged += OnThemeChanged;

            // THEME-09/23-02/D-02/D-03: unconditionally drops any active live preview and
            // re-resolves from whatever is persisted, on every close route (Discard, Esc,
            // the window X, and even Save-then-close). Unsubscribing ThemeChanged FIRST
            // means the revert cannot try to repaint a form that is already closing. Firing
            // unconditionally rather than branching on DialogResult is deliberate: after a
            // successful Save this is a no-op (persisted already equals the preview), and
            // after any non-save exit it restores the last-saved override (or System/
            // live-follow if none was ever saved) — one line correctly covers every exit
            // route this.CancelButton = btnDiscardChanges already funnels Discard/Esc/X
            // through, with no new close-handling pattern introduced.
            this.FormClosed += (_, _) =>
            {
                _themeProvider.ThemeChanged -= OnThemeChanged;
                _applyThemeOverride();
            };

            // 12-03/THEME-06: this dialog is always shown immediately via ShowDialog
            // (never hidden-tray-started like MainForm), so no --tray-safe timing
            // concern applies -- applying DWM chrome right here, post-InitializeComponent
            // (Handle already exists by this point), is sufficient. DwmSetWindowAttribute
            // is declared to return an HRESULT and never throws (D-07) -- no try/catch
            // needed around this specific call, matching MonitorConfirmDialog's precedent.
            DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDarkTheme);

            // Esc / system close box (X) both produce DialogResult.Cancel via the
            // declarative Discard button — no extra FormClosing handler needed
            // (02-RESEARCH.md Pattern 5).
            this.AcceptButton = btnSaveSettings;
            this.CancelButton = btnDiscardChanges;

            this.Load += SettingsForm_Load;
            dgvMonitors.CurrentCellDirtyStateChanged += DgvMonitors_CurrentCellDirtyStateChanged;
            dgvMonitors.CellValueChanged += OnMonitorCellValueChanged;
            dgvMonitorsNormal.CurrentCellDirtyStateChanged += DgvMonitorsNormal_CurrentCellDirtyStateChanged;
            dgvMonitorsNormal.CellValueChanged += OnMonitorNormalCellValueChanged;
            lblMonitorWarning.LinkClicked += LblMonitorWarning_LinkClicked;
            lblMonitorNormalWarning.LinkClicked += LblMonitorNormalWarning_LinkClicked;
            cboAudioNormal.SelectedIndexChanged += OnPickerChanged;
            cboAudioRig.SelectedIndexChanged += OnPickerChanged;

            // THEME-09/23-02/D-01: all three options share one handler — the theme
            // radio group is the single field in this form that intentionally applies
            // before Save, so it is deliberately excluded from the Save-enablement
            // validation gate that governs every field above (unlike
            // btnSaveSettings.Enabled's other dependencies, a click here is never
            // blocked by form validity). No unsaved-changes indicator is added for it
            // either — the live preview itself is the feedback that a change was made
            // (UI-SPEC Interaction Contract item 5).
            rdoThemeSystem.CheckedChanged += OnThemeRadioCheckedChanged;
            rdoThemeLight.CheckedChanged += OnThemeRadioCheckedChanged;
            rdoThemeDark.CheckedChanged += OnThemeRadioCheckedChanged;

            // D-01: capture mode must only ever begin via an explicit mouse click, never
            // via GotFocus alone (UI-SPEC Interaction States) — MouseDown fires before
            // any focus-change side effects, matching the UI-SPEC's own wording.
            txtHotkey.MouseDown += TxtHotkey_MouseDown;
            txtHotkey.PreviewKeyDown += TxtHotkey_PreviewKeyDown;
            txtHotkey.KeyDown += TxtHotkey_KeyDown;
            txtHotkey.LostFocus += TxtHotkey_LostFocus;
        }

        // 22-04 gap closure: Form.AutoSize was the mechanism producing D-05's
        // content-driven initial size, but the rig's Check 3 showed AutoSize also
        // fights a user-driven edge drag (resize preview flickers, then nothing
        // resizes -- SettingsForm.Designer.cs's SettingsForm block has the full
        // explanation). Disabling Form.AutoSize to fix that leaves the window at the
        // 300x300 Form default unless something else sets it; the only Designer-side
        // alternative is a hardcoded ClientSize, which D-05 forbids and which the
        // 22-01/22-02 TableLayoutPanel migration deliberately deleted. This override
        // is the sole reason SettingsForm.cs is touched by Phase 22 at all -- every
        // prior plan in this phase held this file byte-identical to the phase base
        // commit 0c1234f; that invariant is deliberately broken here per this plan's
        // hard constraint 3.
        //
        // OnLoad, not OnShown: StartPosition.CenterParent is applied when the window
        // becomes visible, which happens after OnLoad runs -- sizing here still lets
        // CenterParent center the final size. Sizing in OnShown would leave the
        // window visibly off-center (it would already have been centered at its
        // stale pre-resize size).
        //
        // base.OnLoad(e) must run first: it raises the Load event that runs
        // SettingsForm_Load, which populates both monitor grids and both audio
        // pickers, and it is also the point at which AutoScaleMode.Font scaling has
        // been applied. Measuring before it would measure empty grids at 100% scale.
        protected override void OnLoad(System.EventArgs e)
        {
            base.OnLoad(e);

            // Reflect the just-populated content rather than a stale layout cache.
            tlpRoot.PerformLayout();

            // tlpRoot.PreferredSize already includes tlpRoot's own 16px Padding, so
            // it is exactly the ClientSize the window wants.
            var preferredSize = tlpRoot.PreferredSize;

            // Clamp to the working area so a content-driven size at 150% display
            // scale cannot overshoot the screen (rig Check 15, threat T-22-16) --
            // this is the one place a screen-derived bound is allowed to override
            // the content-driven figure, since an unreachable window is worse than
            // a clipped one the user can still resize.
            //
            // Screen.FromControl(this.Owner), not Screen.FromControl(this): at this
            // point CenterParent has not yet repositioned the window (see comment
            // above), so measuring "this" resolves against the primary display
            // regardless of which monitor the dialog will actually be centered on.
            // ShowDialog(owner) sets this.Owner before OnLoad runs, so the owner's
            // current screen is the correct proxy for "the screen this dialog will
            // appear on" -- important on this project's own two-monitor rig setup.
            var workingArea = (this.Owner is not null
                ? Screen.FromControl(this.Owner)
                : Screen.FromPoint(System.Windows.Forms.Cursor.Position)).WorkingArea;

            // ClientSize excludes non-client chrome (title bar + resizable border);
            // workingArea is an outer-window bound. Subtract the chrome so the
            // resulting outer Form.Size -- not just ClientSize -- actually fits,
            // otherwise the clamp can still leave the window partially off-screen
            // by the chrome amount when the branch actually binds.
            var chrome = this.Size - this.ClientSize;
            var maxClientWidth = System.Math.Max(0, workingArea.Width - chrome.Width);
            var maxClientHeight = System.Math.Max(0, workingArea.Height - chrome.Height);
            var targetWidth = System.Math.Min(preferredSize.Width, maxClientWidth);
            var targetHeight = System.Math.Min(preferredSize.Height, maxClientHeight);
            this.ClientSize = new System.Drawing.Size(targetWidth, targetHeight);

            // Read Size (the outer window size including chrome) *after* setting
            // ClientSize, so the floor is expressed in the same coordinate space the
            // user drags in. Rig Check 3(c) requires that shrinking stops rather
            // than clips; with Form.AutoSize gone, this MinimumSize is what enforces
            // that. 22-RESEARCH.md Pitfall 2's warning about MinimumSize applied to
            // the AutoSize-on case ("MinimumSize/MaximumSize are respected but Size
            // is ignored") does not apply here: AutoSize is off, and this floor is
            // derived from measured content rather than a hand-picked constant, so
            // D-05's content-driven intent is preserved, not overridden.
            //
            // Known, accepted residual: because the window no longer grows itself
            // after this point, a warning label becoming visible later (e.g. a
            // validation error) adds content the fixed window must absorb -- the
            // shared section's AutoSize rows grow and tlpRoot's Percent row 0 gives
            // way, floored by the grids' own MinimumSize. Rig Checks 7 and 9 in Plan
            // 05 are what test this; the user can always drag the window larger.
            this.MinimumSize = this.Size;
        }

        // 12-03/THEME-04: single source of truth for "is dark mode active right now,"
        // read fresh every call (never cached) so it's always correct across live flips
        // -- consumed by ThemeApplier calls at Load and inside OnThemeChanged.
        private bool IsDarkTheme => _themeProvider.CurrentTheme == AppTheme.Dark;

        /// <summary>
        /// 12-03/D-05: live theme-flip handler for SettingsForm's own per-control
        /// theming (dgvMonitors grid + txtHotkey state colors), mirroring
        /// MonitorConfirmDialog/MainForm's marshaled OnThemeChanged pattern (12-02).
        /// WindowsThemeProvider's ThemeChanged may fire off the UI thread -- marshal via
        /// InvokeRequired/BeginInvoke before touching any control. The whole re-theme
        /// body is wrapped in try/catch: theming is cosmetic-only and must never crash
        /// Settings save/load (T-12-02).
        /// </summary>
        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnThemeChanged(sender, e)));
                return;
            }

            try
            {
                // THEME-09/Task 2: derived from the effective theme rather than always
                // following the OS, so a live Windows flip can't drag this dialog's
                // native controls away from a locked override.
                ThemeApplier.ApplyEffectiveColorMode(IsDarkTheme);
                DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDarkTheme);
                ThemeApplier.ThemeMonitorGrid(dgvMonitors, IsDarkTheme);
                ThemeApplier.ThemeMonitorGrid(dgvMonitorsNormal, IsDarkTheme);

                // Re-render whatever txtHotkey state is currently active -- Recording
                // needs its own re-apply (it's not driven by RenderHotkeyIdleDisplay),
                // every other state re-derives correctly from RenderHotkeyIdleDisplay's
                // existing _pendingHotkeyModifiers/_pendingHotkeyKey read.
                if (_recordingHotkey)
                {
                    ThemeApplier.ApplyHotkeyRecording(txtHotkey, IsDarkTheme);
                }
                else
                {
                    RenderHotkeyIdleDisplay();
                }

                // 12-05/CR-02: re-theme all buttons on every live flip.
                ThemeApplier.ThemeButton(btnBrowse, IsDarkTheme);
                ThemeApplier.ThemeButton(btnClearAppPath, IsDarkTheme);
                ThemeApplier.ThemeButton(btnSaveSettings, IsDarkTheme);
                ThemeApplier.ThemeButton(btnDiscardChanges, IsDarkTheme);

                // 12-05/CR-03: re-theme both audio combos on every live flip.
                ThemeApplier.ThemeComboBox(cboAudioNormal, IsDarkTheme);
                ThemeApplier.ThemeComboBox(cboAudioRig, IsDarkTheme);

                Refresh();
            }
            catch
            {
                // Cosmetic-only (T-12-02) -- a theming failure must never crash Settings.
            }
        }

        private void SettingsForm_Load(object? sender, EventArgs e)
        {
            // Re-enumerate on every open — no manual Refresh control exists (D-11).
            _settings = _settingsStore.Load();
            // THEME-09/Task 2: applied first, before the grid theming below, so a
            // dialog opened while an override is active starts in the right mode
            // rather than waiting for a theme event.
            ThemeApplier.ApplyEffectiveColorMode(IsDarkTheme);
            PopulateMonitorGrid();
            PopulateMonitorGridNormal();
            ThemeApplier.ThemeMonitorGrid(dgvMonitors, IsDarkTheme);
            ThemeApplier.ThemeMonitorGrid(dgvMonitorsNormal, IsDarkTheme);

            // 12-05/CR-02: theme all buttons at load time too.
            ThemeApplier.ThemeButton(btnBrowse, IsDarkTheme);
            ThemeApplier.ThemeButton(btnClearAppPath, IsDarkTheme);
            ThemeApplier.ThemeButton(btnSaveSettings, IsDarkTheme);
            ThemeApplier.ThemeButton(btnDiscardChanges, IsDarkTheme);

            PopulateAudioPickers();
            PopulateAppPathField();
            chkEnableDebugLogging.Checked = _settings.EnableDebugLogging;
            chkCloseMinimizesToTray.Checked = _settings.CloseMinimizesToTray;
            chkMinimizeToTray.Checked = _settings.MinimizeToTray;

            // TRIG-01/D-02: no default hotkey is pre-filled — a null pair renders as the
            // Unconfigured idle state below, not a fabricated combo.
            _pendingHotkeyModifiers = _settings.HotkeyModifiers;
            _pendingHotkeyKey = _settings.HotkeyKey;
            _recordingHotkey = false;
            errHotkey.SetError(txtHotkey, string.Empty);
            lblHotkeyWarning.Visible = false;
            RenderHotkeyIdleDisplay();

            // D-05: the HKCU Run key is the single source of truth for autostart state —
            // no AppSettings.StartWithWindows mirror field exists to read instead.
            // WR-01 (code review): guarded like every other Load-time enumeration in this
            // method — a registry read failure (permissions, hive corruption) must degrade
            // the checkbox to unchecked with an inline warning, not crash the dialog on open.
            try
            {
                chkStartWithWindows.Checked = _autostartConfigurator.IsEnabled();
            }
            catch (Exception ex)
            {
                chkStartWithWindows.Checked = false;
                lblAutostartWarning.Text = $"Could not read Start with Windows state: {ex.Message}";
                lblAutostartWarning.Visible = true;
                errAutostart.SetError(chkStartWithWindows, lblAutostartWarning.Text);
            }

            // THEME-09/23-02/D-01: select the radio matching the persisted override
            // (System when null) without triggering a spurious live preview — the guard
            // is set for the duration of the programmatic .Checked writes below and reset
            // in a finally so it can't get stuck true if a handler throws mid-assignment.
            _pendingThemeOverride = _settings.ThemeOverride;
            _updatingThemeRadiosProgrammatically = true;
            try
            {
                rdoThemeSystem.Checked = _pendingThemeOverride is null;
                rdoThemeLight.Checked = _pendingThemeOverride == AppTheme.Light;
                rdoThemeDark.Checked = _pendingThemeOverride == AppTheme.Dark;
            }
            finally
            {
                _updatingThemeRadiosProgrammatically = false;
            }

            ValidateSettingsForm();
        }

        private void OnPickerChanged(object? sender, EventArgs e) => ValidateSettingsForm();

        /// <summary>
        /// THEME-09/23-02/D-01: live-preview handler shared by all three theme radio
        /// buttons. This is the whole of D-01 — it puts the unsaved value into the
        /// shared resolver, which raises ThemeChanged, which every subscriber (this
        /// form, MainForm, and any MonitorConfirmDialog currently open) already handles
        /// through its existing repaint pipeline. No new repaint call, no Refresh(),
        /// no direct ThemeApplier invocation here, and no Save-enablement re-check —
        /// the theme field is never gated by that validation.
        /// </summary>
        private void OnThemeRadioCheckedChanged(object? sender, EventArgs e)
        {
            // Load-time selection (SettingsForm_Load) writes .Checked programmatically —
            // that must never be mistaken for a user click and must never fire a preview.
            if (_updatingThemeRadiosProgrammatically)
            {
                return;
            }

            // CheckedChanged fires twice per user selection in a mutually-exclusive
            // group (once for the option being cleared, once for the option being set) —
            // only the checked one is a real selection.
            if (sender is not RadioButton radio || !radio.Checked)
            {
                return;
            }

            _pendingThemeOverride = radio switch
            {
                _ when ReferenceEquals(radio, rdoThemeLight) => AppTheme.Light,
                _ when ReferenceEquals(radio, rdoThemeDark) => AppTheme.Dark,
                _ => null,
            };

            _previewThemeOverride(_pendingThemeOverride);
        }

        // TRIG-01/D-01, UI-SPEC "Interaction States — txtHotkey": renders the idle
        // (non-Recording) display from the current _pendingHotkeyModifiers/_pendingHotkeyKey
        // pair — Configured (both set) or Unconfigured (either null). Called on Load and
        // whenever a Recording attempt ends without producing a Configured state that
        // needs its own explicit render (capture/Escape branches set their own text).
        // 12-03/Pitfall 8: this method's two branches used to hardcode
        // SystemColors.Window/WindowText/GrayText directly. SystemColors.* does NOT
        // follow Application.SetColorMode, so those literal assignments silently reset
        // this control back to light-mode colors on every idle re-render even after the
        // rest of the form had already themed correctly -- replaced with ThemeApplier
        // calls sourced from the live IThemeProvider.CurrentTheme instead.
        private void RenderHotkeyIdleDisplay()
        {
            if (_pendingHotkeyModifiers is int modifiers && _pendingHotkeyKey is int key)
            {
                txtHotkey.Text = HotkeyFormatter.ToDisplayString(modifiers, key);
                ThemeApplier.ApplyHotkeyIdleConfigured(txtHotkey, IsDarkTheme);
            }
            else
            {
                txtHotkey.Text = "(No hotkey set — click to configure)";
                ThemeApplier.ApplyHotkeyIdleUnconfigured(txtHotkey, IsDarkTheme);
            }
        }

        // UI-SPEC "Recording" state — entered only via an explicit mouse click on
        // txtHotkey (D-01), never via GotFocus alone (see MouseDown wiring in the
        // constructor). SystemColors.Info is the one Accent color use this phase
        // permits (09-UI-SPEC.md Color).
        private void TxtHotkey_MouseDown(object? sender, MouseEventArgs e)
        {
            _recordingHotkey = true;
            txtHotkey.Text = "Press a key combination… (Esc to clear)";
            ThemeApplier.ApplyHotkeyRecording(txtHotkey, IsDarkTheme);
        }

        // Rig checkpoint 09-04 fix: Escape was clearing the field AND closing the dialog
        // (CancelButton = btnDiscardChanges). Root cause — WinForms routes "dialog keys"
        // (Escape, Enter, Tab, arrows) through Form.ProcessDialogKey/CancelButton BEFORE
        // OnKeyDown ever fires, gated by Control.IsInputKey; TxtHotkey_KeyDown's own
        // e.Handled/e.SuppressKeyPress only affect processing AFTER that point, so they
        // could never stop the Escape from also triggering CancelButton. Setting
        // PreviewKeyDownEventArgs.IsInputKey = true while actively recording claims every
        // key (not just Escape — Enter/Tab/arrows are all valid recordable hotkey keys
        // too) as ordinary input, routing it to TxtHotkey_KeyDown instead of dialog-key
        // processing. Guarded on _recordingHotkey so idle-state Escape/Enter/Tab still
        // behave as normal dialog navigation/cancel.
        private void TxtHotkey_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            if (_recordingHotkey)
            {
                e.IsInputKey = true;
            }
        }

        // UI-SPEC "Interaction States — txtHotkey": the capture state machine. Suppresses
        // every key from reaching normal dialog processing while Recording (e.SuppressKeyPress
        // / e.Handled) so a captured key can never double as e.g. Enter-accepts-dialog.
        private void TxtHotkey_KeyDown(object? sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;

            if (!_recordingHotkey)
            {
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                // D-01: Escape always clears, never just "cancels back to the old value."
                _pendingHotkeyModifiers = null;
                _pendingHotkeyKey = null;
                _recordingHotkey = false;
                RenderHotkeyIdleDisplay();
                return;
            }

            if (HotkeyCombo.IsModifierVirtualKey((int)e.KeyCode))
            {
                // D-01: a bare modifier press alone is not accepted — stay in Recording,
                // waiting for a real non-modifier key while the modifier(s) are held.
                return;
            }

            int capturedModifiers = 0;
            if (e.Control)
            {
                capturedModifiers |= HotkeyCombo.ModControl;
            }
            if (e.Alt)
            {
                capturedModifiers |= HotkeyCombo.ModAlt;
            }
            if (e.Shift)
            {
                capturedModifiers |= HotkeyCombo.ModShift;
            }

            if (capturedModifiers == 0)
            {
                // D-01: require at least one modifier held — an unmodified key stays in
                // Recording rather than being accepted as a bare-key "hotkey."
                return;
            }

            _pendingHotkeyModifiers = capturedModifiers;
            _pendingHotkeyKey = (int)e.KeyCode;
            _recordingHotkey = false;

            ThemeApplier.ApplyHotkeyIdleConfigured(txtHotkey, IsDarkTheme);
            txtHotkey.Text = HotkeyFormatter.ToDisplayString(capturedModifiers, (int)e.KeyCode);
        }

        // UI-SPEC "Recording → focus lost without a completed capture or Escape": losing
        // focus mid-Recording is a silent cancel back to whatever was shown before this
        // Recording attempt started — never a clear. Only an explicit Escape clears an
        // existing configured value (see TxtHotkey_KeyDown's Escape branch above).
        private void TxtHotkey_LostFocus(object? sender, EventArgs e)
        {
            if (_recordingHotkey)
            {
                _recordingHotkey = false;
                RenderHotkeyIdleDisplay();
            }
        }

        // D-03: one grid row per monitor from GetAllMonitors() (active + OS-disabled) —
        // NOT GetActiveMonitors(), which structurally cannot show a monitor DISPLAY-05's
        // enable-set needs to select (06-RESEARCH.md Pitfall 1).
        private void PopulateMonitorGrid()
        {
            errMonitor.SetError(dgvMonitors, string.Empty);
            lblMonitorWarning.Visible = false;

            try
            {
                _allMonitors = _monitorController.GetAllMonitors();
            }
            catch (Exception)
            {
                // Defensive: enumeration should not crash Settings open; degrade to empty-state.
                _allMonitors = Array.Empty<MonitorInfo>();
            }

            // Unhook around the bulk Rows.Add/Clear population, matching the existing
            // "unhook around programmatic write" convention (PopulateAudioCombo below) —
            // avoids spurious D-04/ValidateSettingsForm firing mid-populate.
            dgvMonitors.CellValueChanged -= OnMonitorCellValueChanged;
            dgvMonitors.Rows.Clear();

            if (_allMonitors.Count == 0)
            {
                // Grid Spec § Empty state — informational degrade, NOT the red-icon
                // ErrorProvider path (matches how the v1.0 picker's empty-state string
                // was never wrapped in a warning icon either).
                dgvMonitors.Enabled = false;
                lblMonitorWarning.Text = "No displays detected.";
                lblMonitorWarning.Visible = true;
                dgvMonitors.CellValueChanged += OnMonitorCellValueChanged;
                return;
            }

            dgvMonitors.Enabled = true;

            var disableSet = new HashSet<string>(_settings.MonitorsToDisable ?? new List<string>());
            var enableSet = new HashSet<string>(_settings.MonitorsToEnable ?? new List<string>());

            foreach (MonitorInfo monitor in _allMonitors)
            {
                // Copywriting Contract: exactly one suffix (or none) — a monitor can never
                // be both primary and OS-disabled.
                string suffix = monitor.IsPrimary
                    ? " (Primary)"
                    : !monitor.IsActive
                        ? " (currently OS-disabled)"
                        : string.Empty;

                int rowIndex = dgvMonitors.Rows.Add(
                    monitor.FriendlyName + suffix,
                    disableSet.Contains(monitor.DevicePath),
                    enableSet.Contains(monitor.DevicePath));

                // Stable-identity precedent (06-PATTERNS.md Shared Patterns): key every
                // row by DevicePath via Tag, NEVER by row index.
                dgvMonitors.Rows[rowIndex].Tag = monitor.DevicePath;
            }

            dgvMonitors.CellValueChanged += OnMonitorCellValueChanged;

            // Grid Spec § Stale saved-monitor handling (Open Question 3, resolved): a
            // saved device path GetAllMonitors() no longer enumerates at all (physically
            // disconnected — distinct from "currently OS-disabled but still connected",
            // which DOES get a row) has no grid row to show it in. Surface it via a
            // non-blocking warning here; ValidateSettingsForm re-checks this on every
            // interaction so the warning persists/clears appropriately.
            var staleDevicePaths = GetStaleSavedDevicePaths();
            if (staleDevicePaths.Count > 0)
            {
                ShowStaleMonitorWarning(staleDevicePaths.ToList());
            }
        }

        // 16-02: PopulateMonitorGridNormal's self-analog — reuses the ALREADY-populated
        // _allMonitors list from PopulateMonitorGrid() (no second GetAllMonitors() round
        // trip), reading _settings.NormalMonitorsToDisable/NormalMonitorsToEnable instead.
        // Must be called AFTER PopulateMonitorGrid() on every Load.
        private void PopulateMonitorGridNormal()
        {
            errMonitor.SetError(dgvMonitorsNormal, string.Empty);
            lblMonitorNormalWarning.Visible = false;

            dgvMonitorsNormal.CellValueChanged -= OnMonitorNormalCellValueChanged;
            dgvMonitorsNormal.Rows.Clear();

            if (_allMonitors.Count == 0)
            {
                dgvMonitorsNormal.Enabled = false;
                lblMonitorNormalWarning.Text = "No displays detected.";
                lblMonitorNormalWarning.Visible = true;
                dgvMonitorsNormal.CellValueChanged += OnMonitorNormalCellValueChanged;
                return;
            }

            dgvMonitorsNormal.Enabled = true;

            var disableSet = new HashSet<string>(_settings.NormalMonitorsToDisable ?? new List<string>());
            var enableSet = new HashSet<string>(_settings.NormalMonitorsToEnable ?? new List<string>());

            foreach (MonitorInfo monitor in _allMonitors)
            {
                string suffix = monitor.IsPrimary
                    ? " (Primary)"
                    : !monitor.IsActive
                        ? " (currently OS-disabled)"
                        : string.Empty;

                int rowIndex = dgvMonitorsNormal.Rows.Add(
                    monitor.FriendlyName + suffix,
                    disableSet.Contains(monitor.DevicePath),
                    enableSet.Contains(monitor.DevicePath));

                dgvMonitorsNormal.Rows[rowIndex].Tag = monitor.DevicePath;
            }

            dgvMonitorsNormal.CellValueChanged += OnMonitorNormalCellValueChanged;

            var staleDevicePathsNormal = GetStaleSavedDevicePathsNormal();
            if (staleDevicePathsNormal.Count > 0)
            {
                ShowStaleMonitorWarningNormal(staleDevicePathsNormal.ToList());
            }
        }

        // Pitfall 5: a DataGridViewCheckBoxColumn cell doesn't commit its Value until the
        // cell loses focus — force an immediate commit so CellValueChanged fires on the
        // SAME click (required for D-04's single-click mutual exclusivity).
        private void DgvMonitors_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (dgvMonitors.IsCurrentCellDirty && dgvMonitors.CurrentCell is DataGridViewCheckBoxCell)
            {
                dgvMonitors.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        // 16-02: Normal-grid self-analog of DgvMonitors_CurrentCellDirtyStateChanged above.
        private void DgvMonitorsNormal_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (dgvMonitorsNormal.IsCurrentCellDirty && dgvMonitorsNormal.CurrentCell is DataGridViewCheckBoxCell)
            {
                dgvMonitorsNormal.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        // D-04: checking Disable/Enable for a row instantly unchecks the sibling column
        // for that same row — never a two-click round trip, never both-checked.
        private void OnMonitorCellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return; // column-header pseudo-event guard
            }

            if (!_updatingMonitorGridProgrammatically
                && (e.ColumnIndex == colDisable.Index || e.ColumnIndex == colEnable.Index))
            {
                DataGridViewRow row = dgvMonitors.Rows[e.RowIndex];
                bool newValue = row.Cells[e.ColumnIndex].Value is true;

                if (newValue)
                {
                    int siblingIndex = e.ColumnIndex == colDisable.Index ? colEnable.Index : colDisable.Index;

                    // Reentrancy guard (Pitfall 5) — this programmatic write must not
                    // re-trigger this same handler.
                    _updatingMonitorGridProgrammatically = true;
                    try
                    {
                        row.Cells[siblingIndex].Value = false;
                    }
                    finally
                    {
                        _updatingMonitorGridProgrammatically = false;
                    }
                }
            }

            ValidateSettingsForm();
        }

        // 16-02: Normal-grid self-analog of OnMonitorCellValueChanged above — its own
        // independent reentrancy guard (_updatingMonitorGridNormalProgrammatically), never
        // the shared Rig-grid flag, since the two grids are edited independently.
        private void OnMonitorNormalCellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return; // column-header pseudo-event guard
            }

            if (!_updatingMonitorGridNormalProgrammatically
                && (e.ColumnIndex == colDisableNormal.Index || e.ColumnIndex == colEnableNormal.Index))
            {
                DataGridViewRow row = dgvMonitorsNormal.Rows[e.RowIndex];
                bool newValue = row.Cells[e.ColumnIndex].Value is true;

                if (newValue)
                {
                    int siblingIndex = e.ColumnIndex == colDisableNormal.Index ? colEnableNormal.Index : colDisableNormal.Index;

                    _updatingMonitorGridNormalProgrammatically = true;
                    try
                    {
                        row.Cells[siblingIndex].Value = false;
                    }
                    finally
                    {
                        _updatingMonitorGridNormalProgrammatically = false;
                    }
                }
            }

            ValidateSettingsForm();
        }

        // Reads the live grid state into two DevicePath sets — never trusts row index,
        // always the Tag set by PopulateMonitorGrid (06-PATTERNS.md Shared Patterns).
        private (HashSet<string> Disable, HashSet<string> Enable) GetGridSelection()
        {
            var disable = new HashSet<string>();
            var enable = new HashSet<string>();

            foreach (DataGridViewRow row in dgvMonitors.Rows)
            {
                if (row.Tag is not string devicePath)
                {
                    continue;
                }

                if (row.Cells[colDisable.Index].Value is true)
                {
                    disable.Add(devicePath);
                }

                if (row.Cells[colEnable.Index].Value is true)
                {
                    enable.Add(devicePath);
                }
            }

            return (disable, enable);
        }

        // 16-02: Normal-grid self-analog of GetGridSelection above.
        private (HashSet<string> Disable, HashSet<string> Enable) GetGridSelectionNormal()
        {
            var disable = new HashSet<string>();
            var enable = new HashSet<string>();

            foreach (DataGridViewRow row in dgvMonitorsNormal.Rows)
            {
                if (row.Tag is not string devicePath)
                {
                    continue;
                }

                if (row.Cells[colDisableNormal.Index].Value is true)
                {
                    disable.Add(devicePath);
                }

                if (row.Cells[colEnableNormal.Index].Value is true)
                {
                    enable.Add(devicePath);
                }
            }

            return (disable, enable);
        }

        // Saved device paths (either set) that GetAllMonitors() no longer enumerates at
        // all — physically disconnected, not merely OS-disabled-but-connected. Excludes
        // anything the user has already explicitly forgotten this session (see
        // _forgottenStaleDevicePaths) even though it is still physically present in
        // _settings until the next Save actually removes it.
        private HashSet<string> GetStaleSavedDevicePaths()
        {
            var enumeratedPaths = new HashSet<string>(_allMonitors.Select(m => m.DevicePath));
            IEnumerable<string> saved = (_settings.MonitorsToDisable ?? new List<string>())
                .Concat(_settings.MonitorsToEnable ?? new List<string>());
            return new HashSet<string>(saved.Where(p => !enumeratedPaths.Contains(p) && !_forgottenStaleDevicePaths.Contains(p)));
        }

        // 16-02: Normal-grid self-analog of GetStaleSavedDevicePaths above.
        private HashSet<string> GetStaleSavedDevicePathsNormal()
        {
            var enumeratedPaths = new HashSet<string>(_allMonitors.Select(m => m.DevicePath));
            IEnumerable<string> saved = (_settings.NormalMonitorsToDisable ?? new List<string>())
                .Concat(_settings.NormalMonitorsToEnable ?? new List<string>());
            return new HashSet<string>(saved.Where(p => !enumeratedPaths.Contains(p) && !_forgottenStaleDevicePathsNormal.Contains(p)));
        }

        private static string FormatMonitorNames(IEnumerable<string> names) =>
            string.Join(", ", names.Select(n => $"\"{n}\""));

        // Non-blocking (Grid Spec § Stale saved-monitor handling) — deliberately does NOT
        // call errMonitor.SetError/disable Save, unlike the old single-ComboBox stale-pick
        // warning (ShowStaleWarning below). Blocking Save here would prevent the user from
        // saving an unrelated change (e.g. a new audio device) while the rig monitor
        // happens to be merely disconnected/powered off.
        //
        // A genuinely-gone device path (renamed/re-enumerated by Windows, not just
        // unplugged) can never clear on its own — the app has no way to tell "temporarily
        // disconnected" from "permanently gone", so the stale-preserving merge in
        // BtnSaveSettings_Click keeps re-adding it on every Save. The trailing "Forget"
        // link lets the user say explicitly "this one is not coming back" instead of
        // hand-editing settings.json.
        private void ShowStaleMonitorWarning(IReadOnlyList<string> staleDevicePaths)
        {
            string prefix = $"Previously configured monitor(s) not currently detected: {FormatMonitorNames(staleDevicePaths)} — settings preserved; reconnect the display to manage it here. ";
            const string linkText = "Forget these entries";
            lblMonitorWarning.Text = prefix + linkText;
            lblMonitorWarning.LinkArea = new LinkArea(prefix.Length, linkText.Length);
            lblMonitorWarning.Visible = true;
        }

        // 16-02: Normal-grid self-analog of ShowStaleMonitorWarning above.
        private void ShowStaleMonitorWarningNormal(IReadOnlyList<string> staleDevicePaths)
        {
            string prefix = $"Previously configured monitor(s) not currently detected: {FormatMonitorNames(staleDevicePaths)} — settings preserved; reconnect the display to manage it here. ";
            const string linkText = "Forget these entries";
            lblMonitorNormalWarning.Text = prefix + linkText;
            lblMonitorNormalWarning.LinkArea = new LinkArea(prefix.Length, linkText.Length);
            lblMonitorNormalWarning.Visible = true;
        }

        // Marks the currently-shown stale device paths as explicitly forgotten (session-
        // scoped) and hides the warning immediately. The actual removal from
        // MonitorsToDisable/MonitorsToEnable happens in BtnSaveSettings_Click the next
        // time the user clicks Save — Discard still discards it, consistent with every
        // other pending change in this form.
        private void LblMonitorWarning_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            _forgottenStaleDevicePaths.UnionWith(GetStaleSavedDevicePaths());
            lblMonitorWarning.Visible = false;
        }

        // 16-02: Normal-grid self-analog of LblMonitorWarning_LinkClicked above.
        private void LblMonitorNormalWarning_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            _forgottenStaleDevicePathsNormal.UnionWith(GetStaleSavedDevicePathsNormal());
            lblMonitorNormalWarning.Visible = false;
        }

        private void PopulateAudioPickers()
        {
            IReadOnlyList<AudioDeviceInfo> devices;
            try
            {
                devices = _audioController.GetPlaybackDevices();
            }
            catch (Exception)
            {
                devices = Array.Empty<AudioDeviceInfo>();
            }

            // 15-03/D-02: sentinel prepended unconditionally -- even when zero real
            // devices are enumerated -- so "(None — don't switch audio)" is always a
            // real, selectable list entry representing a deliberate choice, distinct
            // from a blank/empty-list state.
            var items = new List<PickerItem> { new(null, "(None — don't switch audio)") };
            items.AddRange(devices.Select(d => new PickerItem(d.Id, d.FriendlyName)));

            PopulateAudioCombo(cboAudioNormal, errAudioNormal, lblAudioNormalWarning, items, _settings.NormalAudioDeviceId);
            PopulateAudioCombo(cboAudioRig, errAudioRig, lblAudioRigWarning, items, _settings.RigAudioDeviceId);
        }

        private void PopulateAudioCombo(ComboBox combo, ErrorProvider errProvider, Label warningLabel, List<PickerItem> items, string? savedId)
        {
            errProvider.SetError(combo, string.Empty);
            warningLabel.Visible = false;

            combo.SelectedIndexChanged -= OnPickerChanged;

            combo.Enabled = true;
            // Fresh List instance per combo (items.ToList()) — binding the exact same
            // list object to two ComboBoxes would share a CurrencyManager position.
            combo.DataSource = items.ToList();
            combo.DisplayMember = nameof(PickerItem.DisplayLabel);
            combo.ValueMember = nameof(PickerItem.Id);
            combo.SelectedIndex = -1;

            if (savedId is not null)
            {
                var match = items.FirstOrDefault(i => i.Id == savedId);
                if (match is not null)
                {
                    combo.SelectedItem = match;
                }
                else
                {
                    ShowStaleWarning(errProvider, combo, warningLabel, "audio device");
                }
            }
            else
            {
                // 15-03/D-02: explicitly select the sentinel rather than leaving
                // SelectedIndex = -1 -- an intentional "None" choice reads as a
                // deliberate selection, not an unfinished form (the exact bug D-02
                // exists to remove: a blank combo previously blocked Save).
                combo.SelectedItem = items.First(i => i.Id is null);
            }

            combo.SelectedIndexChanged += OnPickerChanged;

            // 12-05/CR-03: re-applied every time this combo is (re)populated, after the
            // DataSource rebind above -- covers both the initial SettingsForm_Load call
            // and any future re-population, so the combo never regresses to its stock
            // un-themed appearance.
            ThemeApplier.ThemeComboBox(combo, IsDarkTheme);
        }

        // 15-03: seeds _pendingAppPath from the persisted settings (Load-time only —
        // this must NOT be called again after Clear/Browse/DragDrop, or it would
        // silently overwrite the pending in-memory value with the stale saved one and
        // undo the user's action). Use RenderAppPathDisplay() to re-render after those.
        private void PopulateAppPathField()
        {
            errApp.SetError(txtAppPath, string.Empty);
            lblAppWarning.Visible = false;

            _pendingAppPath = _settings.CompanionAppPath;
            RenderAppPathDisplay();
        }

        // 15-03/D-01/Landmine: pure display concern, driven only by _pendingAppPath —
        // never re-reads _settings.CompanionAppPath (see PopulateAppPathField's
        // comment above for why). txtAppPath.Text is purely a rendered string, never
        // round-tripped back into AppSettings at Save time.
        private void RenderAppPathDisplay()
        {
            if (_pendingAppPath is null)
            {
                // First-ever run or explicitly cleared (Pitfall 3 / D-01): no warning.
                txtAppPath.Text = "No app shortcut or .exe selected";
            }
            else
            {
                txtAppPath.Text = _pendingAppPath;
                if (!IsValidLaunchTarget(_pendingAppPath))
                {
                    // D-10/D-06: previously configured, but no longer resolves — inline
                    // warning; still blocks Save ("broken != unset"). The field itself
                    // can't be "unselected" like a ComboBox; leaving the stale path
                    // visible alongside the warning lets the user see what to fix.
                    ShowStaleWarning(errApp, txtAppPath, lblAppWarning, "target app");
                }
            }

            // D-01: enabled only when a path is currently set.
            btnClearAppPath.Enabled = _pendingAppPath is not null;
        }

        private static void ShowStaleWarning(ErrorProvider errProvider, Control control, Label warningLabel, string noun)
        {
            string message = $"Previously selected {noun} not found — please reselect.";
            errProvider.SetError(control, message);
            warningLabel.Text = message;
            warningLabel.Visible = true;
        }

        // DISPLAY-06/D-05: "will at least one monitor be active once the rig-mode
        // topology is fully applied" — NOT just "is every currently-active monitor in the
        // disable-set" (an enable-set monitor counts as staying active too).
        private static bool WouldLeaveAtLeastOneMonitorActive(
            IReadOnlyList<MonitorInfo> allMonitors,
            HashSet<string> monitorsToDisable,
            HashSet<string> monitorsToEnable)
        {
            bool anySurvivingActiveMonitor = allMonitors
                .Any(m => m.IsActive && !monitorsToDisable.Contains(m.DevicePath));

            return anySurvivingActiveMonitor || monitorsToEnable.Count > 0;
        }

        // Grid Spec § Validation contract — priority order for which single
        // lblMonitorWarning/errMonitor message is shown (only one is visible at a time):
        //   1. DISPLAY-06 gate (highest priority, blocking)
        //   2. D-07 non-empty gate (blocking)
        //   3. Stale-saved-monitor warning (lowest priority, non-blocking)
        //   4. Clear the warning entirely
        private void ValidateSettingsForm()
        {
            bool audioNormalOk = cboAudioNormal.SelectedItem is PickerItem;
            bool audioRigOk = cboAudioRig.SelectedItem is PickerItem;
            // 15-03/D-06: true for cleanly-unset (_pendingAppPath is null) OR
            // set-and-valid; false only for set-and-broken ("broken != unset"). Save is
            // gated on the monitor grid only per D-05 — audio/app being unset never
            // blocks it, but a configured-but-stale value still does.
            bool appPathOk = _pendingAppPath is null || IsValidLaunchTarget(_pendingAppPath);
            bool monitorOk;
            bool rigGridHasSelection = false;

            if (!dgvMonitors.Enabled)
            {
                // Grid Spec § Empty state — "No displays detected." is already set by
                // PopulateMonitorGrid; nothing here should overwrite that message. The
                // "at least one monitor action configured" gate naturally evaluates to
                // false anyway (zero rows means zero selections), which is sufficient to
                // keep Save disabled.
                monitorOk = false;
            }
            else
            {
                var (disableSelected, enableSelected) = GetGridSelection();
                rigGridHasSelection = disableSelected.Count > 0 || enableSelected.Count > 0;

                if (!WouldLeaveAtLeastOneMonitorActive(_allMonitors, disableSelected, enableSelected))
                {
                    lblMonitorWarning.Text = "This configuration would leave no monitor active. At least one monitor must stay enabled after switching to Rig Mode.";
                    lblMonitorWarning.Visible = true;
                    errMonitor.SetError(dgvMonitors, lblMonitorWarning.Text);
                    monitorOk = false;
                }
                else if (disableSelected.Count == 0 && enableSelected.Count == 0)
                {
                    lblMonitorWarning.Text = "Select at least one monitor to disable or enable.";
                    lblMonitorWarning.Visible = true;
                    errMonitor.SetError(dgvMonitors, lblMonitorWarning.Text);
                    monitorOk = false;
                }
                else
                {
                    errMonitor.SetError(dgvMonitors, string.Empty);

                    var staleDevicePaths = GetStaleSavedDevicePaths();
                    if (staleDevicePaths.Count > 0)
                    {
                        // Non-blocking (Grid Spec) — informational only, does not affect monitorOk.
                        ShowStaleMonitorWarning(staleDevicePaths.ToList());
                    }
                    else
                    {
                        lblMonitorWarning.Visible = false;
                    }

                    monitorOk = true;
                }
            }

            // 16-02/D-01: the Normal grid must NOT block Save when both sets are empty —
            // an all-empty Normal config is valid (a monitor not listed is left
            // untouched). No WouldLeaveAtLeastOneMonitorActive cross-check is applied
            // here — that safety guard stays apply-time-only in
            // WindowsMonitorController.DeactivateMonitors (RESEARCH.md Anti-Pattern).
            // lblMonitorNormalWarning is advisory-only (stale-saved-monitor notice, and
            // now also the CR-02 empty-Normal-while-Rig-configured notice below) —
            // never a blocking condition — monitorNormalOk is unconditionally true.
            bool monitorNormalOk = true;

            if (dgvMonitorsNormal.Enabled)
            {
                var (disableSelectedNormal, enableSelectedNormal) = GetGridSelectionNormal();

                var staleDevicePathsNormal = GetStaleSavedDevicePathsNormal();
                if (staleDevicePathsNormal.Count > 0)
                {
                    ShowStaleMonitorWarningNormal(staleDevicePathsNormal.ToList());
                }
                else if (rigGridHasSelection && disableSelectedNormal.Count == 0 && enableSelectedNormal.Count == 0)
                {
                    // CR-02 (code review): D-01 deliberately allows an all-empty Normal
                    // config (nothing to undo — a monitor not listed is left untouched),
                    // but a user who configured the Rig grid and never touched the
                    // Normal grid is far more likely to have simply missed the second
                    // grid than to have intentionally chosen "Normal mode changes
                    // nothing." Advisory only — never blocks Save, matching D-01 exactly.
                    lblMonitorNormalWarning.Text = "No Normal-mode monitors configured — switching to Normal Mode won't change any monitor. If you meant to mirror your Rig-mode choices, configure this grid too.";
                    lblMonitorNormalWarning.Visible = true;
                }
                else
                {
                    lblMonitorNormalWarning.Visible = false;
                }
            }

            btnSaveSettings.Enabled = monitorOk && monitorNormalOk && audioNormalOk && audioRigOk && appPathOk;
        }

        // Accepts any existing .lnk or .exe as the launch target — a .lnk shortcut is
        // stored and later launched verbatim (no resolution here); ShellExecute handles
        // both at launch time (WindowsAppController.LaunchOrFocus).
        private static bool IsValidLaunchTarget(string path)
            => !string.IsNullOrEmpty(path)
               && File.Exists(path)
               && (string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase));

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            // D-06: file-browser dialog filtered to *.lnk/*.exe (Filter configured in the Designer).
            if (dlgOpenExe.ShowDialog(this) == DialogResult.OK)
            {
                errApp.SetError(txtAppPath, string.Empty);
                lblAppWarning.Visible = false;
                _pendingAppPath = dlgOpenExe.FileName;
                txtAppPath.Text = dlgOpenExe.FileName;
                btnClearAppPath.Enabled = true;
                ValidateSettingsForm();
            }
        }

        // 15-03/D-01: explicit Clear handler — the only way to unset the app path,
        // since txtAppPath is ReadOnly. Re-renders via RenderAppPathDisplay() (not
        // PopulateAppPathField(), which would re-read the stale _settings value and
        // undo this clear — see PopulateAppPathField's comment).
        private void BtnClearAppPath_Click(object? sender, EventArgs e)
        {
            _pendingAppPath = null;
            errApp.SetError(txtAppPath, string.Empty);
            lblAppWarning.Visible = false;
            RenderAppPathDisplay();
            ValidateSettingsForm();
        }

        // Drag-and-drop alternative to Browse (redesign: generalized "target app"
        // configuration, .planning/quick/260726-idx-redesign-companion-app-launch-focus-mech).
        // Accept only a single dropped file with a .exe/.lnk extension; anything else
        // (multiple files, wrong extension, non-file drag source) is rejected via
        // DragDropEffects.None (T-idx-02).
        private void AppPath_DragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = TryGetSingleDroppedLaunchTarget(e, out _)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void AppPath_DragDrop(object? sender, DragEventArgs e)
        {
            if (!TryGetSingleDroppedLaunchTarget(e, out string? path))
            {
                return;
            }

            // Store whatever is dropped verbatim as the launch-target path — no .lnk
            // resolution (ShellExecute handles both .lnk and .exe at launch time).
            errApp.SetError(txtAppPath, string.Empty);
            lblAppWarning.Visible = false;
            _pendingAppPath = path;
            txtAppPath.Text = path;
            btnClearAppPath.Enabled = true;
            ValidateSettingsForm();
        }

        private static bool TryGetSingleDroppedLaunchTarget(DragEventArgs e, out string? path)
        {
            path = null;

            if (!e.Data!.GetDataPresent(DataFormats.FileDrop))
            {
                return false;
            }

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } files)
            {
                return false;
            }

            string candidate = files[0];
            string extension = Path.GetExtension(candidate);
            if (!string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            path = candidate;
            return true;
        }

        private void BtnSaveSettings_Click(object? sender, EventArgs e)
        {
            var audioNormalItem = cboAudioNormal.SelectedItem as PickerItem;
            var audioRigItem = cboAudioRig.SelectedItem as PickerItem;
            var (disableSelected, enableSelected) = GetGridSelection();
            var (disableSelectedNormal, enableSelectedNormal) = GetGridSelectionNormal();

            // Defensive guard only — btnSaveSettings.Enabled (ValidateSettingsForm) should
            // make this unreachable via the UI, but never persist a partial/invalid/
            // would-leave-no-monitor-active selection. 15-03/D-06: mirrors
            // ValidateSettingsForm's appPathOk expression exactly — cleanly-unset OR
            // set-and-valid passes, set-and-broken still blocks.
            if (audioNormalItem is null || audioRigItem is null
                || !(_pendingAppPath is null || IsValidLaunchTarget(_pendingAppPath))
                || (disableSelected.Count == 0 && enableSelected.Count == 0)
                || !WouldLeaveAtLeastOneMonitorActive(_allMonitors, disableSelected, enableSelected))
            {
                return;
            }

            // Grid Spec § Stale saved-monitor handling: persisted sets = (previously-saved
            // entries GetAllMonitors() no longer enumerates at all, MINUS anything the user
            // explicitly forgot via the stale-warning's "Forget" link) UNION (currently-
            // checked rows' device paths). Stale/disconnected entries pass through
            // untouched — a temporarily-unplugged rig monitor must not lose its
            // configuration just because Settings was opened and saved for something
            // unrelated (06-UI-SPEC.md, generalizes D-10). The forgotten-paths exclusion is
            // what actually makes "Forget" stick past this Save — without it, a genuinely-
            // gone device path would be re-merged back in here forever.
            var enumeratedPaths = new HashSet<string>(_allMonitors.Select(m => m.DevicePath));
            IEnumerable<string> staleDisable = (_settings.MonitorsToDisable ?? new List<string>())
                .Where(p => !enumeratedPaths.Contains(p) && !_forgottenStaleDevicePaths.Contains(p));
            IEnumerable<string> staleEnable = (_settings.MonitorsToEnable ?? new List<string>())
                .Where(p => !enumeratedPaths.Contains(p) && !_forgottenStaleDevicePaths.Contains(p));

            var mergedDisable = new HashSet<string>(staleDisable);
            mergedDisable.UnionWith(disableSelected);

            var mergedEnable = new HashSet<string>(staleEnable);
            mergedEnable.UnionWith(enableSelected);

            // 16-02: same stale-preserving merge for the Normal grid's sets.
            IEnumerable<string> staleDisableNormal = (_settings.NormalMonitorsToDisable ?? new List<string>())
                .Where(p => !enumeratedPaths.Contains(p) && !_forgottenStaleDevicePathsNormal.Contains(p));
            IEnumerable<string> staleEnableNormal = (_settings.NormalMonitorsToEnable ?? new List<string>())
                .Where(p => !enumeratedPaths.Contains(p) && !_forgottenStaleDevicePathsNormal.Contains(p));

            var mergedDisableNormal = new HashSet<string>(staleDisableNormal);
            mergedDisableNormal.UnionWith(disableSelectedNormal);

            var mergedEnableNormal = new HashSet<string>(staleEnableNormal);
            mergedEnableNormal.UnionWith(enableSelectedNormal);

            // Pitfall 4 / ToggleService.MonitorStateUnchanged precedent: List<string> has
            // no value equality and reordering-sensitive != comparison would mis-detect a
            // genuine change — use order-independent HashSet<string>.SetEquals against
            // both plural sets to decide whether to reset the durable confirmation-skip
            // flag (generalizes the old single-string MonitorDevicePath comparison).
            bool monitorsChanged =
                !new HashSet<string>(_settings.MonitorsToDisable ?? new List<string>()).SetEquals(mergedDisable)
                || !new HashSet<string>(_settings.MonitorsToEnable ?? new List<string>()).SetEquals(mergedEnable);

            var settingsToSave = new AppSettings
            {
                // Legacy fields are migration source only (D-08) — left exactly as loaded,
                // never repopulated from the grid.
                MonitorDevicePath = _settings.MonitorDevicePath,
                MonitorFriendlyName = _settings.MonitorFriendlyName,
                MonitorsToDisable = mergedDisable.ToList(),
                MonitorsToEnable = mergedEnable.ToList(),
                NormalMonitorsToDisable = mergedDisableNormal.ToList(),
                NormalMonitorsToEnable = mergedEnableNormal.ToList(),
                NormalAudioDeviceId = audioNormalItem.Id,
                // 15-REVIEW.md IN-03: the sentinel's DisplayLabel is UI copy
                // ("(None — don't switch audio)"), not a device name — persist null
                // instead of that sentence when the sentinel is selected, so a future
                // reader of settings.json (diagnostics dump, support script, migration)
                // sees a real device name or null, never dialog wording.
                NormalAudioDeviceName = audioNormalItem.Id is null ? null : audioNormalItem.DisplayLabel,
                RigAudioDeviceId = audioRigItem.Id,
                RigAudioDeviceName = audioRigItem.Id is null ? null : audioRigItem.DisplayLabel,
                // 15-03/T-15-04: persist from the dedicated _pendingAppPath field, never
                // from txtAppPath.Text (whose "No app shortcut..." placeholder value
                // would otherwise round-trip into AppSettings as a bogus path).
                CompanionAppPath = _pendingAppPath,
                SkipMonitorConfirmation = monitorsChanged ? false : _settings.SkipMonitorConfirmation,
                EnableDebugLogging = chkEnableDebugLogging.Checked,
                CloseMinimizesToTray = chkCloseMinimizesToTray.Checked,
                MinimizeToTray = chkMinimizeToTray.Checked,
                // TRIG-01/D-05: persist the chosen combo regardless of registration
                // outcome below — the user's chosen combination is the source of truth
                // even if it can't currently be registered (they may be about to close
                // the conflicting app).
                HotkeyModifiers = _pendingHotkeyModifiers,
                HotkeyKey = _pendingHotkeyKey,
                // THEME-09/23-02: null persists System/live-follow, matching the
                // "null = unset" convention every other nullable AppSettings field uses.
                ThemeOverride = _pendingThemeOverride,
            };

            // Persist before the declarative DialogResult.OK closes the dialog.
            // Discard/close requires no handler — CancelButton wiring (constructor)
            // produces DialogResult.Cancel with nothing persisted.
            _settingsStore.Save(settingsToSave);

            // Debug session monitor-enable-reactivates-others-again, round 4: apply the
            // debug-logging checkbox LIVE, the same instant it persists — same slot/reason as
            // _applyTrayVisibility()/_applyThemeOverride() below. Previously this setting only
            // took effect on the NEXT full app restart (Program.cs wired the file trace
            // listener once, at process startup, and never again), which meant enabling this
            // checkbox on an already-running (tray-resident) instance and testing immediately
            // — the exact workflow this app is built for — silently produced zero log output.
            // DebugLog.Configure is idempotent/safe to call unconditionally on every Save, not
            // just when this field actually changed.
            DebugLog.Configure(settingsToSave.EnableDebugLogging);

            // D-08: apply the derived tray-icon visibility live, the moment settings
            // persist — must run here (not gated behind the autostart/hotkey blocks
            // below) so it still executes even if the later hotkey-registration step
            // resets DialogResult to None and keeps the dialog open.
            _applyTrayVisibility();

            // THEME-09/23-02/D-01: apply the just-saved override live, the moment
            // settings persist — same slot, same reason as _applyTrayVisibility() above.
            // After a successful Save this is a no-op (the persisted value already equals
            // the preview); the FormClosed lambda below covers every non-save exit.
            _applyThemeOverride();

            // T-08-LIE: apply the autostart registry write AFTER settings persist
            // succeeds. A failure here must never claim a success that did not happen —
            // revert the checkbox to the actual registry state and surface an inline
            // warning next to it (dedicated errAutostart/lblAutostartWarning pair, not
            // the unrelated App Path section's errApp/lblAppWarning).
            try
            {
                errAutostart.SetError(chkStartWithWindows, string.Empty);
                lblAutostartWarning.Visible = false;

                if (chkStartWithWindows.Checked)
                {
                    _autostartConfigurator.Enable();
                }
                else
                {
                    _autostartConfigurator.Disable();
                }
            }
            catch (Exception ex)
            {
                string message = $"Could not enable Start with Windows: {ex.Message}";
                lblAutostartWarning.Text = message;
                lblAutostartWarning.Visible = true;
                errAutostart.SetError(chkStartWithWindows, message);

                // CR-01 (code review): this recovery read must never itself throw — it
                // exists specifically to guarantee the checkbox never claims a success
                // that did not happen. A second registry failure here (same underlying
                // cause, or unrelated) must degrade to leaving the checkbox as the user
                // left it, not crash the app from inside its own error-recovery path.
                try
                {
                    chkStartWithWindows.Checked = _autostartConfigurator.IsEnabled();
                }
                catch
                {
                    // Best-effort UI sync only — the inline warning above already told
                    // the user the write failed; leaving the checkbox state as-is is
                    // strictly better than crashing.
                }
            }

            // TRIG-01/D-04/D-05: attempt registration of whatever combo was just saved
            // above. Unlike the autostart block, a failure here does NOT roll back the
            // save and does NOT attempt to resync any UI state from an external source —
            // the user's chosen combination (already persisted) is the source of truth
            // regardless of whether it's currently active (they may be about to close
            // the conflicting app). Instead, DialogResult is reset to None so the dialog
            // stays open with the warning visible, letting the user retry ("click Save
            // again") without losing their place. Do NOT "fix" this into a blocking
            // validation that reverts the field or prevents Save — that would contradict
            // D-05's explicit non-blocking-Save decision.
            errHotkey.SetError(txtHotkey, string.Empty);
            lblHotkeyWarning.Visible = false;

            if (!_tryRegisterConfiguredHotkey())
            {
                string message = "Could not register hotkey — it may already be in use by another application. Choose a different combination or close the conflicting app, then click Save again.";
                lblHotkeyWarning.Text = message;
                lblHotkeyWarning.Visible = true;
                errHotkey.SetError(txtHotkey, message);
                this.DialogResult = DialogResult.None;
            }
        }
    }
}
