using System.Linq;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Windows;

namespace RigToggle.App
{
    /// <summary>
    /// Main window (D-13): mode indicator, Toggle button, Settings launch. Never
    /// instantiates a concrete Windows adapter or Json store directly — everything
    /// is injected by the composition root (Program.cs, Anti-Pattern 2 in
    /// 02-RESEARCH.md). Mode is derived from ToggleOrchestrator.IsInRigMode() (07-01:
    /// every toggle call now routes through the reentrancy-safe orchestrator rather
    /// than ToggleService directly), which itself derives from snapshot-file presence
    /// (D-14) — correct on startup even after a crash while in Rig mode.
    ///
    /// Phase 8 (TRAY-01/03/04/05, NOTIF-01): also tray-resident — hosts a NotifyIcon +
    /// ContextMenuStrip (Switch mode / Settings / Exit), redirects window Close to
    /// hide-to-tray, restores on left-click, and fires a balloon toast on every
    /// tray-menu toggle.
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly ToggleOrchestrator _orchestrator;
        private readonly ISettingsStore _settingsStore;
        private readonly IMonitorController _monitorController;
        private readonly Func<SettingsForm> _settingsFormFactory;

        private System.Drawing.Icon? _normalIcon;
        private System.Drawing.Icon? _rigIcon;

        // TRIG-01: fixed hotkey id -- only one global hotkey exists in this app, so a
        // single constant id is sufficient (RegisterHotKey/UnregisterHotKey key off this
        // id, not a key/modifier combination, to unregister). _hotkeyRegistered tracks
        // whether GlobalHotkey.Register currently holds this id so
        // UnregisterConfiguredHotkey stays idempotent (safe to call when nothing is
        // registered, e.g. before the very first registration attempt).
        private const int GlobalHotkeyId = 0x9001;
        private bool _hotkeyRegistered;

        // Code review CR-01: distinguishes "never shown this session" (true --tray
        // autostart, D-10's accepted no-tray-icon-until-manually-relaunched state) from
        // "was shown, then hidden to tray via user action" (the lockout scenario
        // ApplyTrayVisibility must guard against). OnLoad only fires on the visible
        // startup path -- under --tray the form's Load event never fires (see Program.cs) --
        // so this flag stays false for the entire life of a true --tray session.
        private bool _windowEverShown;

