using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RigToggle.App.Controls
{
    /// <summary>
    /// THEME-08/20-UI-SPEC.md: the three states this switch renders. Off maps
    /// to Normal mode (D-08 -- Normal is this project's baseline state
    /// everywhere else, so it maps to the neutral/left/off reading), On maps
    /// to Rig mode (D-08), and Indeterminate is DISPLAY-11's unknown-mode
    /// state (D-07), which must never be mistaken for either real mode.
    /// </summary>
    public enum ToggleSwitchState
    {
        Off,
        On,
        Indeterminate,
    }

    /// <summary>
    /// THEME-08: a presentational-only, owner-drawn toggle switch replacing
    /// the old plain-button toggle. This control NEVER calls ToggleOrchestrator,
    /// IMonitorController, or ISettingsStore, and it never reads
    /// IThemeProvider -- it only raises ActionRequested and MainForm owns
    /// every gate (unknown-mode gate, WR-01 configured gate, confirmation
    /// dialog) exactly as it does today.
    ///
    /// All paint geometry is fractional, derived from ClientSize (Pitfall 9)
    /// -- RigToggle.App.csproj sets no ApplicationHighDpiMode, so
    /// AutoScaleMode.Font has zero visibility into custom paint code.
    ///
    /// Track and thumb are separate shapes, drawn back to front, because
    /// unioning them into one GraphicsPath before an outline pass reproduces
    /// the Phase 13 DrawPath seam-artifact bug (Pitfall 2).
    ///
    /// There is no animation of any kind -- every state change is an instant
    /// Invalidate() redraw (slide animation is out of scope per
    /// REQUIREMENTS.md).
    ///
    /// Code-only control (no InitializeComponent, no .Designer.cs, no
    /// .resx), matching Controls/MonitorTile.cs's existing convention.
    /// </summary>
    public sealed class ToggleSwitch : UserControl
    {
        // 20-UI-SPEC.md Geometry Contract, expressed as fractions of the
        // control's own ClientSize -- never a bare pixel literal inside
        // OnPaint (Pitfall 9). These are the ONLY place multi-digit numbers
        // appear in this file.

        /// <summary>Track height as a fraction of ClientSize.Height.</summary>
        private const float TrackHeightFraction = 28f / 32f;

        /// <summary>
        /// Track width = trackHeight * this. Derived from HEIGHT, never
        /// width, so the compact switch never stretches into a full-width
        /// pill as MainForm's content width grows with monitor count (D-01,
        /// hard constraint 3).
        /// </summary>
        private const float TrackAspectRatio = 52f / 28f;

        /// <summary>
        /// Thumb inset from the track edge, as a fraction of trackHeight.
        /// Makes thumbDiameter + 2 * inset == trackHeight exactly, so the
        /// thumb always touches both track edges vertically.
        /// </summary>
        private const float ThumbInsetFraction = 4f / 28f;

        /// <summary>Gap between the label's right edge and the track's left edge, as a fraction of ClientSize.Height.</summary>
        private const float LabelGapFraction = 4f / 32f;

        /// <summary>Off-state track outline stroke width.</summary>
        private const float OutlineWidthFraction = 2f / 32f;

        /// <summary>Thumb outline stroke width.</summary>
        private const float ThumbOutlineWidthFraction = 1f / 32f;

        private const float FocusRingWidthFraction = 2f / 32f;

        /// <summary>The ControlPaint.Light/ControlPaint.Dark factor for the On/Indeterminate hover and press variants (20-UI-SPEC.md Color formula table).</summary>
        private const float HoverPressShiftFactor = 0.2f;

        // Safe non-throwing defaults, matching MonitorTile's field discipline.
        private Color _onColor = SystemColors.Highlight;
        private Color _offOutlineColor = SystemColors.GrayText;
        private Color _offHoverFillColor = SystemColors.ControlLight;
        private Color _offPressFillColor = SystemColors.ControlDarkDark;
        private Color _indeterminateColor = Color.FromArgb(200, 200, 200);
        private Color _thumbColor = Color.White;
        private Color _thumbOutlineColor = SystemColors.ControlDark;
        private Color _labelColor = SystemColors.ControlText;
        private Color _focusRingColor = SystemColors.Highlight;

        private ToggleSwitchState _state = ToggleSwitchState.Off;
        private bool _isHovered;
        private bool _isPressed;

        public ToggleSwitch()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            // A UserControl inherits none of Button's free focus/keyboard
            // affordances -- every one of them is opted into explicitly
            // here. This is exactly what this control is replacing
            // (the old plain-button toggle got them for free).
            TabStop = true;

            // D-04: the entire row is the click target, not just the small
            // track, which is why this control spans the full content width
            // rather than only the pill.
            Cursor = Cursors.Hand;

            // MainForm.LayoutDashboard() sizes this row explicitly from a
            // font-derived Scaled() factor -- letting the control also
            // auto-scale itself would apply the DPI factor twice.
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
        }

        public ToggleSwitchState State => _state;

        public event EventHandler? ActionRequested;

        // Theme color properties. ThemeApplier.ThemeToggleSwitch is the only
        // intended writer of these -- this control never reads
        // IThemeProvider, keeping theming in the one hand-maintained
        // pipeline (Pitfall 1).
        //
        // DesignerSerializationVisibility.Hidden: this control is never
        // dropped on a WinForms designer surface (code-only), so these
        // values are always set programmatically at runtime and must never
        // be designer-codegenned/serialized (WFO1000).
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color OnColor
        {
            get => _onColor;
            set { _onColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color OffOutlineColor
        {
            get => _offOutlineColor;
            set { _offOutlineColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color OffHoverFillColor
        {
            get => _offHoverFillColor;
            set { _offHoverFillColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color OffPressFillColor
        {
            get => _offPressFillColor;
            set { _offPressFillColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color IndeterminateColor
        {
            get => _indeterminateColor;
            set { _indeterminateColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color ThumbColor
        {
            get => _thumbColor;
            set { _thumbColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color ThumbOutlineColor
        {
            get => _thumbOutlineColor;
            set { _thumbOutlineColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color LabelColor
        {
            get => _labelColor;
            set { _labelColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FocusRingColor
        {
            get => _focusRingColor;
            set { _focusRingColor = value; Invalidate(); }
        }

        /// <summary>
        /// The ONLY way state is ever pushed in, mirroring
        /// MonitorTile.SetState. The visual is shape/position-based, so the
        /// accessible name is the screen-reader equivalent and must stay in
        /// lockstep with it (20-UI-SPEC.md Copywriting Contract).
        /// </summary>
        public void SetState(ToggleSwitchState state)
        {
            _state = state;

            AccessibleName = state switch
            {
                ToggleSwitchState.On => "Rig Mode — On",
                ToggleSwitchState.Off => "Rig Mode — Off",
                _ => "Rig Mode — Unknown",
            };
            AccessibleRole = AccessibleRole.CheckButton;

            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            // A mouse click must move keyboard focus to the switch so the
            // focus ring tracks the last-acted control.
            Focus();

            ActionRequested?.Invoke(this, EventArgs.Empty);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Space/Enter must be indistinguishable from a click. Returning
            // true stops the key reaching the form's default button.
            if (Focused && (keyData == Keys.Space || keyData == Keys.Return))
            {
                ActionRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            Invalidate();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            // A drag-off must not leave the control stuck in the pressed
            // fill (matches BtnIdentify_MouseLeave's behavior).
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _isPressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // An escaping paint exception is process-fatal -- this
            // codebase's convention is swallow-with-trace for cosmetic
            // paths.
            try
            {
                if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float w = ClientSize.Width;
                float h = ClientSize.Height;

                float trackH = h * TrackHeightFraction;
                float trackW = trackH * TrackAspectRatio;

                // Right-aligned -- MainForm supplies the outer margin, so the
                // track sits flush with the row's right edge.
                float trackX = w - trackW;
                float trackY = (h - trackH) / 2f;

                // A radius of half the height makes the rounded rect a true
                // pill/stadium shape (D-02).
                float radius = trackH / 2f;
                float inset = trackH * ThumbInsetFraction;
                float thumbRadius = trackH / 2f - inset;

                // Label drawn first, it never overlaps the track. The
                // static Rig Mode label text (D-05) is NEVER re-rendered
                // per state.
                var labelRect = new RectangleF(0f, 0f, trackX - h * LabelGapFraction, h);
                TextRenderer.DrawText(
                    g,
                    "Rig Mode",
                    Font,
                    Rectangle.Round(labelRect),
                    LabelColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                using var trackPath = BuildRoundedRect(new RectangleF(trackX, trackY, trackW, trackH), radius);

                if (_state == ToggleSwitchState.Off)
                {
                    // At rest, do not fill at all so the control's
                    // BackColor shows through (D-10, the hollow-outline
                    // "off" language the tiles already use). Pressed takes
                    // precedence over hovered, matching
                    // MainForm.ManualButtonFill's ordering.
                    if (_isPressed)
                    {
                        using var pressBrush = new SolidBrush(OffPressFillColor);
                        g.FillPath(pressBrush, trackPath);
                    }
                    else if (_isHovered)
                    {
                        using var hoverBrush = new SolidBrush(OffHoverFillColor);
                        g.FillPath(hoverBrush, trackPath);
                    }

                    using var outlinePen = new Pen(OffOutlineColor, Math.Max(1f, h * OutlineWidthFraction));
                    g.DrawPath(outlinePen, trackPath);
                }
                else
                {
                    // On/Indeterminate: solid fill, no outline. ControlPaint
                    // computes correctly whether OnColor is the fixed
                    // dark-mode literal or the user's live
                    // SystemColors.Highlight, and will keep working
                    // unmodified once Phase 21 (THEME-07) replaces this with
                    // a truly live-read accent color (D-09).
                    Color baseFill = _state == ToggleSwitchState.On ? OnColor : IndeterminateColor;
                    Color fill = _isPressed
                        ? ControlPaint.Dark(baseFill, HoverPressShiftFactor)
                        : _isHovered
                            ? ControlPaint.Light(baseFill, HoverPressShiftFactor)
                            : baseFill;

                    using var fillBrush = new SolidBrush(fill);
                    g.FillPath(fillBrush, trackPath);
                }

                // Thumb drawn AFTER the track, as its own separate calls
                // (hard constraint 4 / Pitfall 2) -- never added to
                // trackPath. The Indeterminate position sits exactly
                // mid-track, equidistant from both real positions, so it
                // reads as neither (D-07); combined with the filled-but-gray
                // track it is a third unambiguous visual bucket, not "Off in
                // a different color".
                float thumbCenterX = _state switch
                {
                    ToggleSwitchState.Off => trackX + inset + thumbRadius,
                    ToggleSwitchState.On => trackX + trackW - inset - thumbRadius,
                    _ => trackX + trackW / 2f,
                };
                float thumbCenterY = trackY + trackH / 2f;

                var thumbRect = new RectangleF(
                    thumbCenterX - thumbRadius,
                    thumbCenterY - thumbRadius,
                    thumbRadius * 2f,
                    thumbRadius * 2f);

                using (var thumbBrush = new SolidBrush(ThumbColor))
                {
                    g.FillEllipse(thumbBrush, thumbRect);
                }

                using (var thumbPen = new Pen(ThumbOutlineColor, Math.Max(1f, h * ThumbOutlineWidthFraction)))
                {
                    g.DrawEllipse(thumbPen, thumbRect);
                }

                // Focus ring hugs the pill (rounded, radius-matched) rather
                // than being a plain rectangle, which is why
                // MainForm.DrawButtonFocusRing is deliberately NOT reused
                // here -- it draws a rectangle, the wrong shape for a pill.
                // Drawn around the track only, and OUTSIDE the track so the
                // On state's accent fill cannot swallow an accent-colored
                // ring.
                if (Focused)
                {
                    float penWidth = Math.Max(1f, h * FocusRingWidthFraction);
                    var ringRect = new RectangleF(
                        trackX - penWidth / 2f,
                        trackY - penWidth / 2f,
                        trackW + penWidth,
                        trackH + penWidth);
                    using var ringPath = BuildRoundedRect(ringRect, radius + penWidth / 2f);
                    using var ringPen = new Pen(FocusRingColor, penWidth);
                    g.DrawPath(ringPen, ringPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"ToggleSwitch.OnPaint failed: {ex}");
            }
        }

        // Local rounded-rect path builder -- sibling copy of
        // MonitorTile.cs's BuildRoundedRect (same four-arc walk, same
        // diameter <= 0f fallback to AddRectangle). There is no shared
        // generic-rounded-rect helper class in this codebase and this plan
        // does not create one, matching the existing same-file-duplication
        // convention for small geometry helpers.
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
    }
}
