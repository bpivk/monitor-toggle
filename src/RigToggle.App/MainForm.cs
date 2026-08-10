using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using RigToggle.App.Controls;
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
    /// than ToggleService directly), which itself derives from the explicit
    /// IModeStore-persisted mode flag (DISPLAY-11) — correct on startup regardless of
    /// snapshot-file presence, which this app no longer writes or reads for mode
    /// determination.
    ///
    /// Phase 8 (TRAY-01/03/04/05, NOTIF-01): also tray-resident — hosts a NotifyIcon +
    /// ContextMenuStrip (Switch mode / Settings / Exit), redirects window Close to
    /// hide-to-tray, restores on left-click, and fires a balloon toast on every
    /// tray-menu toggle.
    ///
    /// Phase 19 (TILE-07): the standalone monitor panel form and both its entry points
    /// (the Monitors button and the tray Monitors item) are retired now that the tile
    /// dashboard has fully absorbed the panel's capability. Per-monitor enable/disable,
    /// hotplug refresh, the confirmation gate, and Identify all live inline on this
    /// form as the monitor-tile dashboard (TILE-01..06, MAIN-01/02). MainForm is the
    /// sole SystemEvents.DisplaySettingsChanged subscriber for the tile row (see the
    /// constructor and Dispose(bool)).
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly ToggleOrchestrator _orchestrator;
        private readonly ISettingsStore _settingsStore;
        private readonly IMonitorController _monitorController;
        private readonly Func<SettingsForm> _settingsFormFactory;
        private readonly IThemeProvider _themeProvider;

        // TILE-01: live tile instances, reconciled (not rebuilt) against monitor count
        // on every RefreshMonitorTiles() call -- see that method's own comment.
        private readonly List<MonitorTile> _tiles = new();

        // 2026-08-10 rig report: FlatAppearance.MouseOverBackColor/MouseDownBackColor
        // (ThemeApplier.ThemeButton) still showed no visible hover/press feedback on
        // real hardware even after widening the color deltas -- consistent with this
        // codebase's own documented dotnet/winforms#13897 (FlatAppearance colors
        // unreliable once dark mode is active). btnIdentify/btnSettings now track
        // hover/press state locally and paint their own background manually (the
        // same reliable owner-draw pattern MonitorTile already uses), bypassing
        // FlatAppearance for these two buttons entirely.
        private bool _identifyHovered;
        private bool _identifyPressed;
        private bool _settingsHovered;
        private bool _settingsPressed;

        // D-08/Pitfall 6: the ONE canonical DevicePath-ordered monitor list. Tile
        // position, tile number, AND (Plan 19-03) the Identify overlay numbers all
        // derive from this single field -- never re-derive an independent order
        // anywhere else.
        private IReadOnlyList<MonitorInfo> _lastKnownMonitors = Array.Empty<MonitorInfo>();

        // 19-UI-SPEC.md Tile & Layout Geometry Contract values, expressed at 100%
        // scale -- every use passes through Scaled(). TileMarginPx = 6 on all four
        // sides yields the spec's 12px tile-to-tile gap (6 + 6); the wrap cap is
        // therefore (TileWidthPx + 2 * TileMarginPx) * MaxTilesPerRow.
        private const int MarginPx = 16;
        private const int GapMdPx = 16;
        private const int GapLgPx = 24;
        private const int TileWidthPx = 72;
        private const int TileHeightPx = 88;
        private const int TileMarginPx = 6;
        private const int MaxTilesPerRow = 4;
        private const int ContentWidthFloorPx = 288;
        private const int IdentifyWidthPx = 100;
        private const int IdentifyHeightPx = 32;
        // 20-UI-SPEC.md Geometry Contract -- the toggle row is 288x32 (was a
        // 288x40 button); it is shorter because it is now a Settings-style row
        // rather than a big primary-action button (D-01), and the switch's own
        // track/thumb geometry is derived inside the control from this row height.
        private const int ToggleRowHeightPx = 32;
        private const int GearSizePx = 32;
        private const int EmptyStateHeightPx = 40;

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

        public MainForm(
            ToggleOrchestrator orchestrator,
            ISettingsStore settingsStore,
            IMonitorController monitorController,
            Func<SettingsForm> settingsFormFactory,
            IThemeProvider themeProvider)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
            _settingsFormFactory = settingsFormFactory ?? throw new ArgumentNullException(nameof(settingsFormFactory));
            _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));

            InitializeComponent();

            // 12-02/D-05: live theme-follow -- WindowsThemeProvider raises this whenever
            // the OS AppsUseLightTheme value genuinely flips while this form is alive.
            _themeProvider.ThemeChanged += OnThemeChanged;

            // TILE-06/19-RESEARCH.md Pitfall 2: subscribed ONCE, app-lifetime, and
            // deliberately NEVER unsubscribed on Hide()/Show()/FormClosed. MainForm is
            // hidden-not-closed for most of its runtime (tray-resident operation);
            // gating this subscription on visibility would silently kill hotplug
            // refresh during exactly that state. TILE-06 only requires refresh while
            // visible -- an always-on subscription satisfies that as a strict
            // superset. Dispose(bool)'s unsubscribe (MainForm.Designer.cs) is a
            // real-process-exit backstop only. Do NOT copy a closable form's
            // subscribe-in-ctor / unsubscribe-on-close pattern here -- that pattern is
            // correct for a closable-and-reopenable form and wrong for this one.
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        /// <summary>
        /// AutoScaleMode.Font rescales control Bounds that WinForms itself lays out,
        /// but has zero effect on positions/sizes assigned programmatically -- exactly
        /// what LayoutDashboard() does below. Every layout-path literal passes through
        /// here. The divisor 15f is MainForm's own AutoScaleDimensions height (new
        /// SizeF(7F, 15F) in the Designer), i.e. the design-time font height this
        /// layout was authored against. Do not replace this with DeviceDpi / 96f --
        /// the form scales by font, not by raw DPI.
        /// </summary>
        private int Scaled(int value) => (int)Math.Round(value * (Font.Height / 15f));

        /// <summary>
        /// 12-02/D-05: live theme-flip handler. WindowsThemeProvider's ThemeChanged
        /// may fire off the UI thread (SystemEvents is not guaranteed to raise on the
        /// subscriber's thread, per A3) -- marshal via InvokeRequired/BeginInvoke before
        /// touching any control. The whole re-theme body is wrapped in try/catch:
        /// theming is cosmetic-only and must never crash the toggle flow (T-12-02).
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
                ThemeApplier.ThemeButton(btnSettings, IsDark);
                // 19-RESEARCH.md Pitfall 1: this call site and InitializeTrayState()
                // below must stay in lockstep -- adding a new control to only one of
                // them is the Phase 12 bug this codebase already shipped twice.
                ApplyDashboardTheming();
                Refresh();
            }
            catch
            {
                // Cosmetic-only (T-12-02) -- a theming failure must never crash the toggle flow.
            }
        }

        // 12-05/CR-01: single source of truth for "is dark mode active right now,"
        // read fresh every call (never cached) so DWM chrome + button theming stay
        // correct across live flips -- mirrors SettingsForm.IsDarkTheme.
        private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;

        // Same source/values as ThemeApplier.ThemeMonitorTile's AccentColor -- kept
        // here too so btnIdentify/btnSettings's manually-painted focus ring uses the
        // identical color a tile's own focus ring does.
        private Color AccentColor => IsDark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;

        /// <summary>
        /// 12-02/D-08/THEME-06: requests Windows-11 rounded corners + Mica for this
        /// window (silent no-op on Windows 10, D-07). Called from InitializeTrayState()
        /// below, NOT from OnLoad/OnShown -- under `--tray` startup, Form.Load/Shown
        /// never fire since the window is never Show()'n, but InitializeTrayState()
        /// runs unconditionally on both startup paths and Handle is already forced by
        /// the time it runs (RegisterHotkeyAtStartup() forces it before either
        /// Application.Run branch in Program.cs).
        /// </summary>
        private void ApplyDwmChrome()
        {
            try
            {
                DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
            }
            catch
            {
                // Cosmetic-only (T-12-03) -- must never block startup.
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
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

            // 12-02/D-08: DWM chrome is applied HERE, not from OnLoad/OnShown -- see
            // ApplyDwmChrome's own doc comment for the full --tray-safe-timing rationale.
            ApplyDwmChrome();

            // 12-05/CR-02: theme the gear button at load time too, same --tray-safe
            // timing rationale as ApplyDwmChrome above -- InitializeTrayState() runs
            // unconditionally on both startup paths, unlike OnLoad/OnShown. The toggle
            // switch is themed below instead, via the shared dashboard-theming helper.
            ThemeApplier.ThemeButton(btnSettings, IsDark);

            // TILE-01/06: populate/theme/layout the dashboard at this same --tray-safe
            // timing point, exactly like ApplyDwmChrome above -- InitializeTrayState()
            // runs unconditionally on both startup paths, unlike OnLoad/OnShown, which
            // never fire under a --tray hidden start.
            RefreshMonitorTiles();

            // 19-RESEARCH.md Pitfall 1: this call site and OnThemeChanged above must
            // stay in lockstep -- adding a new control to only one of them is the
            // Phase 12 bug this codebase already shipped twice.
            ApplyDashboardTheming();
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
        /// Lockout guard (code review CR-01, tightened after VERIFICATION.md gap):
        /// D-09 keeps Settings reachable from the tray context menu even while MainForm
        /// is Hide()'d -- reachable BOTH when the window was explicitly hidden via
        /// SendToTray() and when a --tray autostart with MinimizeToTray=true never
        /// showed the window at all (notifyIcon.Visible is already true from
        /// InitializeTrayState() in that case). If the user disables both prefs from
        /// that Settings session, dropping notifyIcon.Visible while the window is still
        /// not Visible would leave zero UI surface reachable (no tray icon, no taskbar
        /// entry) -- recoverable only via Task Manager.
        ///
        /// The guard therefore keys off notifyIcon.Visible's value BEFORE this call
        /// overwrites it -- "was there a reachable tray icon a moment ago that is about
        /// to disappear while the window is still not Visible" -- not on whether the
        /// window itself was ever shown. This correctly leaves the true --tray-autostart
        /// -with-both-settings-off case (D-10, accepted: app starts hidden with no tray
        /// icon, must be relaunched manually) untouched, since notifyIcon.Visible starts
        /// false in that case and there is nothing to lose.
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

            bool wasVisible = notifyIcon.Visible;
            bool shouldBeVisible = settings.CloseMinimizesToTray || settings.MinimizeToTray;

            if (wasVisible && !shouldBeVisible && !Visible)
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
                ?? throw new InvalidOperationException("Embedded resource 'normal.ico' not found."), SystemInformation.SmallIconSize);
            _rigIcon = new System.Drawing.Icon(rigStream
                ?? throw new InvalidOperationException("Embedded resource 'rig.ico' not found."), SystemInformation.SmallIconSize);
        }

        /// <summary>
        /// Re-derives the mode indicator (from the explicit IModeStore-persisted mode
        /// flag, DISPLAY-11). Called on startup and after every toggle/Settings-dialog
        /// close.
        /// </summary>
        private void RefreshUi()
        {
            // DISPLAY-11/T-16-09: an unknown mode (mode file missing/corrupted) must
            // never default to either Rig or Normal wording — the IsInRigMode() branch
            // below is only meaningful once the mode is actually known. The notify
            // icon is deliberately left at its current safe default rather than picked
            // here (there is no "unknown" icon glyph this phase). The old mode-text
            // label is gone (D-06), so the switch's Indeterminate position — mid-track,
            // filled gray, distinct from both real states — is now the sole carrier of
            // the "never default to either Rig or Normal wording" rule.
            if (!_orchestrator.IsModeKnown())
            {
                toggleSwitch.SetState(ToggleSwitchState.Indeterminate);
                trayToggleMenuItem.Text = "Toggle";
                notifyIcon.Text = "Rig Toggle — Mode Unknown";
                return;
            }

            bool isInRigMode = _orchestrator.IsInRigMode();
            // D-08: Rig = on/right/filled, Normal = the app's baseline everywhere else
            // and therefore the neutral off/left position.
            toggleSwitch.SetState(isInRigMode ? ToggleSwitchState.On : ToggleSwitchState.Off);

            // TRAY-04/D-01: tray icon + tooltip must always reflect the current mode,
            // correct on first paint even under --tray startup. Guarded on the icons
            // being loaded (InitializeTrayState/LoadTrayIconsIfNeeded) so a hypothetical
            // future caller of RefreshUi() before InitializeTrayState() never NREs.
            if (_normalIcon is not null && _rigIcon is not null)
            {
                notifyIcon.Icon = isInRigMode ? _rigIcon : _normalIcon;
            }
            notifyIcon.Text = isInRigMode ? "Rig Toggle — Rig Mode" : "Rig Toggle — Normal Mode";
            // D-04: one shared source of truth -- computed alongside the switch's
            // SetState() call above rather than read off a deleted control's Text.
            trayToggleMenuItem.Text = isInRigMode ? "Switch to Normal Mode" : "Switch to Rig Mode";
        }

        private void ToggleSwitch_ActionRequested(object? sender, EventArgs e)
        {
            ToggleResult? result = null;

            try
            {
                if (!_orchestrator.IsModeKnown())
                {
                    // T-16-09: refuse to toggle from an unknown mode rather than
                    // guessing a direction — mirrors the corrupted-snapshot precedent's
                    // "verify manually" posture. LOCKED copy (16-UI-SPEC.md).
                    MessageBox.Show(
                        this,
                        "Rig Toggle's current mode is unknown — fix or delete the mode file, then restart the app, before toggling.",
                        "Rig Toggle",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

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
                            "Please choose at least one monitor to disable or enable in Settings before switching to Rig Mode.",
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

                        // CR-01 (code review): acquire the same exclusive-access lease
                        // OnTileAction already acquires before ITS confirm dialog, and
                        // for the identical reason (19-RESEARCH.md Pitfall 3) —
                        // ShowDialog() below runs a nested message loop that dispatches
                        // WM_HOTKEY, and ToggleOrchestrator's _busy guard does not
                        // engage until ToggleToRigMode() is actually called, further
                        // down, after the dialog closes. Without this lease, a
                        // concurrent hotkey/tray toggle (PerformBackgroundToggle, which
                        // calls the orchestrator directly with no dialog of its own)
                        // could mutate monitor/audio state while this dialog is still
                        // open. The lease is released BEFORE ToggleToRigMode() is
                        // called below — NOT held across it — because
                        // BeginExclusiveMonitorAccess() and ToggleToRigMode()'s
                        // RunGuarded() share the exact same _busy flag (by design, see
                        // ToggleOrchestrator.BeginExclusiveMonitorAccess's own doc
                        // comment); holding the lease into that call would make
                        // RunGuarded's own CompareExchange see busy=1 and immediately
                        // self-reject every Rig-mode toggle as "already in progress".
                        IDisposable? lease = TryAcquireMonitorAccess();
                        if (lease is null) return;

                        bool confirmed;
                        using (lease)
                        {
                            using var confirmDialog = new MonitorConfirmDialog(disableNames, enableNames, _themeProvider);
                            confirmed = confirmDialog.ShowDialog(this) == DialogResult.OK;

                            if (confirmed && confirmDialog.DontAskAgain)
                            {
                                settings.SkipMonitorConfirmation = true;
                                _settingsStore.Save(settings);
                            }
                        }

                        if (!confirmed)
                        {
                            return; // user cancelled — nothing mutated
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
        /// TILE-01/D-08: (re)populates the tile row from the ONE canonical
        /// DevicePath-ordered monitor list. GetAllMonitors()'s raw order is active
        /// monitors first then newly-discovered inactive targets appended, which can
        /// reshuffle across hotplug events -- sorting by DevicePath ordinally makes
        /// tile identity and numbering stable between refreshes. This sort is the
        /// single source of truth for tile position, tile number, AND (Plan 19-03)
        /// the Identify overlay numbers -- never re-derive an order anywhere else
        /// (19-RESEARCH.md Pitfall 6).
        /// </summary>
        private void RefreshMonitorTiles()
        {
            try
            {
                _lastKnownMonitors = _monitorController.GetAllMonitors()
                    .OrderBy(m => m.DevicePath, StringComparer.Ordinal)
                    .ToList();
            }
            catch (Exception ex)
            {
                // Defensive: enumeration must never crash the form -- degrade to the
                // empty state and leave a diagnostic trail (WR-02 convention).
                System.Diagnostics.Trace.WriteLine($"MainForm.RefreshMonitorTiles: GetAllMonitors failed: {ex}");
                _lastKnownMonitors = Array.Empty<MonitorInfo>();
            }

            if (_lastKnownMonitors.Count == 0)
            {
                foreach (MonitorTile tile in _tiles)
                {
                    tileStrip.Controls.Remove(tile);
                    tile.Dispose();
                }
                _tiles.Clear();

                tileStrip.Visible = false;
                lblNoMonitors.Visible = true;
                btnIdentify.Enabled = false;

                LayoutDashboard();
                return;
            }

            tileStrip.Visible = true;
            lblNoMonitors.Visible = false;
            btnIdentify.Enabled = true;

            // Reconcile tile controls to the monitor count rather than clearing and
            // rebuilding every refresh -- hotplug can fire repeatedly, and reusing
            // control instances avoids per-refresh handle churn over a long
            // tray-resident session (T-19-05).
            while (_tiles.Count < _lastKnownMonitors.Count)
            {
                MonitorTile tile = CreateTile();
                _tiles.Add(tile);
                tileStrip.Controls.Add(tile);
            }

            while (_tiles.Count > _lastKnownMonitors.Count)
            {
                MonitorTile surplus = _tiles[^1];
                tileStrip.Controls.Remove(surplus);
                _tiles.RemoveAt(_tiles.Count - 1);
                surplus.Dispose();
            }

            for (int i = 0; i < _lastKnownMonitors.Count; i++)
            {
                _tiles[i].SetState(_lastKnownMonitors[i], i + 1);
                tileToolTip.SetToolTip(_tiles[i], _tiles[i].AccessibleName);
            }

            ApplyDashboardTheming();
            LayoutDashboard();
        }

        /// <summary>
        /// TILE-01: constructs one MonitorTile sized/margined per the 19-UI-SPEC.md
        /// Tile & Layout Geometry Contract. ActionRequested is wired once at creation
        /// -- Plan 19-03 implements OnTileAction's mutation logic.
        /// </summary>
        private MonitorTile CreateTile()
        {
            var tile = new MonitorTile
            {
                Size = new Size(Scaled(TileWidthPx), Scaled(TileHeightPx)),
                Margin = new Padding(Scaled(TileMarginPx)),
            };
            tile.ActionRequested += (s, e) => OnTileAction((MonitorTile)s!);
            return tile;
        }

        /// <summary>
        /// 19-RESEARCH.md Pitfall 3: acquires the shared ToggleOrchestrator lease
        /// BEFORE MonitorConfirmDialog.ShowDialog() opens, because ShowDialog() runs
        /// a nested message loop that dispatches WM_HOTKEY -- without this, a
        /// hotkey-triggered toggle could start underneath a half-finished tile
        /// action. ToggleSwitch_ActionRequested (CR-01, code review) uses this same
        /// helper around ITS confirm dialog for the identical reason -- but releases
        /// the lease before calling ToggleToRigMode()/ToggleToNormalMode(), since
        /// those go through ToggleOrchestrator.RunGuarded(), which claims the exact
        /// same _busy flag this lease does; holding both at once would make every
        /// guarded toggle self-reject as "already in progress". OnTileAction never
        /// has this constraint because it mutates via
        /// WindowsMonitorController.DeactivateMonitors directly, bypassing
        /// RunGuarded entirely, so it can hold the lease across its whole action. A
        /// future editor must not merge or delete either guard, and must not widen
        /// ToggleSwitch_ActionRequested's lease scope to cover its RunGuarded call.
        /// </summary>
        private IDisposable? TryAcquireMonitorAccess()
        {
            try
            {
                return _orchestrator.BeginExclusiveMonitorAccess();
            }
            catch (ToggleInProgressException ex)
            {
                // Information (not Warning) matches how ToggleSwitch_ActionRequested
                // already classifies a busy rejection: an expected condition, not an error.
                MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
        }

        /// <summary>
        /// TILE-02/TILE-03: port of the retired standalone panel's
        /// DisableMonitor/EnableMonitor with zero simplification -- the only
        /// intentional changes are the entry point (a tile instead of a grid cell)
        /// and the refresh target (RefreshMonitorTiles() instead of
        /// PopulateMonitorGrid()). DISPLAY-12's
        /// zero-survivors guard is deliberately NOT duplicated here -- this method
        /// calls the same WindowsMonitorController.DeactivateMonitors both toggle
        /// directions already call, so the guard living solely in that adapter
        /// applies unchanged.
        /// </summary>
        private void OnTileAction(MonitorTile tile)
        {
            if (tile.DevicePath is not string devicePath) return;

            // DevicePath is the tile's only identity -- never its index in _tiles,
            // never its friendly name (the stable-identity rule the retired grid's
            // row Tag followed).
            MonitorInfo? monitor = _lastKnownMonitors.FirstOrDefault(m => m.DevicePath == devicePath);
            if (monitor is null) return;

            IDisposable? lease = TryAcquireMonitorAccess();
            if (lease is null) return;

            using (lease)
            {
                // The tile is presentational -- the active/inactive branch belongs
                // here, not inside MonitorTile (19-RESEARCH.md Pattern 1).
                if (monitor.IsActive)
                {
                    // WR-03 (code review): degrade to defaults rather than let an
                    // I/O hiccup (AV lock, concurrent Settings-Save) propagate
                    // unhandled out of a MonitorTile.ActionRequested event handler --
                    // matches ApplyTrayVisibility/MainForm_FormClosing/MainForm_Resize's
                    // existing defensive Load() pattern.
                    AppSettings settings;
                    try
                    {
                        settings = _settingsStore.Load();
                    }
                    catch
                    {
                        settings = new AppSettings();
                    }

                    if (!settings.SkipMonitorConfirmation)
                    {
                        // enableNames is always empty here -- TILE-03 gates Disable
                        // only; enabling can never trigger the zero-survivors
                        // failure mode, matching the retired panel exactly.
                        using var confirmDialog = new MonitorConfirmDialog(
                            new[] { monitor.FriendlyName },
                            Array.Empty<string>(),
                            _themeProvider);
                        if (confirmDialog.ShowDialog(this) != DialogResult.OK) return;
                        if (confirmDialog.DontAskAgain)
                        {
                            settings.SkipMonitorConfirmation = true;
                            _settingsStore.Save(settings);
                        }

                        // WR-03: ShowDialog()'s nested message loop dispatches
                        // queued messages including a hotplug-triggered
                        // OnDisplaySettingsChanged -> RefreshMonitorTiles(), which
                        // can have replaced _lastKnownMonitors while the dialog was
                        // open -- re-validate the pre-dialog snapshot before a
                        // possibly-unplugged device path reaches DeactivateMonitors.
                        if (!_lastKnownMonitors.Any(m => m.DevicePath == devicePath))
                        {
                            MessageBox.Show(this, "This monitor is no longer connected.", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            RefreshMonitorTiles();
                            return;
                        }
                    }

                    try
                    {
                        // The same method ToggleService.ToggleToRigMode and
                        // ToggleService.ToggleToNormalMode both call -- no
                        // monitor-count pre-check is written here; the
                        // zero-survivors guard lives solely in
                        // WindowsMonitorController.DeactivateMonitors (DISPLAY-12).
                        _monitorController.DeactivateMonitors(new HashSet<string> { devicePath });
                    }
                    catch (InvalidOperationException ex)
                    {
                        // One shared codepath, one shared message -- ex.Message
                        // reused verbatim, never reworded for tile context.
                        MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"{ex.GetType().Name}: {ex.Message}", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    finally
                    {
                        RefreshMonitorTiles();
                    }
                }
                else
                {
                    try
                    {
                        _monitorController.ActivateMonitors(new HashSet<string> { devicePath });
                    }
                    catch (InvalidOperationException ex)
                    {
                        MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"{ex.GetType().Name}: {ex.Message}", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    finally
                    {
                        RefreshMonitorTiles();
                    }
                }
            }
        }

        /// <summary>
        /// TILE-04: port of the retired standalone panel's BtnIdentify_Click with
        /// exactly two deliberate changes: the iteration source (the canonical
        /// _lastKnownMonitors list instead of DataGridView rows, which no longer
        /// exist) and the counter semantics (increments even on skip -- see below).
        /// </summary>
        private void BtnIdentify_Click(object? sender, EventArgs e)
        {
            MonitorState state;
            try
            {
                state = _monitorController.CaptureState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"{ex.GetType().Name}: {ex.Message}", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Tolerant of duplicate keys, ported as-is.
            var snapshotsByPath = state.Paths.GroupBy(s => s.DevicePath).ToDictionary(g => g.Key, g => g.First());

            // Identify is read-only (CaptureState() mutates no topology), so this
            // handler does not take any exclusive access lease -- blocking it during
            // a toggle would be gratuitous. Ported rationale, unchanged.
            //
            // Iterate the same canonical _lastKnownMonitors list RefreshMonitorTiles()
            // used to number the tiles, so overlay number N and tile number N always
            // name the same physical monitor (19-RESEARCH.md Pitfall 6).
            int number = 1;
            foreach (MonitorInfo monitor in _lastKnownMonitors)
            {
                // An OS-disabled monitor has no active desktop surface, so
                // CaptureState() legitimately has no coordinates for it -- skip.
                // DIVERGENCE from the retired panel: the retired panel did NOT
                // increment the counter on skip. Incrementing here is required so
                // an OS-disabled monitor still consumes its ordinal and every later
                // overlay keeps matching its tile -- do not "fix" this back to the
                // retired panel's behavior.
                if (!snapshotsByPath.TryGetValue(monitor.DevicePath, out MonitorPathSnapshot? snapshot))
                {
                    number++;
                    continue;
                }

                // Defensive against a degenerate CCD mode record -- a zero-size
                // overlay would be invisible and look unclosable.
                if (snapshot.ResolutionWidth <= 0 || snapshot.ResolutionHeight <= 0)
                {
                    number++;
                    continue;
                }

                try
                {
                    // A single overlay's construction failing (GDI exhaustion, etc.)
                    // must not abort the remaining overlays or throw out of this
                    // Button.Click handler. Owner = this retargets the overlay's
                    // lifetime to this window (the retired panel targeted itself),
                    // so no dangling ownerless TopMost window can survive.
                    var overlay = new MonitorIdentifyOverlay(snapshot, number) { Owner = this };
                    overlay.Show();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"BtnIdentify_Click: overlay for {monitor.DevicePath} failed: {ex}");
                }

                number++;
            }
        }

        /// <summary>
        /// D-04/D-06/D-09: computes every dashboard position and the form's own
        /// ClientSize arithmetically from tile count and the font-derived Scaled()
        /// factor -- no dependence on a WinForms layout pass having run. This must
        /// produce the correct window size on the FIRST Show() after a --tray hidden
        /// start, where InitializeTrayState() -- not OnLoad/OnShown -- does the
        /// population work, and 19-RESEARCH.md Open Question 2 flags
        /// Form.AutoSize/FlowLayoutPanel.AutoSize timing on that path as unproven on
        /// this runtime.
        /// </summary>
        private void LayoutDashboard()
        {
            try
            {
                SuspendLayout();

                int margin = Scaled(MarginPx);

                int count = _tiles.Count;
                int perRow = Math.Min(Math.Max(count, 1), MaxTilesPerRow);
                int rows = count == 0 ? 0 : (int)Math.Ceiling(count / (double)MaxTilesPerRow);
                int cellW = Scaled(TileWidthPx + 2 * TileMarginPx);
                int cellH = Scaled(TileHeightPx + 2 * TileMarginPx);
                int stripW = perRow * cellW;
                int stripH = rows * cellH;

                // 2026-08-10 rig fix: branch on `count > 0`, never on
                // `tileStrip.Visible`. Control.Visible is not a simple local flag
                // -- the getter cascades through the whole ancestor chain, so it
                // reads false whenever an ancestor (here, MainForm itself before
                // Form.Show() has ever run) isn't visible yet, even though we set
                // tileStrip.Visible = true moments earlier in RefreshMonitorTiles().
                // InitializeTrayState() calls RefreshMonitorTiles() before the form
                // is ever shown on every startup path, so this branch was silently
                // always false on first layout, undersizing contentBottom by the
                // full tile-strip height and overlapping every control below it
                // with the tile row (rig-confirmed via the diagnostic trace below).
                bool hasMonitors = count > 0;

                // 19-UI-SPEC.md content-width floor: keeps the window from shrinking
                // narrower than the existing toggle-button width at one or two
                // monitors.
                int contentWidth = Math.Max(Scaled(ContentWidthFloorPx), hasMonitors ? stripW : Scaled(ContentWidthFloorPx));

                // 20-UI-SPEC.md Layout Integration (D-06 resolved): the ~28px of
                // vertical space the old mode-text label's removal frees is COLLAPSED,
                // not preserved as breathing room -- once the switch row is the sole
                // mode readout (D-06), an empty gap there would read as an unexplained
                // layout hole, so the tile strip now starts flush with the top margin
                // and the window stays exactly as tall as its content needs.
                int stripTop = margin;

                tileStrip.Size = new Size(stripW, stripH);
                tileStrip.Location = new Point(margin + (contentWidth - stripW) / 2, stripTop);

                lblNoMonitors.Location = new Point(margin, stripTop);
                lblNoMonitors.Size = new Size(contentWidth, Scaled(EmptyStateHeightPx));

                int contentBottom = stripTop + (hasMonitors ? stripH : Scaled(EmptyStateHeightPx));

                // 2026-08-10 rig fix round 2 (user feedback, post-check-11): Identify
                // and the toggle row used to stack vertically (Identify centered,
                // toggle right-aligned below it), which read as visually loose --
                // two unrelated-looking rows with a lot of dead horizontal space on
                // Identify's row. They now share ONE action row -- Identify in the
                // left corner, the switch in the right corner -- since both are the
                // same Scaled(32) height, so no separate vertical-centering math is
                // needed within the row. Tab order (tiles -> Identify -> toggle ->
                // Settings, TILE-05/success criterion 3a) is unaffected: it comes
                // from Controls.Add order, not Location.
                int actionRowY = contentBottom + Scaled(GapMdPx);

                btnIdentify.Size = new Size(Scaled(IdentifyWidthPx), Scaled(IdentifyHeightPx));
                btnIdentify.Location = new Point(margin, actionRowY);

                // Sized to its own measured content (ToggleSwitch.GetPreferredSize,
                // rig fix round 1) and right-aligned to the same right edge as
                // btnSettings/tiles below -- now sharing btnIdentify's row instead of
                // sitting on its own row underneath it.
                int toggleRowHeight = Scaled(ToggleRowHeightPx);
                int toggleRowWidth = toggleSwitch.GetPreferredSize(new Size(0, toggleRowHeight)).Width;
                toggleSwitch.Size = new Size(toggleRowWidth, toggleRowHeight);
                toggleSwitch.Location = new Point(margin + contentWidth - toggleRowWidth, actionRowY);

                int actionRowBottom = Math.Max(btnIdentify.Bottom, toggleSwitch.Bottom);

                // MAIN-02's spacing cue: the deliberately larger GapLgPx gap above the
                // gear must read as separated from the action row, not as the next
                // item in the same stack.
                btnSettings.Size = new Size(Scaled(GearSizePx), Scaled(GearSizePx));
                btnSettings.Location = new Point(margin + contentWidth - btnSettings.Width, actionRowBottom + Scaled(GapLgPx));

                ClientSize = new Size(contentWidth + 2 * margin, btnSettings.Bottom + margin);

                ResumeLayout(true);
            }
            catch (Exception ex)
            {
                // A layout failure must never crash the toggle flow.
                System.Diagnostics.Trace.WriteLine($"MainForm.LayoutDashboard failed: {ex}");
            }
        }

        /// <summary>
        /// 19-RESEARCH.md Pitfall 1: the single helper both theming call sites
        /// (OnThemeChanged, InitializeTrayState()) invoke for every dashboard control,
        /// so the two-call-site rule cannot drift. Hard constraint 3/20-UI-SPEC.md
        /// Pitfall 1: the toggle switch is themed from here too -- routing it through
        /// this single helper, rather than adding two direct calls in OnThemeChanged
        /// and InitializeTrayState(), is what makes the two call sites structurally
        /// unable to drift (the bug class this codebase shipped twice in Phase 12).
        /// </summary>
        private void ApplyDashboardTheming()
        {
            foreach (MonitorTile tile in _tiles)
            {
                ThemeApplier.ThemeMonitorTile(tile, IsDark);
            }

            ThemeApplier.ThemeButton(btnIdentify, IsDark);
            ThemeApplier.ThemeButton(btnSettings, IsDark);
            ThemeApplier.ThemeToggleSwitch(toggleSwitch, IsDark);
            // The gear glyph is painted from btnSettings.ForeColor, which
            // ThemeButton just changed -- force a repaint so it doesn't wait for the
            // next incidental invalidation.
            btnSettings.Invalidate();

            lblNoMonitors.ForeColor = IsDark ? Color.FromArgb(160, 160, 160) : SystemColors.GrayText;
        }

        /// <summary>
        /// TILE-06/T-19-04: hotplug refresh handler, ported from the retired
        /// standalone panel's OnDisplaySettingsChanged's exact IsDisposed ->
        /// InvokeRequired -> BeginInvoke -> try/catch marshalling shape. Unlike the
        /// panel's version, this handler's IsDisposed guard covers real process exit
        /// rather than a close-while-event-in-flight race, because MainForm is never
        /// closed during normal tray-resident operation.
        /// </summary>
        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => OnDisplaySettingsChanged(sender, e)));
                }
                catch (ObjectDisposedException)
                {
                }
                return;
            }

            try
            {
                RefreshMonitorTiles();
            }
            catch
            {
                // A hotplug notification must never crash the form.
            }
        }

        /// <summary>
        /// Manual hover/press fill for btnIdentify/btnSettings, bypassing
        /// FlatAppearance.MouseOverBackColor/MouseDownBackColor entirely (rig-proven
        /// unreliable, see the field comments above). Reads the three colors
        /// ThemeApplier.ThemeButton already stored on the button (BackColor plus its
        /// two FlatAppearance backing colors) so the palette stays defined in exactly
        /// one place.
        /// </summary>
        private static Color ManualButtonFill(Button button, bool hovered, bool pressed)
        {
            if (pressed) return button.FlatAppearance.MouseDownBackColor;
            if (hovered) return button.FlatAppearance.MouseOverBackColor;
            return button.BackColor;
        }

        /// <summary>
        /// 2026-08-10 rig report: Tab-focusing btnIdentify/btnSettings showed no
        /// focus indicator at all. Root cause: ManualButtonFill's FillRectangle
        /// covers the button's entire ClientRectangle, painting over WinForms'
        /// native dotted focus-cue rectangle that would otherwise have been drawn
        /// as part of the button's own base painting, before our Paint handler
        /// runs. Draws an explicit accent-colored ring instead -- the same visual
        /// language and color source (ThemeApplier.ThemeMonitorTile's AccentColor)
        /// MonitorTile's own focus ring already uses, so keyboard users get one
        /// consistent focus cue across tiles and buttons. The toggle switch draws
        /// its own pill-shaped focus ring inside ToggleSwitch.OnPaint instead of
        /// reusing this helper, because a rectangle is the wrong shape for a pill.
        /// </summary>
        private static void DrawButtonFocusRing(Graphics g, Rectangle bounds, Color accentColor)
        {
            float penWidth = Math.Max(1f, bounds.Height * (2f / 32f));
            var ringRect = new RectangleF(
                penWidth / 2f, penWidth / 2f,
                bounds.Width - penWidth, bounds.Height - penWidth);
            using var ringPen = new Pen(accentColor, penWidth);
            g.DrawRectangle(ringPen, ringRect.X, ringRect.Y, ringRect.Width, ringRect.Height);
        }

        /// <summary>
        /// 2026-08-10 rig round 2 (user feedback): with the toggle switch's pill
        /// shape and the tiles' rounded corners as its row/column neighbours,
        /// btnIdentify's sharp-cornered FillRectangle/DrawButtonFocusRing read as
        /// visually out of place -- "the only square thing". Gives Identify the
        /// same subtle corner rounding as MonitorTile (4px at 100% scale, height-
        /// derived to avoid a bare pixel literal, Pitfall 9) rather than the
        /// switch's full pill curve, since Identify sits in the tile-language
        /// register, not the switch's. btnSettings is left untouched (explicit
        /// user sign-off: "everything else looks good now").
        /// </summary>
        private static GraphicsPath BuildRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2f;

            if (diameter <= 0f)
            {
                path.AddRectangle(rect);
                return path;
            }

            var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

            path.StartFigure();
            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
        }

        /// <summary>
        /// TILE-04/D-11: paints btnIdentify's background (hover/press-aware, see
        /// ManualButtonFill), its text, and a focus ring when Tab-focused --
        /// the same never-crash-on-paint rule the tile follows.
        /// </summary>
        private void BtnIdentify_Paint(object? sender, PaintEventArgs e)
        {
            try
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                RectangleF bounds = btnIdentify.ClientRectangle;
                float cornerRadius = bounds.Height * (4f / 32f);

                using var fillPath = BuildRoundedRect(bounds, cornerRadius);
                using var fillBrush = new SolidBrush(ManualButtonFill(btnIdentify, _identifyHovered, _identifyPressed));
                e.Graphics.FillPath(fillBrush, fillPath);

                TextRenderer.DrawText(
                    e.Graphics,
                    btnIdentify.Text,
                    btnIdentify.Font,
                    btnIdentify.ClientRectangle,
                    btnIdentify.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                if (btnIdentify.Focused)
                {
                    float penWidth = Math.Max(1f, bounds.Height * (2f / 32f));
                    var ringRect = new RectangleF(
                        penWidth / 2f, penWidth / 2f,
                        bounds.Width - penWidth, bounds.Height - penWidth);
                    using var ringPath = BuildRoundedRect(ringRect, Math.Max(0f, cornerRadius - penWidth / 2f));
                    using var ringPen = new Pen(AccentColor, penWidth);
                    e.Graphics.DrawPath(ringPen, ringPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"MainForm.BtnIdentify_Paint failed: {ex}");
            }
        }

        private void BtnIdentify_Enter(object? sender, EventArgs e) => btnIdentify.Invalidate();

        private void BtnIdentify_Leave(object? sender, EventArgs e) => btnIdentify.Invalidate();

        private void BtnIdentify_MouseEnter(object? sender, EventArgs e)
        {
            _identifyHovered = true;
            btnIdentify.Invalidate();
        }

        private void BtnIdentify_MouseLeave(object? sender, EventArgs e)
        {
            _identifyHovered = false;
            _identifyPressed = false;
            btnIdentify.Invalidate();
        }

        private void BtnIdentify_MouseDown(object? sender, MouseEventArgs e)
        {
            _identifyPressed = true;
            btnIdentify.Invalidate();
        }

        private void BtnIdentify_MouseUp(object? sender, MouseEventArgs e)
        {
            _identifyPressed = false;
            btnIdentify.Invalidate();
        }

        /// <summary>
        /// MAIN-02/D-10: paints btnSettings's background (hover/press-aware, see
        /// ManualButtonFill) then the procedural gear glyph on top -- the same
        /// never-crash-on-paint rule the tile follows.
        /// </summary>
        private void BtnSettings_Paint(object? sender, PaintEventArgs e)
        {
            try
            {
                using var fillBrush = new SolidBrush(ManualButtonFill(btnSettings, _settingsHovered, _settingsPressed));
                e.Graphics.FillRectangle(fillBrush, btnSettings.ClientRectangle);

                Rectangle bounds = btnSettings.ClientRectangle;
                float inset = Math.Min(bounds.Width, bounds.Height) * 0.2f;
                var glyphBounds = new RectangleF(
                    bounds.X + inset,
                    bounds.Y + inset,
                    bounds.Width - 2 * inset,
                    bounds.Height - 2 * inset);

                MonitorIconGeometry.DrawGearIcon(e.Graphics, glyphBounds, btnSettings.ForeColor);

                if (btnSettings.Focused)
                {
                    DrawButtonFocusRing(e.Graphics, btnSettings.ClientRectangle, AccentColor);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"MainForm.BtnSettings_Paint failed: {ex}");
            }
        }

        private void BtnSettings_Enter(object? sender, EventArgs e) => btnSettings.Invalidate();

        private void BtnSettings_Leave(object? sender, EventArgs e) => btnSettings.Invalidate();

        private void BtnSettings_MouseEnter(object? sender, EventArgs e)
        {
            _settingsHovered = true;
            btnSettings.Invalidate();
        }

        private void BtnSettings_MouseLeave(object? sender, EventArgs e)
        {
            _settingsHovered = false;
            _settingsPressed = false;
            btnSettings.Invalidate();
        }

        private void BtnSettings_MouseDown(object? sender, MouseEventArgs e)
        {
            _settingsPressed = true;
            btnSettings.Invalidate();
        }

        private void BtnSettings_MouseUp(object? sender, MouseEventArgs e)
        {
            _settingsPressed = false;
            btnSettings.Invalidate();
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
        private void TrayToggleMenuItem_Click(object? sender, EventArgs e) => PerformBackgroundToggle();

        /// <summary>
        /// TRIG-01: the global-hotkey toggle handler, dispatched from WndProc on
        /// WM_HOTKEY. Structurally identical to TrayToggleMenuItem_Click (D-03) --
        /// same skip-the-GUI posture (no DISPLAY-07 confirm dialog, no WR-01 config
        /// guard, since the main window may be hidden to tray or the hotkey pressed
        /// mid-game). CRITICAL (D-08 no-chrome guarantee): every branch below routes
        /// through notifyIcon.ShowBalloonTip, NEVER MessageBox.Show -- a hotkey-triggered
        /// toggle must never surface GUI chrome.
        /// </summary>
        private void HandleHotkeyToggle() => PerformBackgroundToggle();

        /// <summary>
        /// WR-05 (code review): shared body for TrayToggleMenuItem_Click and
        /// HandleHotkeyToggle, which were previously line-for-line identical method
        /// bodies — extracted so any future fix (balloon wording, exception handling)
        /// only needs to be made once. See the two callers' doc comments for the
        /// no-GUI-chrome contract this method upholds.
        /// </summary>
        private void PerformBackgroundToggle()
        {
            if (!_orchestrator.IsModeKnown())
            {
                // T-16-09/D-08: balloon chrome only, never MessageBox, for the
                // tray/hotkey triggers — matches their existing no-GUI-chrome guarantee.
                notifyIcon.ShowBalloonTip(
                    3000,
                    "Rig Toggle",
                    "Current mode is unknown — restart the app to fix, or check Settings.",
                    ToolTipIcon.Warning);
                return;
            }

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

            // WR-03 (code review): OpenSettingsDialog calls this unguarded after every
            // Settings-dialog close (a very common, everyday interaction) -- degrade to
            // "nothing configured" defaults rather than let a settings.json read
            // failure propagate out, matching this file's established defensive
            // Load() pattern. RegisterHotkeyAtStartup's own try/catch around its call
            // to this method remains as a second, harmless layer of the same guard.
            AppSettings settings;
            try
            {
                settings = _settingsStore.Load();
            }
            catch
            {
                settings = new AppSettings();
            }

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