        public MainForm(
            ToggleOrchestrator orchestrator,
            ISettingsStore settingsStore,
            IMonitorController monitorController,
            Func<SettingsForm> settingsFormFactory)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
            _settingsFormFactory = settingsFormFactory ?? throw new ArgumentNullException(nameof(settingsFormFactory));

            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _windowEverShown = true;
            RefreshUi();
        }

        /// <summary>
        /// TRIG-01: intercepts WM_HOTKEY, the message user32.dll posts to this window
        /// once GlobalHotkey.Register has bound GlobalHotkeyId to it. base.WndProc MUST
        /// run unconditionally for every message, not just when the id doesn't match --
        /// skipping it (e.g. inside an early-return branch) would silently break WinForms'
        /// own focus/tray/paint message handling for this window. Do not "optimize" this
        /// into a conditional base call.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == GlobalHotkey.WmHotkey && (int)m.WParam == GlobalHotkeyId)
            {
                HandleHotkeyToggle();
            }

            base.WndProc(ref m);
        }

        /// <summary>
        /// TRAY-04/D-01, 08-RESEARCH.md Pitfall 6: Form.Load never fires unless the
        /// form is actually shown at least once, so a `--tray` autostart launch (which
        /// never calls Show()) would otherwise leave the tray icon in an uninitialized
        /// state until the first toggle. Program.cs (Plan 08-03) calls this explicitly
        /// and unconditionally right after constructing MainForm, BEFORE either
        /// Application.Run branch — so the tray glyph/tooltip are always correct on
        /// first paint, tray-only session or not. OnLoad's own RefreshUi() call above
        /// is kept for the normal (non-tray) startup path; both are safe to call
        /// (RefreshUi/LoadTrayIconsIfNeeded are idempotent).
        /// </summary>
        public void InitializeTrayState()
        {
            LoadTrayIconsIfNeeded();
            RefreshUi();
            ApplyTrayVisibility();
        }

        /// <summary>
        /// TRAY-01/D-08/D-09: derives notifyIcon.Visible from the OR of the two Phase 11
        /// preferences — the tray icon is shown whenever EITHER CloseMinimizesToTray OR
        /// MinimizeToTray is true, and absent only when BOTH are false (D-09). Idempotent
        /// and safe to call repeatedly (called at startup via InitializeTrayState() and
        /// again on demand, e.g. after Settings-Save, so the icon's presence updates live
        /// without requiring an app restart). D-11: the NotifyIcon component itself is
        /// always instantiated regardless of these settings — only its .Visible is
        /// derived here, so ShowBalloonTip calls elsewhere (tray/hotkey toggle toasts)
        /// never need a null/existence guard. Accepted consequence of D-11: toasts render
        /// nothing while both settings are off, since ShowBalloonTip requires Visible ==
        /// true to actually display — this is an intentional tradeoff, not a bug.
        ///
        /// Lockout guard (code review CR-01): D-09 keeps Settings reachable from the tray
        /// context menu even while MainForm is Hide()'d. If the user disables both prefs
        /// from that Settings session, dropping notifyIcon.Visible while the window is
        /// still hidden would leave zero UI surface reachable (no tray icon, no taskbar
        /// entry) — recoverable only via Task Manager. Force the window back into view
        /// before losing the tray icon in that case.
        ///
        /// The guard only fires when _windowEverShown is true (the window was visible at
        /// some point this session, then hidden via SendToTray) — NOT on a true --tray
        /// autostart where the window was never shown at all. That combination (D-10) is
        /// an accepted, deliberate state: the app starts hidden with no tray icon and can
        /// only be relaunched manually. Do not "fix" that case by forcing a Show() here.
        /// </summary>
        public void ApplyTrayVisibility()
        {
            AppSettings settings;
            try
            {
                settings = _settingsStore.Load();
            }
            catch
            {
                settings = new AppSettings();
            }

            bool shouldBeVisible = settings.CloseMinimizesToTray || settings.MinimizeToTray;

            if (!shouldBeVisible && !Visible && _windowEverShown)
            {
                Show();
                WindowState = FormWindowState.Normal;
            }

            notifyIcon.Visible = shouldBeVisible;
        }

        /// <summary>
        /// 08-RESEARCH.md Pitfall 3: loads the two pre-made embedded .ico resources
        /// once and keeps the resulting Icon instances for the lifetime of the form —
        /// never re-derive an Icon from a Bitmap per toggle (Icon.FromHandle leaks the
        /// underlying GDI handle since the wrapper does not own it).
        /// </summary>
        private void LoadTrayIconsIfNeeded()
        {
            if (_normalIcon is not null && _rigIcon is not null)
            {
                return;
            }

            var assembly = typeof(MainForm).Assembly;
            using var normalStream = assembly.GetManifestResourceStream("normal.ico");
            using var rigStream = assembly.GetManifestResourceStream("rig.ico");
            // IN-01 (code review): explicit null-check with a descriptive message
            // instead of the null-forgiving operator — a missing/renamed embedded
            // resource should fail with a clear diagnostic, not an opaque exception
            // from the Icon constructor at startup, before any window exists.
            _normalIcon = new System.Drawing.Icon(normalStream
                ?? throw new InvalidOperationException("Embedded resource 'normal.ico' not found."));
            _rigIcon = new System.Drawing.Icon(rigStream
                ?? throw new InvalidOperationException("Embedded resource 'rig.ico' not found."));
        }

        /// <summary>
        /// Re-derives the mode indicator (from snapshot-file presence, D-14). Called
        /// on startup and after every toggle/Settings-dialog close.
        /// </summary>
        private void RefreshUi()
        {
            bool isInRigMode = _orchestrator.IsInRigMode();
            lblMode.Text = isInRigMode ? "Mode: Rig" : "Mode: Normal";
            btnToggle.Text = isInRigMode ? "Switch to Normal Mode" : "Switch to Rig Mode";

            // TRAY-04/D-01: tray icon + tooltip must always reflect the current mode,
            // correct on first paint even under --tray startup. Guarded on the icons
            // being loaded (InitializeTrayState/LoadTrayIconsIfNeeded) so a hypothetical
            // future caller of RefreshUi() before InitializeTrayState() never NREs.
            if (_normalIcon is not null && _rigIcon is not null)
            {
                notifyIcon.Icon = isInRigMode ? _rigIcon : _normalIcon;
            }
            notifyIcon.Text = isInRigMode ? "Rig Toggle — Rig Mode" : "Rig Toggle — Normal Mode";
            trayToggleMenuItem.Text = btnToggle.Text; // D-04: one shared source of truth
        }

        private void BtnToggle_Click(object? sender, EventArgs e)
        {
            ToggleResult? result = null;

            try
            {
                if (_orchestrator.IsInRigMode())
                {
                    result = _orchestrator.ToggleToNormalMode();
                }
                else
                {
                    if (!_orchestrator.IsSettingsConfigured())
                    {
                        // WR-01: don't let an incomplete Settings state reach ToggleToRigMode
                        // at all — redirect to Settings instead of persisting a garbage
                        // snapshot and flipping the mode indicator to "Rig".
                        MessageBox.Show(
                            this,
                            "Please finish configuring Settings (at least one monitor to disable or enable, both audio devices, and the companion app) before switching to Rig Mode.",
                            "Rig Toggle",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    // DISPLAY-07 / D-06: informed-consent confirmation naming every
                    // monitor being disabled AND every monitor being enabled, durably
                    // suppressible via "don't ask again" and reset whenever Settings
                    // changes either configured set (04-RESEARCH.md Pattern 5, generalized
                    // Phase 6). Names are resolved via GetAllMonitors() (not
                    // GetActiveMonitors()) because an enable-set monitor is inactive at
                    // confirm-time and would otherwise fail to resolve.
                    var settings = _settingsStore.Load();
                    if (!settings.SkipMonitorConfirmation)
                    {
                        var disablePaths = settings.MonitorsToDisable ?? new List<string>();
                        var enablePaths = settings.MonitorsToEnable ?? new List<string>();

                        IReadOnlyList<MonitorInfo> allMonitors;
                        try
                        {
                            allMonitors = _monitorController.GetAllMonitors();
                        }
                        catch (Exception ex)
                        {
                            // Defensive fallback: an enumeration hiccup must never block the
                            // confirmation — fall back to raw device paths as names. Traced
                            // (WR-02, code review) for consistency with every other swallowed
                            // failure in this codebase (see ToggleService.cs's IN-02 comments) —
                            // otherwise a machine hitting this path leaves no diagnostic trail
                            // even with EnableDebugLogging on.
                            System.Diagnostics.Trace.WriteLine($"GetAllMonitors failed while resolving names for confirm dialog: {ex}");
                            allMonitors = Array.Empty<MonitorInfo>();
                        }

                        string ResolveName(string devicePath) =>
                            allMonitors.FirstOrDefault(m => m.DevicePath == devicePath)?.FriendlyName ?? devicePath;

                        var disableNames = disablePaths.Select(ResolveName).ToList();
                        var enableNames = enablePaths.Select(ResolveName).ToList();

                        using var confirmDialog = new MonitorConfirmDialog(disableNames, enableNames);
                        if (confirmDialog.ShowDialog(this) != DialogResult.OK)
                        {
                            return; // user cancelled — nothing mutated
                        }

                        if (confirmDialog.DontAskAgain)
                        {
                            settings.SkipMonitorConfirmation = true;
                            _settingsStore.Save(settings);
                        }
                    }

                    result = _orchestrator.ToggleToRigMode();
                }

                RefreshUi();

                if (result is not null && !result.Success)
                {
                    // CORE-04: per-step checklist for a partial failure. State may have
                    // partially changed (e.g. monitor disabled OK but audio failed), which
                    // is why RefreshUi() above always runs before this dialog is shown.
                    MessageBox.Show(
                        this,
                        $"The toggle did not fully complete:\n\n{ToggleResultFormatter.FormatChecklist(result)}",
                        "Rig Toggle",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (ToggleInProgressException ex)
            {
                // WR-01 (code review): a busy-rejection is an expected condition (CORE-06),
                // not an error — refines D-05's "zero UI changes" trade-off with a dedicated
                // branch rather than the generic "something went wrong" wording below, which
                // would misleadingly tell the user to "check Settings" for simply clicking
                // too fast. D-05 itself only required no NEW UI code to keep this trigger's
                // existing behavior unchanged when no toggle is in flight — it still holds.
                // Currently unreachable from this single-threaded UI-only trigger (the guard
                // exists for Phase 8+'s tray/hotkey/CLI triggers), but the informational
                // wording is correct now rather than deferred.
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Rig Toggle",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                // Basic guard only (D-13/T-02-FAKEFAIL) — this catch is the fallback for the
                // exception-based preflight/corrupted-snapshot guards (unconfigured settings,
                // missing companion app path, corrupted monitor snapshot), which are NOT part
                // of the ToggleResult contract (see Plan 01). Per-step CORE-04 partial-failure
                // reporting for the three mutation steps (Monitor/Audio/App) happens via the
                // ToggleResult checklist above. Exception detail is included (not just a
                // generic message) because this is a single-user diagnostic tool, not a
                // hardened multi-user app — surfacing the real error is more useful than
                // hiding it, especially for CCD-mutation failures that are otherwise
                // unreproducible without rig hardware.
                MessageBox.Show(
                    this,
                    $"Something went wrong while toggling:\n\n{ex.GetType().Name}: {ex.Message}\n\nTry again, or check Settings.",
                    "Rig Toggle",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void BtnSettings_Click(object? sender, EventArgs e) => OpenSettingsDialog();

        /// <summary>
        /// Modal (D-03): blocks Main until closed. New settings apply on the NEXT
        /// toggle, not mid-flight — RefreshUi() below only updates the status line, it
        /// does not re-run any toggle logic. Shared by BtnSettings_Click and
        /// TraySettingsMenuItem_Click (IN-02, code review) so the two paths can't drift.
        ///
        /// TRIG-01/D-07: unregisters the global hotkey for the entire Settings dialog
        /// lifetime and re-registers on close (reflecting whatever the user saved or
        /// discarded). Unregistering for the whole lifetime is deliberately simpler and
        /// more robust than queuing/ignoring a mid-edit WM_HOTKEY -- it guarantees zero
        /// chance of a toggle racing an in-progress Settings edit. Do not "fix" this by
        /// re-adding mid-edit hotkey handling.
        /// </summary>
        private void OpenSettingsDialog()
        {
            UnregisterConfiguredHotkey();
            using var settingsForm = _settingsFormFactory();
            settingsForm.ShowDialog(this);
            TryRegisterConfiguredHotkey();
            RefreshUi();
        }

        /// <summary>
        /// TRAY-01/D-01: the single shared "send to tray" mechanism used by both the
        /// Close (MainForm_FormClosing) and Minimize (MainForm_Resize) hide-to-tray
        /// paths — do NOT duplicate the Hide() call in either caller; both must route
        /// through here so the two paths can never drift apart.
        /// </summary>
        private void SendToTray()
        {
            Hide();
        }

        /// <summary>
        /// TRAY-01/D-01: only the window's own Close (X button, Alt+F4, or a plain
        /// this.Close() call) is ever considered for hide-to-tray —
        /// CloseReason.UserClosing is the specific, documented enum value raised for
        /// exactly that case (08-RESEARCH.md Pattern 1). Phase 11 (D-01) makes this
        /// redirect CONDITIONAL on AppSettings.CloseMinimizesToTray (default false,
        /// D-02) rather than the previously-unconditional Phase-8 behavior: when the
        /// setting is on, X hides to tray exactly as before; when off, X now lets the
        /// close proceed and the app exits. Deliberately does NOT gate on any other
        /// CloseReason: the tray's own "Exit" menu item calls Application.Exit()
        /// directly, which raises the distinct CloseReason.ApplicationExitCall and
        /// must be allowed to proceed through this same handler with no extra flag —
        /// do NOT "fix" this into symmetry with a custom _isExiting boolean.
        /// WindowsShutDown/TaskManagerClosing must also be allowed through, or the
        /// OS/Task Manager could never actually terminate the process.
        /// </summary>
        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // WR-01 (code review): Load() is unguarded elsewhere for best-effort, low-frequency
            // callers, but Close is a core, everyday interaction — degrade to "never configured"
            // defaults (X exits, matching first-run behavior) rather than let a settings.json
            // read failure (e.g. UnauthorizedAccessException) propagate out of this handler.
            AppSettings settings;
            try
            {
                settings = _settingsStore.Load();
            }
            catch
            {
                settings = new AppSettings();
            }

            if (e.CloseReason == CloseReason.UserClosing && settings.CloseMinimizesToTray)
            {
                e.Cancel = true;
                SendToTray();
                return;
            }

            // UserClosing with CloseMinimizesToTray false (D-01: X now exits the app),
            // or any non-UserClosing reason (ApplicationExitCall from tray Exit,
            // WindowsShutDown, TaskManagerClosing, etc.) falls through here and the
            // close proceeds. T-08-GHOST: belt-and-suspenders ghost-icon prevention
            // alongside the explicit notifyIcon.Visible = false already set in
            // TrayExitMenuItem_Click.
            notifyIcon.Visible = false;
        }

        /// <summary>
        /// TRAY-01/D-04/D-05: intercepts the window minimize action. When
        /// AppSettings.MinimizeToTray is true, a minimize hides the window to tray via
        /// the same shared SendToTray() helper used by the Close path — the window's
        /// hidden appearance is pixel-identical to the closed-to-tray state, so no new
        /// visual variant is introduced, and this deliberately reuses SendToTray()
        /// rather than duplicating Hide(). When MinimizeToTray is false (D-05 default),
        /// this handler does nothing and the standard OS minimize-to-taskbar proceeds
        /// unchanged. Restore happens through the existing NotifyIcon_MouseClick
        /// left-click path (Show(); WindowState = Normal; Activate()), not from here.
        /// </summary>
        private void MainForm_Resize(object? sender, EventArgs e)
        {
            // WR-01 (code review): same defensive Load() as MainForm_FormClosing — Minimize
            // is a core, everyday interaction and should degrade to standard OS minimize
            // rather than throw if settings.json is unreadable.
            AppSettings settings;
            try
            {
                settings = _settingsStore.Load();
            }
            catch
            {
                settings = new AppSettings();
            }

            if (WindowState == FormWindowState.Minimized && settings.MinimizeToTray)
            {
                SendToTray();
            }
        }

        /// <summary>
        /// TRAY-05/D-02: NotifyIcon.MouseClick (not the button-agnostic Click event,
        /// which fires for both mouse buttons per 08-RESEARCH.md Pitfall 2) restores
        /// and focuses the main window on a LEFT click only — a right-click here must
        /// only open the context menu, not also restore the window.
        /// </summary>
        private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
            }
        }

        private void TraySettingsMenuItem_Click(object? sender, EventArgs e) => OpenSettingsDialog();

        /// <summary>
        /// TRAY-03/D-04: explicitly hide the tray icon before Application.Exit() — an
        /// undisposed/still-visible NotifyIcon is a well-known WinForms bug (T-08-GHOST)
        /// that leaves a stale, unclickable ghost icon in the tray until the user
        /// hovers over it. components.Dispose() (Dispose(bool)) is a backstop, not a
        /// substitute, for this explicit call.
        /// </summary>
        private void TrayExitMenuItem_Click(object? sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            Application.Exit();
        }

        /// <summary>
        /// TRAY-03/NOTIF-01, D-08/D-09: the tray-menu toggle handler — the second-ever
        /// caller of Phase 7's ToggleOrchestrator, validating that extraction. Skips
        /// the GUI-only WR-01 config guard and DISPLAY-07 confirm dialog on purpose
        /// (inappropriate for a background trigger with no guaranteed-visible window).
        /// ACCEPTED RISK (WR-02, 08-REVIEW.md, reviewed and knowingly kept): this means
        /// a user who configures Settings entirely via the tray's own Settings item and
        /// then toggles for the very first time from the tray menu never sees the
        /// DISPLAY-07 informed-consent dialog naming which monitors will be
        /// disabled/enabled. This is the intentional cost of "control entirely from the
        /// tray icon" (TRAY-03's own goal); it is not an oversight and should not be
        /// silently "fixed" by re-adding the dialog here, which would defeat the
        /// tray-only convenience this handler exists for.
        /// CRITICAL (D-08 no-chrome guarantee): every branch below — both exception
        /// handlers and the final result toast — routes through
        /// notifyIcon.ShowBalloonTip, NEVER MessageBox.Show. A tray-triggered toggle
        /// must never surface GUI chrome, since the main window may be hidden to tray
        /// at the moment this runs. The final result toast fires UNCONDITIONALLY
        /// (regardless of whether the window happens to be visible right now) by
        /// design — do not add a visibility check here; a future editor might assume
        /// the toast is redundant when the window is already visible, but D-08 requires
        /// it every time regardless.
        /// </summary>
        private void TrayToggleMenuItem_Click(object? sender, EventArgs e)
        {
            ToggleResult result;

            try
            {
                result = _orchestrator.IsInRigMode()
                    ? _orchestrator.ToggleToNormalMode()
                    : _orchestrator.ToggleToRigMode();
            }
            catch (ToggleInProgressException ex)
            {
                notifyIcon.ShowBalloonTip(
                    3000,
                    "Rig Toggle",
                    ToggleResultFormatter.TruncateForBalloon(ex.Message),
                    ToolTipIcon.Warning);
                return;
            }
            catch (Exception ex)
            {
                notifyIcon.ShowBalloonTip(
                    3000,
                    "Rig Toggle",
                    ToggleResultFormatter.TruncateForBalloon($"Something went wrong while toggling: {ex.GetType().Name}: {ex.Message}"),
                    ToolTipIcon.Warning);
                return;
            }

            RefreshUi();

            notifyIcon.ShowBalloonTip(
                3000,
                ToggleResultFormatter.FormatModeTitle(_orchestrator.IsInRigMode()),
                ToggleResultFormatter.TruncateForBalloon(ToggleResultFormatter.FormatChecklist(result)),
                result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }

        /// <summary>
        /// TRIG-01: the global-hotkey toggle handler, dispatched from WndProc on
        /// WM_HOTKEY. Structurally identical to TrayToggleMenuItem_Click (D-03) --
        /// same skip-the-GUI posture (no DISPLAY-07 confirm dialog, no WR-01 config
        /// guard, since the main window may be hidden to tray or the hotkey pressed
        /// mid-game). CRITICAL (D-08 no-chrome guarantee): every branch below routes
        /// through notifyIcon.ShowBalloonTip, NEVER MessageBox.Show -- a hotkey-triggered
        /// toggle must never surface GUI chrome.
        /// </summary>
        private void HandleHotkeyToggle()
        {
            ToggleResult result;

            try
            {
                result = _orchestrator.IsInRigMode()
                    ? _orchestrator.ToggleToNormalMode()
                    : _orchestrator.ToggleToRigMode();
            }
            catch (ToggleInProgressException ex)
            {
                notifyIcon.ShowBalloonTip(
                    3000,
                    "Rig Toggle",
                    ToggleResultFormatter.TruncateForBalloon(ex.Message),
                    ToolTipIcon.Warning);
                return;
            }
            catch (Exception ex)
            {
                notifyIcon.ShowBalloonTip(
                    3000,
                    "Rig Toggle",
                    ToggleResultFormatter.TruncateForBalloon($"Something went wrong while toggling: {ex.GetType().Name}: {ex.Message}"),
                    ToolTipIcon.Warning);
                return;
            }

            RefreshUi();

            notifyIcon.ShowBalloonTip(
                3000,
                ToggleResultFormatter.FormatModeTitle(_orchestrator.IsInRigMode()),
                ToggleResultFormatter.TruncateForBalloon(ToggleResultFormatter.FormatChecklist(result)),
                result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }

        /// <summary>
        /// TRIG-01/D-04: (re)registers the configured global hotkey on this window's
        /// handle, unregister-first so it is safe to call repeatedly -- this is what
        /// keeps both the D-04 Settings-Save path and the D-07 re-register-on-close path
        /// correct without double-register bugs. Do NOT "simplify" this into a
        /// register-only call; a stale registration left behind by a prior call would
        /// otherwise make a subsequent RegisterHotKey call fail spuriously (Windows
        /// rejects registering the same id twice on the same window without first
        /// unregistering it).
        /// Returns true if nothing is configured (nothing to register) OR registration
        /// succeeded; false if a configured hotkey failed to register (e.g. already
        /// claimed by another application).
        /// </summary>
        public bool TryRegisterConfiguredHotkey()
        {
            GlobalHotkey.Unregister(Handle, GlobalHotkeyId);
            _hotkeyRegistered = false;

            var settings = _settingsStore.Load();
            if (settings.HotkeyModifiers is not int modifiers || settings.HotkeyKey is not int key)
            {
                return true; // nothing configured -- nothing to register, not a failure
            }

            bool registered = GlobalHotkey.Register(
                Handle,
                GlobalHotkeyId,
                (uint)modifiers | GlobalHotkey.ModNoRepeat,
                (uint)key);

            _hotkeyRegistered = registered;
            return registered;
        }

        /// <summary>
        /// TRIG-01/D-07: unregisters the global hotkey if currently registered; no-op
        /// otherwise, so callers (OpenSettingsDialog, FormClosing paths) never need to
        /// track registration state themselves.
        /// </summary>
        public void UnregisterConfiguredHotkey()
        {
            if (_hotkeyRegistered)
            {
                GlobalHotkey.Unregister(Handle, GlobalHotkeyId);
                _hotkeyRegistered = false;
            }
        }

        /// <summary>
        /// TRIG-01/D-06: best-effort startup registration. Never rethrows -- this is a
        /// startup side effect, not a critical path, and must never block
        /// Application.Run (mirroring Program.cs's own trace-listener try/catch
        /// convention). A false return OR a caught exception is traced and surfaced via
        /// a warning balloon toast using the exact D-06 wording from the 09-UI-SPEC
        /// Copywriting Contract.
        /// </summary>
        public void RegisterHotkeyAtStartup()
        {
            try
            {
                if (!TryRegisterConfiguredHotkey())
                {
                    System.Diagnostics.Trace.WriteLine("RegisterHotkeyAtStartup: TryRegisterConfiguredHotkey returned false -- the configured hotkey could not be registered.");
                    notifyIcon.ShowBalloonTip(
                        3000,
                        "Rig Toggle",
                        ToggleResultFormatter.TruncateForBalloon("Rig Toggle: the configured hotkey could not be registered — it may already be in use by another application."),
                        ToolTipIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"RegisterHotkeyAtStartup: exception while registering configured hotkey: {ex}");
                notifyIcon.ShowBalloonTip(
                    3000,
                    "Rig Toggle",
                    ToggleResultFormatter.TruncateForBalloon("Rig Toggle: the configured hotkey could not be registered — it may already be in use by another application."),
                    ToolTipIcon.Warning);
            }
        }
    }
}
