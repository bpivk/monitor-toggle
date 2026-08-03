using RigToggle.Core;
using RigToggle.Core.Abstractions;
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

        private AppSettings _settings = new();

        // TRIG-01/D-01: the working (not-yet-saved) hotkey combo, initialized from
        // _settings on Load and mutated only by the capture state machine below. Null
        // means "no hotkey configured" (D-02 — no default is pre-filled).
        private int? _pendingHotkeyModifiers;
        private int? _pendingHotkeyKey;

        // Reentrancy/mode-tracking guard for the txtHotkey capture state machine —
        // mirrors the _updatingMonitorGridProgrammatically boolean-flag idiom already
        // established for the monitor grid's own programmatic-write guard.
        private bool _recordingHotkey;

        // Enumerated (active + OS-disabled) monitors backing the grid — cached from the
        // last PopulateMonitorGrid() call so validation/save can re-read it without a
        // second GetAllMonitors() round trip mid-interaction (D-03/D-04/D-05).
        private IReadOnlyList<MonitorInfo> _allMonitors = Array.Empty<MonitorInfo>();

        // Reentrancy guard around the D-04 programmatic sibling-checkbox write — without
        // this, unchecking the sibling column would itself re-fire CellValueChanged
        // (06-UI-SPEC.md Grid Spec § D-04 mechanism, RESEARCH.md Pitfall 5).
        private bool _updatingMonitorGridProgrammatically;

        /// <summary>
        /// Display/value wrapper for ComboBox binding (DisplayMember/ValueMember) —
        /// 02-RESEARCH.md Pattern 2.
        /// </summary>
        private sealed record PickerItem(string Id, string DisplayLabel);

        // 12-02: ctor param + field only in this plan -- SettingsForm's own
        // subscribe/OnThemeChanged/per-control theming lands in plan 12-03. Threaded
        // here so the composition root (Program.cs SettingsFormFactory) compiles.
        public SettingsForm(IMonitorController monitorController, IAudioController audioController, ISettingsStore settingsStore, IAutostartConfigurator autostartConfigurator, IThemeProvider themeProvider, Func<bool> tryRegisterConfiguredHotkey, Action applyTrayVisibility)
        {
            _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
            _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _autostartConfigurator = autostartConfigurator ?? throw new ArgumentNullException(nameof(autostartConfigurator));
            _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));
            _tryRegisterConfiguredHotkey = tryRegisterConfiguredHotkey ?? throw new ArgumentNullException(nameof(tryRegisterConfiguredHotkey));
            _applyTrayVisibility = applyTrayVisibility ?? throw new ArgumentNullException(nameof(applyTrayVisibility));

            InitializeComponent();

            // 12-03/D-05: live theme-follow, mirroring MonitorConfirmDialog's (12-02)
            // marshal-then-try/catch pattern. SettingsForm is transient (fresh-per-open
            // via `using var settingsForm = ...ShowDialog()`, see OpenSettingsDialog in
            // MainForm.cs) while WindowsThemeProvider is an app-lifetime singleton --
            // FormClosed unsubscribe (not Dispose) is REQUIRED here or every dialog open
            // leaks a handler onto the outliving provider (T-12-05).
            _themeProvider.ThemeChanged += OnThemeChanged;
            this.FormClosed += (_, _) => _themeProvider.ThemeChanged -= OnThemeChanged;

            // 12-03/THEME-06: this dialog is always shown immediately via ShowDialog
            // (never hidden-tray-started like MainForm), so no --tray-safe timing
            // concern applies -- applying DWM chrome right here, post-InitializeComponent
            // (Handle already exists by this point), is sufficient. DwmSetWindowAttribute
            // is declared to return an HRESULT and never throws (D-07) -- no try/catch
            // needed around this specific call, matching MonitorConfirmDialog's precedent.
            DwmTitleBar.ApplyRoundedCornersAndMica(Handle);

            // Esc / system close box (X) both produce DialogResult.Cancel via the
            // declarative Discard button — no extra FormClosing handler needed
            // (02-RESEARCH.md Pattern 5).
            this.AcceptButton = btnSaveSettings;
            this.CancelButton = btnDiscardChanges;

            this.Load += SettingsForm_Load;
            dgvMonitors.CurrentCellDirtyStateChanged += DgvMonitors_CurrentCellDirtyStateChanged;
            dgvMonitors.CellValueChanged += OnMonitorCellValueChanged;
            cboAudioNormal.SelectedIndexChanged += OnPickerChanged;
            cboAudioRig.SelectedIndexChanged += OnPickerChanged;

            // D-01: capture mode must only ever begin via an explicit mouse click, never
            // via GotFocus alone (UI-SPEC Interaction States) — MouseDown fires before
            // any focus-change side effects, matching the UI-SPEC's own wording.
            txtHotkey.MouseDown += TxtHotkey_MouseDown;
            txtHotkey.PreviewKeyDown += TxtHotkey_PreviewKeyDown;
            txtHotkey.KeyDown += TxtHotkey_KeyDown;
            txtHotkey.LostFocus += TxtHotkey_LostFocus;
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
                System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);
                DwmTitleBar.ApplyRoundedCornersAndMica(Handle);
                ThemeApplier.ThemeMonitorGrid(dgvMonitors, IsDarkTheme);

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
            PopulateMonitorGrid();
            ThemeApplier.ThemeMonitorGrid(dgvMonitors, IsDarkTheme);
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

            ValidateSettingsForm();
        }

        private void OnPickerChanged(object? sender, EventArgs e) => ValidateSettingsForm();

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

        // Saved device paths (either set) that GetAllMonitors() no longer enumerates at
        // all — physically disconnected, not merely OS-disabled-but-connected.
        private HashSet<string> GetStaleSavedDevicePaths()
        {
            var enumeratedPaths = new HashSet<string>(_allMonitors.Select(m => m.DevicePath));
            IEnumerable<string> saved = (_settings.MonitorsToDisable ?? new List<string>())
                .Concat(_settings.MonitorsToEnable ?? new List<string>());
            return new HashSet<string>(saved.Where(p => !enumeratedPaths.Contains(p)));
        }

        private static string FormatMonitorNames(IEnumerable<string> names) =>
            string.Join(", ", names.Select(n => $"\"{n}\""));

        // Non-blocking (Grid Spec § Stale saved-monitor handling) — deliberately does NOT
        // call errMonitor.SetError/disable Save, unlike the old single-ComboBox stale-pick
        // warning (ShowStaleWarning below). Blocking Save here would prevent the user from
        // saving an unrelated change (e.g. a new audio device) while the rig monitor
        // happens to be merely disconnected/powered off.
        private void ShowStaleMonitorWarning(IReadOnlyList<string> staleDevicePaths)
        {
            lblMonitorWarning.Text =
                $"Previously configured monitor(s) not currently detected: {FormatMonitorNames(staleDevicePaths)} — settings preserved; reconnect the display to manage it here.";
            lblMonitorWarning.Visible = true;
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

            var items = devices.Select(d => new PickerItem(d.Id, d.FriendlyName)).ToList();

            PopulateAudioCombo(cboAudioNormal, errAudioNormal, lblAudioNormalWarning, items, _settings.NormalAudioDeviceId);
            PopulateAudioCombo(cboAudioRig, errAudioRig, lblAudioRigWarning, items, _settings.RigAudioDeviceId);
        }

        private void PopulateAudioCombo(ComboBox combo, ErrorProvider errProvider, Label warningLabel, List<PickerItem> items, string? savedId)
        {
            errProvider.SetError(combo, string.Empty);
            warningLabel.Visible = false;

            combo.SelectedIndexChanged -= OnPickerChanged;

            if (items.Count == 0)
            {
                combo.DataSource = null;
                combo.Items.Clear();
                combo.Items.Add("No audio devices detected.");
                combo.SelectedIndex = -1;
                combo.Enabled = false;
            }
            else
            {
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
            }

            combo.SelectedIndexChanged += OnPickerChanged;
        }

        private void PopulateAppPathField()
        {
            errApp.SetError(txtAppPath, string.Empty);
            lblAppWarning.Visible = false;

            string? savedPath = _settings.CompanionAppPath;
            if (savedPath is null)
            {
                // First-ever run: no warning (Pitfall 3).
                txtAppPath.Text = "No app shortcut or .exe selected";
            }
            else
            {
                txtAppPath.Text = savedPath;
                if (!IsValidLaunchTarget(savedPath))
                {
                    // D-10: previously configured, but no longer resolves — inline warning.
                    // The field itself can't be "unselected" like a ComboBox; leaving the
                    // stale path visible alongside the warning lets the user see what to fix.
                    ShowStaleWarning(errApp, txtAppPath, lblAppWarning, "target app");
                }
            }
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
            bool appPathOk = IsValidLaunchTarget(txtAppPath.Text);
            bool monitorOk;

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

            btnSaveSettings.Enabled = monitorOk && audioNormalOk && audioRigOk && appPathOk;
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
                txtAppPath.Text = dlgOpenExe.FileName;
                ValidateSettingsForm();
            }
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
            txtAppPath.Text = path;
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

            // Defensive guard only — btnSaveSettings.Enabled (ValidateSettingsForm) should
            // make this unreachable via the UI, but never persist a partial/invalid/
            // would-leave-no-monitor-active selection.
            if (audioNormalItem is null || audioRigItem is null || !IsValidLaunchTarget(txtAppPath.Text)
                || (disableSelected.Count == 0 && enableSelected.Count == 0)
                || !WouldLeaveAtLeastOneMonitorActive(_allMonitors, disableSelected, enableSelected))
            {
                return;
            }

            // Grid Spec § Stale saved-monitor handling: persisted sets = (previously-saved
            // entries GetAllMonitors() no longer enumerates at all) UNION (currently-
            // checked rows' device paths). Stale/disconnected entries pass through
            // untouched — a temporarily-unplugged rig monitor must not lose its
            // configuration just because Settings was opened and saved for something
            // unrelated (06-UI-SPEC.md, generalizes D-10).
            var enumeratedPaths = new HashSet<string>(_allMonitors.Select(m => m.DevicePath));
            IEnumerable<string> staleDisable = (_settings.MonitorsToDisable ?? new List<string>())
                .Where(p => !enumeratedPaths.Contains(p));
            IEnumerable<string> staleEnable = (_settings.MonitorsToEnable ?? new List<string>())
                .Where(p => !enumeratedPaths.Contains(p));

            var mergedDisable = new HashSet<string>(staleDisable);
            mergedDisable.UnionWith(disableSelected);

            var mergedEnable = new HashSet<string>(staleEnable);
            mergedEnable.UnionWith(enableSelected);

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
                NormalAudioDeviceId = audioNormalItem.Id,
                NormalAudioDeviceName = audioNormalItem.DisplayLabel,
                RigAudioDeviceId = audioRigItem.Id,
                RigAudioDeviceName = audioRigItem.DisplayLabel,
                CompanionAppPath = txtAppPath.Text,
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
            };

            // Persist before the declarative DialogResult.OK closes the dialog.
            // Discard/close requires no handler — CancelButton wiring (constructor)
            // produces DialogResult.Cancel with nothing persisted.
            _settingsStore.Save(settingsToSave);

            // D-08: apply the derived tray-icon visibility live, the moment settings
            // persist — must run here (not gated behind the autostart/hotkey blocks
            // below) so it still executes even if the later hotkey-registration step
            // resets DialogResult to None and keeps the dialog open.
            _applyTrayVisibility();

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
