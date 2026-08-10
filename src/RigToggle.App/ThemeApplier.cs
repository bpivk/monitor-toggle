using System.Drawing;
using System.Windows.Forms;
using RigToggle.App.Controls;

namespace RigToggle.App
{
    /// <summary>
    /// Targeted per-control recolor helpers for the confirmed gaps Application.SetColorMode
    /// does not reach: the dgvMonitors DataGridView (dotnet/winforms#11893), txtHotkey's
    /// hand-rolled SystemColors.* state machine (12-RESEARCH.md Pitfall 8 — those literal
    /// SystemColors.* assignments do NOT follow SetColorMode and were silently resetting
    /// the control to light-mode on every user interaction), and -- as of 12-REVIEW.md
    /// CR-02/CR-03 -- Button and ComboBox. The Windows 11 rig test in 12-04 proved Button
    /// (FlatStyle.System) and ComboBox (DropDownList) fill+text are NOT actually recolored
    /// by SetColorMode on this runtime, so they now get the same explicit-override
    /// treatment as the grid and hotkey box, for the same reason. Every method here is
    /// idempotent (safe to call once at startup and again on every live theme flip) and
    /// never throws — this is cosmetic-only code and a theming failure must never crash
    /// Settings save/load (T-12-02). Deliberately NOT a recursive Controls-tree walk: every
    /// override here targets a specific control instance passed by the caller, never a
    /// blanket Controls-tree walk over base controls SetColorMode otherwise handles
    /// correctly (12-RESEARCH.md Pitfall 1/8).
    /// </summary>
    internal static class ThemeApplier
    {
        /// <summary>
        /// dgvMonitors theming (12-RESEARCH.md Pattern 5, verified against the grid's
        /// actual 3-column mixed text+checkbox shape). EnableHeadersVisualStyles MUST be
        /// set false before any ColumnHeadersDefaultCellStyle assignment takes effect —
        /// a well-known WinForms gotcha, not optional ordering.
        /// </summary>
        public static void ThemeMonitorGrid(DataGridView grid, bool dark)
        {
            try
            {
                grid.EnableHeadersVisualStyles = false;
                grid.BackgroundColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Window;
                grid.GridColor = dark ? Color.FromArgb(70, 70, 70) : SystemColors.ControlLight;
                grid.DefaultCellStyle.BackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Window;
                grid.DefaultCellStyle.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
                grid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;
                grid.DefaultCellStyle.SelectionForeColor = dark ? Color.White : SystemColors.HighlightText;
                grid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
            }
            catch
            {
                // Cosmetic-only — leave the grid exactly as it currently renders on failure.
            }
        }

        /// <summary>
        /// txtHotkey Idle/Configured state (a hotkey combo is set and displayed).
        /// Replaces the hardcoded SystemColors.Window/WindowText this state used to
        /// carry in RenderHotkeyIdleDisplay's Configured branch (Pitfall 8).
        /// </summary>
        public static void ApplyHotkeyIdleConfigured(TextBox textBox, bool dark)
        {
            try
            {
                textBox.BackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Window;
                textBox.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.WindowText;
            }
            catch
            {
                // Cosmetic-only — leave the control unchanged on failure.
            }
        }

        /// <summary>
        /// txtHotkey Idle/Unconfigured state (no hotkey set — muted placeholder text).
        /// Replaces the hardcoded SystemColors.Window/GrayText this state used to carry
        /// in RenderHotkeyIdleDisplay's Unconfigured branch (Pitfall 8).
        /// </summary>
        public static void ApplyHotkeyIdleUnconfigured(TextBox textBox, bool dark)
        {
            try
            {
                textBox.BackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Window;
                textBox.ForeColor = dark ? Color.FromArgb(160, 160, 160) : SystemColors.GrayText;
            }
            catch
            {
                // Cosmetic-only — leave the control unchanged on failure.
            }
        }

        /// <summary>
        /// txtHotkey Recording state (actively capturing a key combination) — the one
        /// genuine accent-color moment outside the grid's own selection highlight.
        /// Replaces the hardcoded SystemColors.Info/WindowText this state used to carry
        /// in TxtHotkey_MouseDown (Pitfall 8).
        /// </summary>
        public static void ApplyHotkeyRecording(TextBox textBox, bool dark)
        {
            try
            {
                textBox.BackColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Info;
                textBox.ForeColor = dark ? Color.White : SystemColors.WindowText;
            }
            catch
            {
                // Cosmetic-only — leave the control unchanged on failure.
            }
        }

