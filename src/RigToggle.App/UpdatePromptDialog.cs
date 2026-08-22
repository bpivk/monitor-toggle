using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Windows;

namespace RigToggle.App
{
    /// <summary>
    /// UPDATE-03/D-04: the "an update is available" confirm dialog, shown when
    /// UpdateOrchestrator.CheckOnLaunchAsync finds a strictly newer release -- follows
    /// MonitorConfirmDialog's exact structural precedent (themed Form constructed
    /// with themeProvider, never bare native modal chrome, ARCHITECTURE.md
    /// Anti-Pattern 3). Nothing downloads before btnUpdateNow is clicked and this
    /// dialog returns DialogResult.OK -- that ordering is the entire point of
    /// UPDATE-03.
    ///
    /// Reserves the left third of the button row for a future "Skip this version"
    /// button (D-02) -- deliberately not added in this task; plan 26-04 owns it.
    /// </summary>
    public partial class UpdatePromptDialog : Form
    {
        private readonly IThemeProvider _themeProvider;

        public UpdatePromptDialog(ReleaseInfo release, IThemeProvider themeProvider)
        {
            if (release is null)
            {
                throw new ArgumentNullException(nameof(release));
            }

            _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));

            InitializeComponent();

            lblHeadline.Text = $"Rig Toggle {release.TagName} is available";
            rtbReleaseNotes.Text = string.IsNullOrWhiteSpace(release.Body)
                ? "This release doesn't include notes. See the full release on GitHub for details."
                : release.Body;

            this.AcceptButton = btnUpdateNow;
            this.CancelButton = btnLater;

            // Same transient-dialog lifecycle as MonitorConfirmDialog: constructed
            // fresh via `using var ... ShowDialog()` on every confirm, so unsubscribe
            // on FormClosed (not Dispose-time) is the correct hook here.
            _themeProvider.ThemeChanged += OnThemeChanged;
            this.FormClosed += (_, _) => _themeProvider.ThemeChanged -= OnThemeChanged;

            ThemeApplier.ApplyEffectiveColorMode(IsDark);
            DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
            ThemeApplier.ThemeButton(btnLater, IsDark);
            ThemeApplier.ThemeButton(btnUpdateNow, IsDark);
            ThemeApplier.ThemeRichTextBox(rtbReleaseNotes, IsDark);
        }

        // Single source of truth for "is dark mode active right now," read fresh
        // every call -- mirrors MonitorConfirmDialog.IsDark/MainForm.IsDark.
        private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;

        /// <summary>
        /// Live theme-flip handler, same marshal-then-try/catch pattern as
        /// MonitorConfirmDialog.OnThemeChanged -- ThemeChanged may fire off the UI
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
                ThemeApplier.ThemeButton(btnLater, IsDark);
                ThemeApplier.ThemeButton(btnUpdateNow, IsDark);
                ThemeApplier.ThemeRichTextBox(rtbReleaseNotes, IsDark);
                Refresh();
            }
            catch
            {
                // Cosmetic-only -- a theming failure must never crash the confirm flow.
            }
        }
    }
}
