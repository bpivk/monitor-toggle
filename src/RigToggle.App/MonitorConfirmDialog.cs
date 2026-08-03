using System.Collections.Generic;
using System.Linq;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Windows;

namespace RigToggle.App
{
    /// <summary>
    /// DISPLAY-07/D-06 safety confirmation: shown before any configured monitor
    /// disable/enable is applied, naming every monitor in both sets by friendly
    /// name (full, comma-separated, never truncated). Previously pure display data
    /// with no Core interface injected (04-RESEARCH.md Pattern 5) — 12-02/THEME-01/03
    /// is the FIRST time this form's constructor gains an interface dependency, to
    /// support launch-time + live-follow theming (title bar, controls, flat buttons)
    /// matching MainForm. The caller (MainForm) still resolves both sets' friendly
    /// names via IMonitorController.GetAllMonitors() before constructing this (an
    /// enable-set monitor is inactive at confirm-time, so GetActiveMonitors() cannot
    /// resolve it) — that part of the contract is unchanged.
    /// </summary>
    public partial class MonitorConfirmDialog : Form
    {
        private readonly IThemeProvider _themeProvider;

        public bool DontAskAgain => chkDontAskAgain.Checked;

        public MonitorConfirmDialog(IReadOnlyList<string> disableNames, IReadOnlyList<string> enableNames, IThemeProvider themeProvider)
        {
            _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));

            InitializeComponent();

            var clauses = new List<string>();
            if (disableNames.Count > 0) clauses.Add($"disable {FormatNames(disableNames)}");
            if (enableNames.Count > 0) clauses.Add($"enable {FormatNames(enableNames)}");
            lblMessage.Text = $"This will {string.Join(" and ", clauses)}. Continue?";

            this.AcceptButton = btnContinue;
            this.CancelButton = btnCancel;

            // 12-02/D-05: this dialog is transient (constructed fresh via `using var ...
            // ShowDialog()` on every confirm), so it MUST unsubscribe on close -- unlike
            // MainForm's Dispose-time unsubscribe, FormClosed is the right lifecycle hook
            // here since Dispose may run well after the dialog has logically finished.
            _themeProvider.ThemeChanged += OnThemeChanged;
            this.FormClosed += (_, _) => _themeProvider.ThemeChanged -= OnThemeChanged;

            // 12-02/THEME-06: this dialog is always shown immediately via ShowDialog
            // (never hidden-tray-started like MainForm), so no --tray-safe timing
            // concern applies -- applying DWM chrome right here, post-InitializeComponent
            // (Handle already exists by this point), is sufficient.
            DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
        }

        // 12-05/CR-01: single source of truth for "is dark mode active right now,"
        // read fresh every call (never cached) -- mirrors SettingsForm.IsDarkTheme /
        // MainForm.IsDark.
        private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;

        /// <summary>
        /// 12-02/D-05: live theme-flip handler, same marshal-then-try/catch pattern as
        /// MainForm.OnThemeChanged (T-12-02) -- ThemeChanged may fire off the UI thread.
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
                DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
                Refresh();
            }
            catch
            {
                // Cosmetic-only (T-12-02) -- a theming failure must never crash the confirm flow.
            }
        }

        private static string FormatNames(IReadOnlyList<string> names) =>
            string.Join(", ", names.Select(n => $"\"{n}\""));
    }
}
