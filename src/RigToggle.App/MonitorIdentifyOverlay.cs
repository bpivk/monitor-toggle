using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using RigToggle.Core.Models;

namespace RigToggle.App
{
    /// <summary>
    /// PANEL-05: a borderless, topmost, full-screen per-monitor overlay showing an
    /// ordinal identify number, used by MainForm's Identify action on the
    /// monitor-tile dashboard (TILE-04) to let the user visually map tiles to
    /// physical monitors.
    ///
    /// Positioned and sized exclusively from a CCD-sourced <see cref="MonitorPathSnapshot"/>
    /// (from IMonitorController.CaptureState()) — never from
    /// <see cref="System.Windows.Forms.Screen.AllScreens"/>. 17-RESEARCH.md Pitfall 2:
    /// Screen.DeviceName and MonitorInfo.DevicePath are disjoint identity spaces with no
    /// built-in mapping, so using Screen.AllScreens here would risk placing an overlay on
    /// the wrong physical monitor.
    ///
    /// Auto-closes after <see cref="AutoCloseMilliseconds"/> with no user interaction
    /// required. Mixed-DPI placement accuracy (correct monitor, full coverage, no offset)
    /// is only provable on real rig hardware (17-RESEARCH.md Pitfall 3 —
    /// RigToggle.App.csproj sets no ApplicationHighDpiMode) and is verified in Plan 04's
    /// rig checkpoint, not by this build environment.
    /// </summary>
    public sealed class MonitorIdentifyOverlay : Form
    {
        private const int AutoCloseMilliseconds = 2500;

        private readonly Font _displayFont;
        private readonly System.Windows.Forms.Timer _autoCloseTimer;

        public MonitorIdentifyOverlay(MonitorPathSnapshot snapshot, int number)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

            // Position and size come exclusively from the CCD-sourced snapshot, never
            // Screen.AllScreens (17-RESEARCH.md Pitfall 2). Bound to locals on their own
            // lines for clarity on exactly which CCD fields drive placement.
            int x = snapshot.PositionX;
            int y = snapshot.PositionY;
            int width = snapshot.ResolutionWidth;
            int height = snapshot.ResolutionHeight;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(x, y);
            Size = new Size(width, height);
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            ControlBox = false;
            Text = string.Empty;

            // 17-UI-SPEC.md Typography "Display" role: the one deliberate exception to
            // this app's single-font-single-size discipline. Stored in a field (not
            // constructed inline) so it can be disposed explicitly below — Fonts are not
            // disposed by the owning control.
            _displayFont = new Font("Segoe UI", 120f, FontStyle.Bold);

            var lbl = new Label
            {
                Text = number.ToString(CultureInfo.InvariantCulture),
                ForeColor = Color.White,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = _displayFont,
            };
            Controls.Add(lbl);

            // 17-UI-SPEC.md: auto-close after AutoCloseMilliseconds, no user interaction.
            _autoCloseTimer = new System.Windows.Forms.Timer { Interval = AutoCloseMilliseconds };
            _autoCloseTimer.Tick += (_, _) =>
            {
                _autoCloseTimer.Stop();
                Close();
            };
            Shown += (_, _) => _autoCloseTimer.Start();
        }

        // Displaying N overlays (one per active monitor) must never steal focus from the
        // panel the user just clicked "Identify" in — ShowWithoutActivation keeps every
        // overlay non-activating while still TopMost/visible.
        protected override bool ShowWithoutActivation => true;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoCloseTimer.Stop();
                _autoCloseTimer.Dispose();
                _displayFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
