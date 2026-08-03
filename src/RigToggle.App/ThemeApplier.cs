using System.Drawing;
using System.Windows.Forms;

namespace RigToggle.App
{
    /// <summary>
    /// Targeted per-control recolor helpers for the two confirmed SettingsForm gaps
    /// Application.SetColorMode does not reach: the dgvMonitors DataGridView
    /// (dotnet/winforms#11893) and txtHotkey's hand-rolled SystemColors.* state
    /// machine (12-RESEARCH.md Pitfall 8 — those literal SystemColors.* assignments do
    /// NOT follow SetColorMode and were silently resetting the control to light-mode on
    /// every user interaction). Every method here is idempotent (safe to call once at
    /// startup and again on every live theme flip) and never throws — this is
    /// cosmetic-only code and a theming failure must never crash Settings save/load
    /// (T-12-02). Deliberately NOT a recursive Controls-tree walk: base controls
    /// (Label/TextBox/ComboBox/CheckBox/Button fill+text) are already owned by
    /// SetColorMode, and adding overrides there would fight it (12-RESEARCH.md
    /// Pitfall 1/8).
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
    }
}
