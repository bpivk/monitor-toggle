using System.Drawing;
using System.Drawing.Drawing2D;

namespace RigToggle.App;

/// <summary>
/// TILE-01/D-01: procedural GDI+ geometry for the monitor-tile dashboard's icon glyphs
/// (monitor silhouette ON/OFF state, primary-monitor badge, and the Settings gear).
///
/// This is a DELIBERATE COPY of the monitor-silhouette fractional constants and
/// <c>BuildMonitorPath</c>/<c>AddRoundedRect</c> geometry in
/// <c>src/RigToggle.IconGen/IconGeometry.cs</c>, duplicated (not shared via a project
/// reference) because <c>RigToggle.IconGen</c> is a dev-time-only console tool
/// explicitly documented as never referenced by <c>RigToggle.App</c> and never part of
/// the shipped self-contained publish (19-RESEARCH.md Pitfall 7). The two copies have
/// NO shared source of truth and MUST be edited together — if you change the monitor
/// silhouette's proportions here, also update
/// <c>src/RigToggle.IconGen/IconGeometry.cs</c>'s <c>BuildMonitorPath</c>/constants,
/// and vice versa.
/// </summary>
internal static class MonitorIconGeometry
{
    // Ported verbatim from RigToggle.IconGen/IconGeometry.cs lines 21-36 (monitor
    // silhouette: screen/neck/base, fractions of the icon's own W/H).
    private const float ScreenX = 0.125f;
    private const float ScreenY = 0.125f;
    private const float ScreenW = 0.75f;
    private const float ScreenH = 0.5f;
    private const float ScreenRadius = 0.06f;

    private const float NeckX = 0.4375f;
    private const float NeckY = 0.625f;
    private const float NeckW = 0.125f;
    private const float NeckH = 0.125f;

    private const float BaseX = 0.28f;
    private const float BaseY = 0.75f;
    private const float BaseW = 0.44f;
    private const float BaseH = 0.125f;
    private const float BaseRadius = 0.03f;

    // D-03 / 19-UI-SPEC.md Color table: a single, theme-independent, locked literal
    // for the primary-monitor badge — deliberately takes no isDark parameter, same
    // treatment MonitorPanelForm.CreateStatusDot's status literals already carry.
    private static readonly Color PrimaryBadgeColor = Color.FromArgb(245, 166, 35);

    /// <summary>
    /// Builds the monitor silhouette (screen ∪ neck ∪ base) as one combined
    /// GraphicsPath, offset by (x, y) and scaled to (w, h) so the caller can place the
    /// glyph anywhere inside a tile rather than only at the origin (unlike the
    /// IconGen port source, which only ever draws at (0, 0)).
    /// </summary>
    public static GraphicsPath BuildMonitorPath(float x, float y, float w, float h)
    {
        var path = new GraphicsPath();

        // Screen: top-left offset (ScreenX*w, ScreenY*h), size ScreenW*w x ScreenH*h,
        // corner radius ScreenRadius*w.
        AddRoundedRect(path, x + ScreenX * w, y + ScreenY * h, ScreenW * w, ScreenH * h, ScreenRadius * w);
        // Neck: sharp-cornered rectangle.
        path.AddRectangle(new RectangleF(x + NeckX * w, y + NeckY * h, NeckW * w, NeckH * h));
        // Base: corner radius BaseRadius*w.
        AddRoundedRect(path, x + BaseX * w, y + BaseY * h, BaseW * w, BaseH * h, BaseRadius * w);

        return path;
    }

    /// <summary>
    /// Adds a rounded rectangle (all four corners) to an existing GraphicsPath. Ported
    /// verbatim from RigToggle.IconGen/IconGeometry.cs's private AddRoundedRect.
    /// </summary>
    private static void AddRoundedRect(GraphicsPath path, float x, float y, float width, float height, float radius)
    {
        float diameter = radius * 2;

        if (diameter <= 0f)
        {
            path.AddRectangle(new RectangleF(x, y, width, height));
            return;
        }

        var arc = new RectangleF(x, y, diameter, diameter);

        path.StartFigure();
        // Top-left corner.
        path.AddArc(arc, 180, 90);
        // Top-right corner.
        arc.X = x + width - diameter;
        path.AddArc(arc, 270, 90);
        // Bottom-right corner.
        arc.Y = y + height - diameter;
        path.AddArc(arc, 0, 90);
        // Bottom-left corner.
        arc.X = x;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
    }

