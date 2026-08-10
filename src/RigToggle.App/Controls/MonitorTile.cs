using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using RigToggle.Core.Models;

namespace RigToggle.App.Controls
{
    /// <summary>
    /// TILE-01/TILE-05: a reusable, presentational-only monitor tile
    /// (19-RESEARCH.md Pattern 1). This control NEVER calls IMonitorController,
    /// ToggleOrchestrator, ISettingsStore, MonitorConfirmDialog, or
    /// MonitorIdentifyOverlay, and it never decides whether an action means
    /// enable or disable -- it only raises ActionRequested and MainForm owns
    /// the branch and every mutation, so the DISPLAY-12 zero-survivors guard
    /// can never gain a second call site.
    ///
    /// All paint geometry is fractional, derived from ClientSize/Font
    /// (Pitfall 5) -- RigToggle.App.csproj sets no ApplicationHighDpiMode, so
    /// AutoScaleMode.Font has zero visibility into custom paint code.
    ///
    /// Code-only control (no InitializeComponent, no .Designer.cs, no .resx),
    /// following this app's existing code-only-Form convention -- this
    /// codebase's Designer files are hand-written and this control has no
    /// designer-surface use.
    /// </summary>
    public sealed class MonitorTile : UserControl
    {
        // 19-UI-SPEC.md Tile & Layout Geometry Contract, expressed as fractions
        // of the tile's own ClientSize -- never a bare pixel literal inside
        // OnPaint (Pitfall 5).
        private const float IconAreaFraction = 48f / 72f;
        private const float IconTopFraction = 4f / 88f;
        private const float LabelGapFraction = 4f / 88f;
        private const float BadgeDiameterFraction = 14f / 72f;
        private const float FocusRingRadiusFraction = 4f / 72f;
        private const float FocusRingWidthFraction = 2f / 72f;

        private Color _accentColor = SystemColors.Highlight;
        private Color _iconOffColor = SystemColors.GrayText;
        private Color _hoverBackColor = SystemColors.ControlLight;
        private Color _focusRingColor = SystemColors.Highlight;

        private bool _isActive;
        private bool _isPrimary;
        private string _friendlyName = string.Empty;
        private int _displayNumber;
        private bool _isHovered;

        public MonitorTile()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            // TILE-05 / Pitfall 4: a UserControl inherits none of Button's free
            // focus/keyboard affordances -- every one of them is opted into
            // explicitly here.
            TabStop = true;
            Cursor = Cursors.Hand;

            // The hosting form sizes tiles explicitly from a font-derived scale
            // factor (Plan 02) -- letting the control also auto-scale itself
            // would apply the DPI factor twice.
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
        }

        /// <summary>
        /// The ONLY identity ever used for this tile -- never index-based,
        /// never friendly-name matching (06-PATTERNS.md stable-identity
        /// convention, the same rule the retired grid's row Tag followed).
        /// </summary>
        public string? DevicePath { get; private set; }

        public event EventHandler? ActionRequested;

