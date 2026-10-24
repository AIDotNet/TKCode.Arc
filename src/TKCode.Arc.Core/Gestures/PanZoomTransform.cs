using Avalonia;

namespace TKCode.Arc.Gestures;

/// <summary>
/// A 2D pan-and-zoom view (S10): <c>screen = (world − Origin) · Zoom</c>, where <see cref="Origin"/> is the world point
/// at the viewport's top-left corner. Immutable and pure, so views can be compared, stored for undo and unit-tested;
/// controls apply <see cref="Matrix"/> as the render transform of their world layer, which moves the content without a
/// layout pass.
/// </summary>
/// <param name="Origin">The world point shown at the viewport's top-left corner.</param>
/// <param name="Zoom">Screen pixels per world unit; above zero.</param>
public readonly record struct PanZoomTransform(Point Origin, double Zoom)
{
    /// <summary>World and screen coincide.</summary>
    public static PanZoomTransform Identity => new(default, 1);

    /// <summary>The world → screen matrix (translate by −<see cref="Origin"/>, then scale by <see cref="Zoom"/>).</summary>
    public Matrix Matrix => Matrix.CreateTranslation(-Origin.X, -Origin.Y) * Matrix.CreateScale(Zoom, Zoom);

    /// <summary>Where a world point appears on screen.</summary>
    public Point WorldToScreen(Point world) => new((world.X - Origin.X) * Zoom, (world.Y - Origin.Y) * Zoom);

    /// <summary>The world point under a screen point.</summary>
    public Point ScreenToWorld(Point screen) => new(screen.X / Zoom + Origin.X, screen.Y / Zoom + Origin.Y);

    /// <summary>Where a world rectangle appears on screen.</summary>
    public Rect WorldToScreen(Rect world) => new(WorldToScreen(world.TopLeft), world.Size * Zoom);

    /// <summary>The world rectangle under a screen rectangle.</summary>
    public Rect ScreenToWorld(Rect screen) => new(ScreenToWorld(screen.TopLeft), screen.Size / Zoom);

    /// <summary>The world rectangle a viewport of <paramref name="viewport"/> shows.</summary>
    public Rect VisibleWorld(Size viewport) => new(Origin, viewport / Zoom);

    /// <summary>Moves the content by <paramref name="screenDelta"/> pixels (a drag or a wheel pan).</summary>
    public PanZoomTransform PanBy(Vector screenDelta) => this with { Origin = Origin - screenDelta / Zoom };

    /// <summary>
    /// Sets the zoom to <paramref name="zoom"/> clamped to [<paramref name="minZoom"/>, <paramref name="maxZoom"/>],
    /// keeping the world point under <paramref name="screenAnchor"/> where it is.
    /// </summary>
    public PanZoomTransform ZoomTo(double zoom, Point screenAnchor, double minZoom = double.Epsilon,
                                   double maxZoom = double.PositiveInfinity)
    {
        var next = Clamp(zoom, minZoom, maxZoom);
        var anchor = ScreenToWorld(screenAnchor);
        return new PanZoomTransform(new Point(anchor.X - screenAnchor.X / next, anchor.Y - screenAnchor.Y / next), next);
    }

    /// <summary>Multiplies the zoom by <paramref name="factor"/> about <paramref name="screenAnchor"/> (see <see cref="ZoomTo"/>).</summary>
    public PanZoomTransform ZoomBy(double factor, Point screenAnchor, double minZoom = double.Epsilon,
                                   double maxZoom = double.PositiveInfinity)
        => ZoomTo(Zoom * factor, screenAnchor, minZoom, maxZoom);

    /// <summary>
    /// The view that shows <paramref name="content"/> as large as fits inside <paramref name="viewport"/> less
    /// <paramref name="padding"/>, centered, with the zoom clamped to [<paramref name="minZoom"/>,
    /// <paramref name="maxZoom"/>]. Pass <paramref name="maxZoom"/> = 1 to shrink large content without enlarging small
    /// content. Empty content is centered at the clamped zoom 1.
    /// </summary>
    public static PanZoomTransform Fit(Rect content, Size viewport, Thickness padding, double minZoom = double.Epsilon,
                                       double maxZoom = double.PositiveInfinity)
    {
        var available = new Size(Math.Max(0, viewport.Width - padding.Left - padding.Right),
            Math.Max(0, viewport.Height - padding.Top - padding.Bottom));
        var zoom = 1.0;
        if (content.Width > 0 && content.Height > 0 && available.Width > 0 && available.Height > 0)
        {
            zoom = Math.Min(available.Width / content.Width, available.Height / content.Height);
        }
        else if (content.Width > 0 && available.Width > 0)
        {
            zoom = available.Width / content.Width;
        }
        else if (content.Height > 0 && available.Height > 0)
        {
            zoom = available.Height / content.Height;
        }
        zoom = Clamp(zoom, minZoom, maxZoom);
        // Center the content inside the padded area.
        var screenCenter = new Point(padding.Left + available.Width / 2, padding.Top + available.Height / 2);
        var worldCenter = content.Center;
        return new PanZoomTransform(new Point(worldCenter.X - screenCenter.X / zoom, worldCenter.Y - screenCenter.Y / zoom), zoom);
    }

    /// <summary>
    /// The smallest pan that brings <paramref name="world"/> inside <paramref name="viewport"/> less
    /// <paramref name="margin"/>; unchanged when it is already inside. A rectangle larger than the area aligns its
    /// top-left corner.
    /// </summary>
    public PanZoomTransform EnsureVisible(Rect world, Size viewport, Thickness margin = default)
    {
        var screen = WorldToScreen(world);
        var dx = Shift(screen.Left, screen.Right, margin.Left, viewport.Width - margin.Right);
        var dy = Shift(screen.Top, screen.Bottom, margin.Top, viewport.Height - margin.Bottom);
        return dx == 0 && dy == 0 ? this : PanBy(new Vector(dx, dy));
    }

    /// <summary>
    /// Interpolates two views for an animation at progress <paramref name="t"/> (0 → <paramref name="from"/>,
    /// 1 → <paramref name="to"/>). The zoom and the screen translation (<c>−Origin·Zoom</c>) move linearly, so a world
    /// point that has the same screen position in both views (the anchor of a zoom) stays put for the whole animation;
    /// interpolating <see cref="Origin"/> directly would make it drift.
    /// </summary>
    public static PanZoomTransform Interpolate(PanZoomTransform from, PanZoomTransform to, double t)
    {
        var zoom = from.Zoom + (to.Zoom - from.Zoom) * t;
        if (!(zoom > 0))
        {
            return t < 0.5 ? from : to;
        }
        var tx = Lerp(-from.Origin.X * from.Zoom, -to.Origin.X * to.Zoom, t);
        var ty = Lerp(-from.Origin.Y * from.Zoom, -to.Origin.Y * to.Zoom, t);
        return new PanZoomTransform(new Point(-tx / zoom, -ty / zoom), zoom);
    }

    private static double Shift(double start, double end, double min, double max)
    {
        if (end - start > max - min || start < min)
        {
            return min - start;
        }
        return end > max ? max - end : 0;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
}