    /// <summary>
    /// D-02: draws a tile's monitor icon in either the ON (active) or OFF (inactive)
    /// state. The two states differ by BOTH shape (solid fill vs. hollow outline) AND
    /// color — two independent visual signals, never collapsed into a single color
    /// swap or a separate status dot.
    /// </summary>
    public static void DrawTileIcon(Graphics g, RectangleF bounds, bool isActive, Color activeColor, Color outlineColor)
    {
        if (bounds.Width <= 0f || bounds.Height <= 0f) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var path = BuildMonitorPath(bounds.X, bounds.Y, bounds.Width, bounds.Height);

        if (isActive)
        {
            // ON state: solid fill only, no stroke. A single combined-shape FILL
            // cannot produce the icon-generator's CR-01 seam artifact — that bug only
            // appears when a Pen strokes a multi-sub-figure combined path (DrawPath
            // strokes each sub-figure's own boundary independently rather than
            // computing a merged union contour). A fill has no such per-sub-figure
            // stroking step, so no stroke-then-fill compositing is needed here.
            using var fill = new SolidBrush(activeColor);
            g.FillPath(fill, path);
        }
        else
        {
            // OFF state: hollow outline only, no fill. The interior seams the CR-01
            // fix exists to hide are acceptable-and-intended here — D-02 wants the OFF
            // state to read as a hollow wireframe, so the sub-shape boundaries showing
            // as visible lines (screen/neck/base seams) is the desired look, not the
            // artifact stroke-then-fill was built to eliminate.
            float strokeWidth = Math.Max(1.5f, bounds.Width / 24f);
            using var outline = new Pen(outlineColor, strokeWidth);
            g.DrawPath(outline, path);
        }
    }

    /// <summary>
    /// D-03: draws the primary-monitor badge — a small filled 5-point star, centered
    /// at the given point. Deliberately a pure shape with no glyph or text, so it
    /// needs no second font size inside a badge this small (19-UI-SPEC.md
    /// Typography), and deliberately not green/red or accent-blue so it can never be
    /// confused with the ON/OFF signal above.
    /// </summary>
    public static void DrawPrimaryBadge(Graphics g, PointF center, float diameter)
    {
        if (diameter <= 0f) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var star = BuildStarPath(center, diameter / 2f);
        using var fill = new SolidBrush(PrimaryBadgeColor);
        g.FillPath(fill, star);
    }

    /// <summary>
    /// Builds a filled 5-point star GraphicsPath: 10 vertices alternating between the
    /// outer radius and half that as the inner radius, starting at a point facing
    /// straight up.
    /// </summary>
    private static GraphicsPath BuildStarPath(PointF center, float outerRadius)
    {
        float innerRadius = outerRadius * 0.5f;
        var points = new PointF[10];

        for (int i = 0; i < 10; i++)
        {
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            points[i] = new PointF(
                center.X + radius * (float)Math.Cos(angle),
                center.Y + radius * (float)Math.Sin(angle));
        }

        var path = new GraphicsPath();
        path.AddPolygon(points);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// MAIN-02/D-10: draws a procedural gear glyph for the de-emphasized icon-only
    /// Settings button — no image asset, no NuGet icon package. The caller supplies
    /// the color (Plan 02 passes the themed button ForeColor) — this helper never
    /// hardcodes a gear color.
    /// </summary>
    public static void DrawGearIcon(Graphics g, RectangleF bounds, Color color)
    {
        if (bounds.Width <= 0f || bounds.Height <= 0f) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        float side = Math.Min(bounds.Width, bounds.Height);
        float cx = bounds.X + bounds.Width / 2f;
        float cy = bounds.Y + bounds.Height / 2f;

        float ringRadius = 0.34f * side;
        float toothWidth = 0.16f * side;
        float toothHeight = 0.22f * side;
        float holeRadius = 0.14f * side;

        // FillMode.Alternate so the center hole punches out of the body rather than
        // needing to be overpainted with a background color this helper does not know.
        using var gear = new GraphicsPath(FillMode.Alternate);

        // Ring body.
        gear.AddEllipse(cx - ringRadius, cy - ringRadius, ringRadius * 2, ringRadius * 2);

        // 8 teeth, each a rectangle centred on the ring, rotated about the gear
        // center via a Matrix transform on a per-tooth path merged into the whole.
        for (int i = 0; i < 8; i++)
        {
            float angleDegrees = i * 45f;
            using var tooth = new GraphicsPath();
            tooth.AddRectangle(new RectangleF(cx - toothWidth / 2f, cy - ringRadius - toothHeight / 2f, toothWidth, toothHeight));
            using var matrix = new Matrix();
            matrix.RotateAt(angleDegrees, new PointF(cx, cy));
            tooth.Transform(matrix);
            gear.AddPath(tooth, false);
        }

        // Center hole, added last so FillMode.Alternate renders it transparent.
        gear.AddEllipse(cx - holeRadius, cy - holeRadius, holeRadius * 2, holeRadius * 2);

        using var fill = new SolidBrush(color);
        g.FillPath(fill, gear);
    }
}
