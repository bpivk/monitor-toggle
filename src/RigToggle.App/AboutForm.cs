using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Windows;

namespace RigToggle.App
{
    /// <summary>
    /// UPDATE-06/quick-260829-fnt: the Help &gt; About dialog -- a third, more
    /// discoverable manual "Check for Updates" entry point alongside the tray item
    /// and the Settings button, plus a version readout. Follows
    /// UpdatePromptDialog's exact structural precedent (themed Form constructed
    /// with themeProvider, never bare native modal chrome, ARCHITECTURE.md
    /// Anti-Pattern 3): ctor theming block, ThemeChanged subscribe + FormClosed
    /// unsubscribe + Dispose backstop, IsDark property, marshalled OnThemeChanged.
    ///
    /// The Check for Updates button invokes <paramref name="performManualUpdateCheck"/>
    /// (i.e. MainForm.PerformManualUpdateCheck) directly -- never a re-implementation
    /// of the update-check body -- so it inherits the _updateCheckInProgress
    /// reentrancy guard that already protects the tray and Settings entry points.
    /// </summary>
    public partial class AboutForm : Form
    {
        private readonly IThemeProvider _themeProvider;

        public AboutForm(string versionText, IThemeProvider themeProvider, Action? performManualUpdateCheck)
        {
            if (versionText is null)
            {
                throw new ArgumentNullException(nameof(versionText));
            }

            _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));

            InitializeComponent();

            lblVersion.Text = $"Version {versionText}";

            // A null delegate means "no update orchestrator is wired" (e.g. a test
            // harness constructing MainForm without one) -- disable rather than
            // ship a button that silently does nothing on click.
            if (performManualUpdateCheck is null)
            {
                btnCheckForUpdates.Enabled = false;
            }
            else
            {
                btnCheckForUpdates.Click += (_, _) => performManualUpdateCheck();
            }

            this.AcceptButton = btnClose;
            this.CancelButton = btnClose;

            // Same transient-dialog lifecycle as UpdatePromptDialog: constructed
            // fresh via `using var ... ShowDialog()` on every open, so unsubscribe
            // on FormClosed (not Dispose-time) is the correct hook here.
            _themeProvider.ThemeChanged += OnThemeChanged;
            this.FormClosed += (_, _) => _themeProvider.ThemeChanged -= OnThemeChanged;

            ThemeApplier.ApplyEffectiveColorMode(IsDark);
            DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
            ThemeApplier.ThemeButton(btnCheckForUpdates, IsDark);
            ThemeApplier.ThemeButton(btnClose, IsDark);
        }

        // Single source of truth for "is dark mode active right now," read fresh
        // every call -- mirrors UpdatePromptDialog.IsDark/MainForm.IsDark.
        private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;

        /// <summary>
        /// Live theme-flip handler, same marshal-then-try/catch pattern as
        /// UpdatePromptDialog.OnThemeChanged -- ThemeChanged may fire off the UI
        /// thread.
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
                ThemeApplier.ApplyEffectiveColorMode(IsDark);
                DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
                ThemeApplier.ThemeButton(btnCheckForUpdates, IsDark);
                ThemeApplier.ThemeButton(btnClose, IsDark);
                Refresh();
            }
            catch
            {
                // Cosmetic-only -- a theming failure must never crash the About dialog.
            }
        }
    }
}
