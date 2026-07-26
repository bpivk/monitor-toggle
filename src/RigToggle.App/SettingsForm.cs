using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

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

        private AppSettings _settings = new();

        /// <summary>
        /// Display/value wrapper for ComboBox binding (DisplayMember/ValueMember) —
        /// 02-RESEARCH.md Pattern 2.
        /// </summary>
        private sealed record PickerItem(string Id, string DisplayLabel);

        public SettingsForm(IMonitorController monitorController, IAudioController audioController, ISettingsStore settingsStore)
        {
            _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
            _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

            InitializeComponent();

            // Esc / system close box (X) both produce DialogResult.Cancel via the
            // declarative Discard button — no extra FormClosing handler needed
            // (02-RESEARCH.md Pattern 5).
            this.AcceptButton = btnSaveSettings;
            this.CancelButton = btnDiscardChanges;

            this.Load += SettingsForm_Load;
            cboMonitor.SelectedIndexChanged += OnPickerChanged;
            cboAudioNormal.SelectedIndexChanged += OnPickerChanged;
            cboAudioRig.SelectedIndexChanged += OnPickerChanged;
        }

        private void SettingsForm_Load(object? sender, EventArgs e)
        {
            // Re-enumerate on every open — no manual Refresh control exists (D-11).
            _settings = _settingsStore.Load();
            PopulateMonitorPicker();
            PopulateAudioPickers();
            PopulateAppPathField();
            ValidateSettingsForm();
        }

        private void OnPickerChanged(object? sender, EventArgs e) => ValidateSettingsForm();

        private void PopulateMonitorPicker()
        {
            errMonitor.SetError(cboMonitor, string.Empty);
            lblMonitorWarning.Visible = false;

            IReadOnlyList<MonitorInfo> monitors;
            try
            {
                monitors = _monitorController.GetActiveMonitors();
            }
            catch (Exception)
            {
                // Defensive: enumeration should not crash Settings open; degrade to empty-state.
                monitors = Array.Empty<MonitorInfo>();
            }

            var items = monitors
                .Select(m => new PickerItem(m.DevicePath, m.IsPrimary ? $"{m.FriendlyName} (Primary)" : m.FriendlyName))
                .ToList();

            // Pitfall 1: unhook SelectedIndexChanged around DataSource assignment to avoid
            // a spurious change event firing mid-populate.
            cboMonitor.SelectedIndexChanged -= OnPickerChanged;

            if (items.Count == 0)
            {
                cboMonitor.DataSource = null;
                cboMonitor.Items.Clear();
                cboMonitor.Items.Add("No displays detected.");
                cboMonitor.SelectedIndex = -1;
                cboMonitor.Enabled = false;
            }
            else
            {
                cboMonitor.Enabled = true;
                cboMonitor.DataSource = items;
                cboMonitor.DisplayMember = nameof(PickerItem.DisplayLabel);
                cboMonitor.ValueMember = nameof(PickerItem.Id);
                cboMonitor.SelectedIndex = -1;

                string? savedId = _settings.MonitorDevicePath;
                if (savedId is not null)
                {
                    var match = items.FirstOrDefault(i => i.Id == savedId);
                    if (match is not null)
                    {
                        cboMonitor.SelectedItem = match;
                    }
                    else
                    {
                        // D-10: saved-but-not-found — unselected + inline warning.
                        // (savedId is null branch above is the distinct first-run case — no warning, Pitfall 3.)
                        ShowStaleWarning(errMonitor, cboMonitor, lblMonitorWarning, "monitor");
                    }
                }
            }

            cboMonitor.SelectedIndexChanged += OnPickerChanged;
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

        private void ValidateSettingsForm()
        {
            bool monitorOk = cboMonitor.SelectedItem is PickerItem;
            bool audioNormalOk = cboAudioNormal.SelectedItem is PickerItem;
            bool audioRigOk = cboAudioRig.SelectedItem is PickerItem;
            bool appPathOk = IsValidLaunchTarget(txtAppPath.Text);

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
            var monitorItem = cboMonitor.SelectedItem as PickerItem;
            var audioNormalItem = cboAudioNormal.SelectedItem as PickerItem;
            var audioRigItem = cboAudioRig.SelectedItem as PickerItem;

            // Defensive guard only — btnSaveSettings.Enabled (D-12) should make this
            // unreachable via the UI, but never persist a partial/invalid selection.
            if (monitorItem is null || audioNormalItem is null || audioRigItem is null || !IsValidLaunchTarget(txtAppPath.Text))
            {
                return;
            }

            // D-02: reset the durable confirmation-skip flag whenever the configured
            // monitor changes, so a fresh named confirmation is forced for the new
            // display; preserve the prior value when the monitor is unchanged.
            bool monitorChanged = _settings.MonitorDevicePath != monitorItem.Id;

            // MonitorFriendlyName is documented display-cache only — store the raw
            // FriendlyName, not monitorItem.DisplayLabel, which carries the ComboBox's
            // rendered "(Primary)" suffix and would permanently read "... (Primary)"
            // even after the monitor stops being primary. Re-resolve from the live
            // controller rather than trusting the picker's rendered label.
            string rawMonitorFriendlyName = _monitorController.GetActiveMonitors()
                .FirstOrDefault(m => m.DevicePath == monitorItem.Id)?.FriendlyName
                ?? monitorItem.DisplayLabel;

            var settingsToSave = new AppSettings
            {
                MonitorDevicePath = monitorItem.Id,
                MonitorFriendlyName = rawMonitorFriendlyName,
                NormalAudioDeviceId = audioNormalItem.Id,
                NormalAudioDeviceName = audioNormalItem.DisplayLabel,
                RigAudioDeviceId = audioRigItem.Id,
                RigAudioDeviceName = audioRigItem.DisplayLabel,
                CompanionAppPath = txtAppPath.Text,
                SkipMonitorConfirmation = monitorChanged ? false : _settings.SkipMonitorConfirmation,
            };

            // Persist before the declarative DialogResult.OK closes the dialog.
            // Discard/close requires no handler — CancelButton wiring (constructor)
            // produces DialogResult.Cancel with nothing persisted.
            _settingsStore.Save(settingsToSave);
        }
    }
}
