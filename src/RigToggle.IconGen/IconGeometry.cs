using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace RigToggle.IconGen;

/// <summary>
/// Procedural GDI+ silhouette drawing for the three icon assets, driven by
/// 13-UI-SPEC.md's "Icon Geometry Contract" (locked fractional coordinates and
/// hex colors -- not discretionary). Each Draw*Icon method redraws fresh at the
/// requested pixel size (UI-SPEC governing rule 2: "redraw at each target size --
/// do not scale a single bitmap") rather than rasterizing once and downscaling,
/// which would produce mushy edges at 16px specifically.
/// </summary>
internal static class IconGeometry
{
    // normal.ico / app.ico shared monitor geometry (fractions of W/H). ICON-04
    // requires app.ico to reuse the exact same monitor motif as normal.ico, so
    // both draw methods below reference these same constants rather than
    // re-deriving the shape independently.
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

    // rig.ico wheel geometry (fractions of W, centered at 0.5W,0.5H).
    private const float OuterRimRadius = 0.40f;
    private const float InnerCutoutRadius = 0.26f;
    private const float HubRadius = 0.12f;
    private const float SpokeHalfWidth = 0.07f; // spoke width 0.14W, half on each side of the spoke's centerline

    // app.ico color treatment (13-UI-SPEC.md Color table).
    private static readonly Color AppBodyColor = Color.FromArgb(45, 45, 48);   // #2D2D30
    private static readonly Color AppGlassColor = Color.FromArgb(0, 90, 158);  // #005A9E
    private const float GlassInset = 0.06f;

    /// <summary>
    /// normal.ico: monitor-on-a-stand silhouette. Screen ∪ neck ∪ base as one
    /// contiguous GraphicsPath (13-RESEARCH.md Pattern 2) so a single DrawPath
    /// call traces the outer contour only, not internal seams between the
    /// touching/overlapping sub-shapes. White fill, black outline (D-04/D-05).
    /// </summary>
    public static Bitmap DrawNormalIcon(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        using var path = BuildMonitorPath(size);

        using var fill = new SolidBrush(Color.White);
        g.FillPath(fill, path);

        using var outline = new Pen(Color.Black, OutlineWidth(size));
        g.DrawPath(outline, path);

        return bmp;
    }

    /// <summary>
    /// rig.ico: tri-spoke steering-wheel silhouette. Outer rim minus inner
    /// cutout (ring), plus hub disc and three spokes, all combined into one
    /// GraphicsPath so the union outlines as a single contiguous silhouette
    /// (13-RESEARCH.md Pattern 2). White fill, black outline (D-04/D-05).
    /// </summary>
    public static Bitmap DrawRigIcon(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        using var path = BuildWheelPath(size);

        using var fill = new SolidBrush(Color.White);
        g.FillPath(fill, path);

        using var outline = new Pen(Color.Black, OutlineWidth(size));
        g.DrawPath(outline, path);

        return bmp;
    }

    /// <summary>
    /// app.ico: reuses normal.ico's exact screen/neck/base fractions (D-06,
    /// ICON-04 motif reuse) at the color treatment defined in 13-UI-SPEC.md's
    /// Color table -- dark-gray body (#2D2D30) with a blue screen-glass inset
    /// accent (#005A9E), no outline (flat color fill only per the spec).
    /// </summary>
    public static Bitmap DrawAppIcon(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float w = size, h = size;

        // Bezel/neck/base body -- same geometry as normal.ico's monitor, filled
        // #2D2D30 instead of white, no black outline.
        using var bodyPath = BuildMonitorPath(size);
        using var bodyFill = new SolidBrush(AppBodyColor);
        g.FillPath(bodyFill, bodyPath);

        // Screen glass: inset rect inside the bezel's inner edge, filled #005A9E.
        float screenX = ScreenX * w;
        float screenY = ScreenY * h;
        float screenW = ScreenW * w;
        float screenH = ScreenH * h;
        float inset = GlassInset * w;

        var glassRect = new RectangleF(
            screenX + inset,
            screenY + inset,
            screenW - 2 * inset,
            screenH - 2 * inset);

        using var glassFill = new SolidBrush(AppGlassColor);
        g.FillRectangle(glassFill, glassRect);

        return bmp;
    }