        // Theme color properties. A future ThemeApplier helper (Plan 02) is the
        // only intended writer of these -- the tile itself never reads
        // IThemeProvider, keeping theming in the one hand-maintained pipeline
        // (19-RESEARCH.md Pitfall 1). BackColor/ForeColor are inherited from
        // Control and are used for the tile chrome and the number label.
        //
        // DesignerSerializationVisibility.Hidden: this control is never dropped
        // on a WinForms designer surface (code-only, per the class doc comment
        // above), so these values are always set programmatically at runtime
        // and must never be designer-codegenned/serialized (WFO1000).
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentColor
        {
            get => _accentColor;
            set { _accentColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color IconOffColor
        {
            get => _iconOffColor;
            set { _iconOffColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color HoverBackColor
        {
            get => _hoverBackColor;
            set { _hoverBackColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FocusRingColor
        {
            get => _focusRingColor;
            set { _focusRingColor = value; Invalidate(); }
        }

        /// <summary>
        /// The only way tile state is ever set. MonitorInfo has no
        /// Number/Ordinal field (19-RESEARCH.md Pitfall 6), so the displayed
        /// number is assigned by the caller.
        /// </summary>
        public void SetState(MonitorInfo monitor, int displayNumber)
        {
            if (monitor is null) throw new ArgumentNullException(nameof(monitor));

            DevicePath = monitor.DevicePath;
            _isActive = monitor.IsActive;
            _isPrimary = monitor.IsPrimary;
            _friendlyName = monitor.FriendlyName;
            _displayNumber = displayNumber;

            // 19-UI-SPEC.md Copywriting Contract's locked accessible-name
            // format, e.g. "Monitor 1: DELL U2720Q, Primary -- On".
            string name = $"Monitor {displayNumber}: {_friendlyName}";
            if (_isPrimary)
            {
                name += ", Primary";
            }

            name += _isActive ? " — On" : " — Off";

            AccessibleName = name;
            AccessibleRole = AccessibleRole.PushButton;

            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            // A mouse click must move keyboard focus to the clicked tile so
            // the focus ring tracks the last-acted tile.
            Focus();

            ActionRequested?.Invoke(this, EventArgs.Empty);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // TILE-05: Space/Enter must be indistinguishable from a click.
            // Returning true stops the key reaching the form's default button.
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

        // Mouse-only polish per 19-UI-SPEC.md -- it must never be the sole
        // clickability affordance, because keyboard users see the focus ring
        // instead.
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // An escaping paint exception is process-fatal -- this codebase's
            // convention is swallow-with-trace for cosmetic paths.
            try
            {
                if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float w = ClientSize.Width;
                float h = ClientSize.Height;
                float cornerRadius = w * FocusRingRadiusFraction;

                if (_isHovered)
                {
                    using var hoverPath = BuildRoundedRect(new RectangleF(0f, 0f, w, h), cornerRadius);
                    using var hoverBrush = new SolidBrush(HoverBackColor);
                    g.FillPath(hoverBrush, hoverPath);
                }

                float iconSide = w * IconAreaFraction;
                float iconX = (w - iconSide) / 2f;
                float iconY = h * IconTopFraction;
                var iconRect = new RectangleF(iconX, iconY, iconSide, iconSide);

                MonitorIconGeometry.DrawTileIcon(g, iconRect, _isActive, AccentColor, IconOffColor);

                if (_isPrimary)
                {
                    // 2026-08-10 rig report: centering the badge exactly on
                    // iconRect.Top clipped its top edge, since iconY (the icon's own
                    // top inset) is smaller than the badge's radius at this tile
                    // size -- the badge's top half fell outside the control's own
                    // paint bounds (Y < 0) and never rendered. Clamping the center's
                    // Y to at least one radius down keeps the whole badge inside
                    // ClientSize regardless of tile size.
                    float badgeRadius = w * BadgeDiameterFraction / 2f;
                    var badgeCenter = new PointF(iconRect.Right, Math.Max(iconRect.Top, badgeRadius));
                    MonitorIconGeometry.DrawPrimaryBadge(g, badgeCenter, w * BadgeDiameterFraction);
                }

                float labelTop = iconRect.Bottom + h * LabelGapFraction;
                var labelRect = new RectangleF(0f, labelTop, w, h - labelTop);
                string numberText = _displayNumber.ToString(CultureInfo.InvariantCulture);
                TextRenderer.DrawText(
                    g,
                    numberText,
                    Font,
                    Rectangle.Round(labelRect),
                    ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix);

                if (Focused)
                {
                    float penWidth = Math.Max(1f, w * FocusRingWidthFraction);
                    var ringRect = new RectangleF(penWidth / 2f, penWidth / 2f, w - penWidth, h - penWidth);
                    using var ringPath = BuildRoundedRect(ringRect, cornerRadius);
                    using var ringPen = new Pen(FocusRingColor, penWidth);
                    g.DrawPath(ringPen, ringPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"MonitorTile.OnPaint failed: {ex}");
            }
        }

        // Local rounded-rect path builder for the hover background and focus
        // ring -- both fraction-derived from the caller's rect/radius, never a
        // bare pixel literal.
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