        /// <summary>
        /// Button theming (12-REVIEW.md CR-02). The Windows 11 rig proved FlatStyle.System
        /// buttons do NOT pick up dark-mode coloring from Application.SetColorMode on this
        /// runtime, so this replaces the native FlatStyle.System rendering pipeline with
        /// explicit FlatStyle.Flat + palette colors -- the same explicit-override approach
        /// already working for the grid and hotkey box.
        ///
        /// Why BorderSize=0 + explicit MouseOverBackColor/MouseDownBackColor rather than
        /// relying on FlatStyle.Flat's normal auto-apply pipeline: dotnet/winforms#13897
        /// (open) documents that FlatAppearance border/hover/pressed colors don't reliably
        /// apply once dark mode is active when FlatStyle.Flat is used, producing visually
        /// broken buttons (a stock light-colored flash on hover/press even though the rest
        /// of the button is themed dark). Setting BorderSize=0 (NOT 1) sidesteps the
        /// unreliable border-color application entirely, and pinning MouseOverBackColor/
        /// MouseDownBackColor explicitly (rather than leaving them to auto-derive from
        /// BackColor) keeps hover/pressed states dark-themed instead of hitting the #13897
        /// bug -- this is 12-REVIEW.md CR-02 "Fix Option 2".
        /// </summary>
        public static void ThemeButton(Button button, bool dark)
        {
            try
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.BackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
                button.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
                // 2026-08-10 rig report: BorderSize=0 plus the original hover/press
                // deltas (dark 45,45,48 -> 62,62,66 / 28,28,30) read as "no button
                // feedback at all, like a hyperlink." Widened both deltas so hover
                // and press are unmistakable at a glance without reintroducing
                // FlatAppearance.BorderColor (the #13897 dark-mode bug this method
                // exists to avoid).
                button.FlatAppearance.MouseOverBackColor = dark ? Color.FromArgb(80, 80, 86) : SystemColors.ControlLight;
                button.FlatAppearance.MouseDownBackColor = dark ? Color.FromArgb(18, 18, 20) : SystemColors.ControlDarkDark;
            }
            catch
            {
                // Cosmetic-only — leave the control unchanged on failure.
            }
        }

        /// <summary>
        /// Audio-device ComboBox theming (12-REVIEW.md CR-03). cboAudioNormal/cboAudioRig
        /// are DropDownList-style combos that the Windows 11 rig proved are never recolored
        /// by SetColorMode -- a documented WinForms gap even where SetColorMode is
        /// otherwise effective (edit/list portions often need explicit BackColor/ForeColor/
        /// FlatStyle overrides to follow dark mode).
        /// </summary>
        public static void ThemeComboBox(ComboBox combo, bool dark)
        {
            try
            {
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Window;
                combo.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.WindowText;
            }
            catch
            {
                // Cosmetic-only — leave the control unchanged on failure.
            }
        }

        /// <summary>
        /// TILE-01/19-UI-SPEC.md Color table: the tile half of 19-RESEARCH.md Pitfall 1's
        /// two-call-site rule — both MainForm.OnThemeChanged AND
        /// MainForm.InitializeTrayState() must reach every live MonitorTile through this
        /// method (via MainForm.ApplyDashboardTheming), or a tile themed only at startup
        /// freezes at whatever theme Windows was in while its neighbours keep flipping —
        /// this codebase already shipped that exact bug twice in Phase 12.
        ///
        /// AccentColor is deliberately confined to the tile's ON-state icon fill and its
        /// keyboard-focus ring (19-UI-SPEC.md "Accent reserved for" rule) — it must never
        /// be spread to the Identify button, the Settings gear, or the tile's hover tint,
        /// all of which stay on the existing gray-scale ThemeButton palette.
        ///
        /// The primary-monitor badge color is deliberately NOT set here — it is a locked
        /// theme-independent literal owned by MonitorIconGeometry.DrawPrimaryBadge,
        /// exactly like MonitorPanelForm.CreateStatusDot's status-dot literals — a future
        /// editor should not thread `dark` into it.
        /// </summary>
        public static void ThemeMonitorTile(MonitorTile tile, bool dark)
        {
            try
            {
                tile.BackColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
                tile.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
                tile.AccentColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;
                tile.FocusRingColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;
                tile.IconOffColor = dark ? Color.FromArgb(160, 160, 160) : SystemColors.GrayText;
                tile.HoverBackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.ControlLight;
                tile.Invalidate();
            }
            catch
            {
                // Cosmetic-only — leave the control unchanged on failure.
            }
        }
    }
}
