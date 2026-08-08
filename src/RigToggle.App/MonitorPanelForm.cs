using System.Drawing;
using System.Linq;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Windows;

namespace RigToggle.App
{
    /// <summary>
    /// PANEL-01/02/03/04/05: the live manual monitor panel. Non-modal (Show(), never
    /// ShowDialog()), backed directly by IMonitorController -- never routes through
    /// ToggleService or ToggleOrchestrator.ToggleToRigMode/ToggleToNormalMode (PANEL-02).
    /// DISPLAY-12's zero-survivors guard is deliberately NOT duplicated here: every
    /// Disable action calls IMonitorController.DeactivateMonitors directly, the exact
    /// same method both toggle directions already call, so the guard living solely in
    /// WindowsMonitorController.DeactivateMonitors applies unchanged. Panel actions are
    /// ad-hoc and session-local -- they never persist into AppSettings.MonitorsToDisable/
    /// NormalMonitorsToDisable/MonitorsToEnable/NormalMonitorsToEnable or IModeStore; the
    /// single permitted settings write is SkipMonitorConfirmation from the confirm
    /// dialog's "don't ask again" checkbox (PANEL-04).
    /// </summary>
    public partial class MonitorPanelForm : Form
    {
        private readonly IMonitorController _monitorController;
        private readonly ISettingsStore _settingsStore;
        private readonly IThemeProvider _themeProvider;
        private readonly ToggleOrchestrator _orchestrator;
        private readonly Bitmap _dotActive;
        private readonly Bitmap _dotInactive;
        private IReadOnlyList<MonitorInfo> _allMonitors = Array.Empty<MonitorInfo>();

        public MonitorPanelForm(IMonitorController monitorController, ISettingsStore settingsStore, IThemeProvider themeProvider, ToggleOrchestrator orchestrator)
        {
            _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));

            InitializeComponent();

            // Built ONCE and shared across all rows and all refreshes.
            // PopulateMonitorGrid runs on every hotplug notification (PANEL-03) --
            // allocating a fresh Bitmap per row per refresh would leak GDI handles over
            // a long session (the same class of concern as MainForm's
            // LoadTrayIconsIfNeeded comment about Icon handles, T-17-10).
            _dotActive = CreateStatusDot(true);
            _dotInactive = CreateStatusDot(false);

            // This panel is non-modal and potentially long-lived (unlike SettingsForm/
            // MonitorConfirmDialog's fresh-per-open `using var ... ShowDialog()`
            // idiom), so both subscriptions are unsubscribed on FormClosed here; the
            // Designer's Dispose(bool) is the backstop, not the substitute (WR-01
            // pattern, same reasoning as MonitorConfirmDialog).
            _themeProvider.ThemeChanged += OnThemeChanged;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            this.FormClosed += (_, _) =>
            {
                _themeProvider.ThemeChanged -= OnThemeChanged;
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            };

            DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDarkTheme);
            ThemeApplier.ThemeMonitorGrid(dgvMonitorPanel, IsDarkTheme);
            ThemeApplier.ThemeButton(btnIdentify, IsDarkTheme);

            PopulateMonitorGrid();
        }

        // Fresh read, never cached (matches SettingsForm.IsDarkTheme / MainForm.IsDark)
        // so DWM chrome + control theming stay correct across live flips.
        private bool IsDarkTheme => _themeProvider.CurrentTheme == AppTheme.Dark;

        // 17-UI-SPEC.md Color: these two literals are the locked Status colors and are
        // theme-independent -- deliberately takes no isDark parameter. A future editor
        // should not add one; neither literal changes on a theme flip.
        private static Bitmap CreateStatusDot(bool isActive)
        {
            var bmp = new Bitmap(12, 12);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Color dotColor = isActive ? Color.FromArgb(46, 204, 113) : Color.FromArgb(200, 60, 60);
            using var brush = new SolidBrush(dotColor);
            g.FillEllipse(brush, 0, 0, 11, 11);
            return bmp;
        }

        // D-03/PANEL-01 analog: one row per monitor from GetAllMonitors() (active AND
        // OS-disabled), never GetActiveMonitors() (structurally cannot show a monitor
        // the Enable action needs to select).
        private void PopulateMonitorGrid()
        {
            try
            {
                _allMonitors = _monitorController.GetAllMonitors();
            }
            catch (Exception ex)
            {
                // Defensive: enumeration must never crash the panel -- degrade to
                // empty-state and leave a diagnostic trail (WR-02 convention: every
                // swallowed failure in this codebase leaves a trace).
                System.Diagnostics.Trace.WriteLine($"MonitorPanelForm.PopulateMonitorGrid: GetAllMonitors failed: {ex}");
                _allMonitors = Array.Empty<MonitorInfo>();
            }

            dgvMonitorPanel.Rows.Clear();

            if (_allMonitors.Count == 0)
            {
                dgvMonitorPanel.Visible = false;
                lblEmptyState.Visible = true;
                btnIdentify.Enabled = false;
                return;
            }

            dgvMonitorPanel.Visible = true;
            lblEmptyState.Visible = false;
            btnIdentify.Enabled = true;

            foreach (MonitorInfo monitor in _allMonitors)
            {
                // Exact same suffix expression SettingsForm.PopulateMonitorGrid uses --
                // a monitor can never be both primary and OS-disabled.
                string suffix = monitor.IsPrimary
                    ? " (Primary)"
                    : !monitor.IsActive
                        ? " (currently OS-disabled)"
                        : string.Empty;

                int rowIndex = dgvMonitorPanel.Rows.Add(
                    monitor.IsActive ? _dotActive : _dotInactive,
                    monitor.FriendlyName + suffix,
                    monitor.IsActive ? "Disable" : "Enable"); // 17-UI-SPEC.md Copywriting Contract

                // Stable-identity precedent (06-PATTERNS.md Shared Patterns, reused
                // every grid in this app): key every row by DevicePath via Tag, NEVER
                // by row index or display-name matching.
                dgvMonitorPanel.Rows[rowIndex].Tag = monitor.DevicePath;
            }
        }

        // PANEL-03: live hotplug refresh. This event also fires for the panel's OWN
        // Activate/Deactivate calls (17-RESEARCH.md Pitfall 1) -- a panel-initiated
        // change therefore re-renders twice (once from the explicit post-action
        // refresh, once from here). That double-fire is harmless and intentional --
        // do not add debounce or suppression logic, and keep this handler
        // side-effect-free beyond re-populating the grid.
        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnDisplaySettingsChanged(sender, e)));
                return;
            }

            try
            {
                PopulateMonitorGrid();
            }
            catch
            {
                // A hotplug notification must never crash the panel.
            }
        }

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
                DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDarkTheme);
                ThemeApplier.ThemeMonitorGrid(dgvMonitorPanel, IsDarkTheme);
                ThemeApplier.ThemeButton(btnIdentify, IsDarkTheme);
                Refresh();
            }
            catch
            {
                // Cosmetic-only -- a theming failure must never crash the panel.
            }
        }

        private void DgvMonitorPanel_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            // Implemented in 17-02 Task 3.
        }

        private void BtnIdentify_Click(object? sender, EventArgs e)
        {
            // Implemented in 17-02 Task 3.
        }
    }
}