    /// <summary>
    /// Builds the normal.ico/app.ico shared monitor silhouette (screen ∪ neck ∪
    /// base) as one contiguous GraphicsPath.
    /// </summary>
    private static GraphicsPath BuildMonitorPath(int size)
    {
        float w = size, h = size;
        var path = new GraphicsPath();

        // Screen: top-left (0.125W, 0.125H), size 0.75W x 0.5H, corner radius 0.06W.
        AddRoundedRect(path, ScreenX * w, ScreenY * h, ScreenW * w, ScreenH * h, ScreenRadius * w);
        // Neck: top-left (0.4375W, 0.625H), size 0.125W x 0.125H, sharp corners.
        path.AddRectangle(new RectangleF(NeckX * w, NeckY * h, NeckW * w, NeckH * h));
        // Base: top-left (0.28W, 0.75H), size 0.44W x 0.125H, corner radius 0.03W.
        AddRoundedRect(path, BaseX * w, BaseY * h, BaseW * w, BaseH * h, BaseRadius * w);

        return path;
    }

    /// <summary>
    /// Builds the rig.ico wheel silhouette: outer rim minus inner cutout (ring,
    /// via FillMode.Alternate with two concentric ellipses), plus a hub disc and
    /// 3 spokes radiating from the hub edge to the inner rim edge at 180/60/300
    /// degrees clockwise from 12 o'clock.
    /// </summary>
    private static GraphicsPath BuildWheelPath(int size)
    {
        float w = size, h = size;
        float cx = 0.5f * w;
        float cy = 0.5f * h;

        float outerR = OuterRimRadius * w;
        float innerR = InnerCutoutRadius * w;
        float hubR = HubRadius * w;
        float spokeHalf = SpokeHalfWidth * w;

        var path = new GraphicsPath(FillMode.Alternate);

        // Ring: outer rim ellipse + inner cutout ellipse, Alternate fill mode
        // makes the overlapping region (the cutout) a hole.
        path.AddEllipse(cx - outerR, cy - outerR, outerR * 2, outerR * 2);
        path.AddEllipse(cx - innerR, cy - innerR, innerR * 2, innerR * 2);

        // Hub disc at dead center.
        path.AddEllipse(cx - hubR, cy - hubR, hubR * 2, hubR * 2);

        // Three spokes at 180 (6 o'clock), 60 (2 o'clock), 300 (10 o'clock)
        // degrees, measured clockwise from 12 o'clock, radiating from the hub
        // edge (hubR) to the inner rim edge (innerR).
        foreach (var angleDegrees in new[] { 180.0, 60.0, 300.0 })
        {
            path.AddPath(BuildSpokePath(cx, cy, hubR, innerR, spokeHalf, angleDegrees), false);
        }

        return path;
    }

    /// <summary>
    /// Builds one spoke as a rectangle spanning from the hub edge to the inner
    /// rim edge, rotated to point at the given clockwise-from-12-o'clock angle.
    /// </summary>
    private static GraphicsPath BuildSpokePath(float cx, float cy, float innerRadius, float outerRadius, float halfWidth, double angleDegreesClockwiseFrom12)
    {
        // Convert "clockwise from 12 o'clock" to standard math radians (0 = +X
        // axis, counter-clockwise positive) for Math.Sin/Cos: angle from 12
        // o'clock clockwise == angle from +Y axis (pointing up, i.e. -Y in
        // screen space) rotating toward +X.
        double radians = (angleDegreesClockwiseFrom12 - 90.0) * Math.PI / 180.0;
        float dirX = (float)Math.Cos(radians);
        float dirY = (float)Math.Sin(radians);

        // Perpendicular direction for spoke width.
        float perpX = -dirY;
        float perpY = dirX;

        float innerX = cx + dirX * innerRadius;
        float innerY = cy + dirY * innerRadius;
        float outerX = cx + dirX * outerRadius;
        float outerY = cy + dirY * outerRadius;

        var points = new[]
        {
            new PointF(innerX + perpX * halfWidth, innerY + perpY * halfWidth),
            new PointF(outerX + perpX * halfWidth, outerY + perpY * halfWidth),
            new PointF(outerX - perpX * halfWidth, outerY - perpY * halfWidth),
            new PointF(innerX - perpX * halfWidth, innerY - perpY * halfWidth),
        };

        var path = new GraphicsPath();
        path.AddPolygon(points);
        return path;
    }

    /// <summary>
    /// Adds a rounded rectangle (all four corners) to an existing GraphicsPath.
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
    /// Outline stroke width -- a tunable starting value (13-RESEARCH.md Open
    /// Question 2 / Pitfall 2), not a locked UI-SPEC fraction. Keeps a ~1px
    /// stroke at the 16px canvas, scaling proportionally at larger sizes.
    /// </summary>
    private static float OutlineWidth(int size) => Math.Max(1f, size / 16f);
}
