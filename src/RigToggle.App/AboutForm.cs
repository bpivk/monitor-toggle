using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Windows;

namespace RigToggle.App
{
    /// <summary>
    /// UPDATE-06/quick-260829-ga9: the Help &gt; About dialog -- now the sole manual
    /// "Check for Updates" entry point (the tray item and the Settings button were
    /// removed as redundant, silently-broken surfaces), plus a version readout. It
    /// reports outcomes through its own <c>lblUpdateStatus</c> label rather than
    /// relying on a tray balloon, because notifyIcon.ShowBalloonTip is silently
    /// dropped whenever the tray icon is hidden -- which it is by default. Follows
    /// UpdatePromptDialog's exact structural precedent (themed Form constructed
    /// with themeProvider, never bare native modal chrome, ARCHITECTURE.md
    /// Anti-Pattern 3): ctor theming block, ThemeChanged subscribe + FormClosed
    /// unsubscribe + Dispose backstop, IsDark property, marshalled OnThemeChanged.
    ///
    /// The Check for Updates button awaits <paramref name="checkForUpdatesAsync"/>
    /// (i.e. MainForm.PerformManualUpdateCheckAsync) directly -- never a
    /// re-implementation of the update-check body -- so it inherits the
    /// _updateCheckInProgress reentrancy guard, and renders the returned
    /// <see cref="UpdateCheckResult"/> via UpdateCheckMessageFormatter.FormatStatus,
    /// the same formatter MainForm's tray balloon uses, so the two channels can
    /// never drift.
    /// </summary>
    public partial class AboutForm : Form
    {
        private readonly IThemeProvider _themeProvider;
        private readonly Func<Task<UpdateCheckResult?>>? _checkForUpdatesAsync;

        public AboutForm(string versionText, IThemeProvider themeProvider, Func<Task<UpdateCheckResult?>>? checkForUpdatesAsync)
        {
            if (versionText is null)
            {
                throw new ArgumentNullException(nameof(versionText));
            }

            _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));
            _checkForUpdatesAsync = checkForUpdatesAsync;

            InitializeComponent();

            lblVersion.Text = $"Version {versionText}";

            // A null delegate means "no update orchestrator is wired" (e.g. a test
            // harness constructing MainForm without one) -- disable rather than
            // ship a button that silently does nothing on click.
            if (checkForUpdatesAsync is null)
            {
                btnCheckForUpdates.Enabled = false;
            }
            else
            {
                btnCheckForUpdates.Click += BtnCheckForUpdates_ClickAsync;
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
        /// Quick-260829-ga9: the About dialog's own Check for Updates click handler.
        /// Sets an immediate "checking" status, disables the button, awaits the
        /// caller-supplied check, then renders the outcome via
        /// UpdateCheckMessageFormatter.FormatStatus -- or the "already running"
        /// copy when the awaited value is null (the CR-01 reentrancy guard rejected
        /// this call). Guarded with an IsDisposed/Disposing check after the await
        /// because the Applying outcome calls Application.Exit() while this modal
        /// dialog is still open, and the continuation would otherwise touch a torn-
        /// down form. Wrapped in try/catch so no exception can escape this async
        /// void handler; the button is re-enabled in a finally that is itself
        /// guarded by the same disposed check.
        /// </summary>
        private async void BtnCheckForUpdates_ClickAsync(object? sender, EventArgs e)
        {
            if (_checkForUpdatesAsync is null)
            {
                return;
            }

            lblUpdateStatus.Text = UpdateCheckMessageFormatter.CheckingMessage;
            btnCheckForUpdates.Enabled = false;

            try
            {
                UpdateCheckResult? result = await _checkForUpdatesAsync().ConfigureAwait(true);

                if (IsDisposed || Disposing)
                {
                    return;
                }

                lblUpdateStatus.Text = result is null
                    ? UpdateCheckMessageFormatter.AlreadyRunningMessage
                    : UpdateCheckMessageFormatter.FormatStatus(result);
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing)
                {
                    return;
                }

                UpdateCheckResult failureResult = new(UpdateCheckOutcome.CheckFailed, RunningVersionText: null, ex.Message);
                lblUpdateStatus.Text = UpdateCheckMessageFormatter.FormatStatus(failureResult);
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    btnCheckForUpdates.Enabled = true;
                }
            }
        }

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
