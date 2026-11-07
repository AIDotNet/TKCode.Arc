using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The card's fake 3D (Avalonia has no CSS 3D): the plane of a face under CSS
/// <c>perspective(d) scale(s) rotateX(rx) rotateY(ry)</c> plus the inner flip <c>rotateY(θ)</c> (and the back's own
/// <c>rotateY(180°)</c>), all about the card's centre, as one projective <see cref="Matrix"/> for a
/// <see cref="Visual.RenderTransform"/>. A plane under perspective is exactly a 3×3 projective map, so content drawn
/// through it stays sharp. Pure functions without allocation.
/// </summary>
/// <remarks>
/// Shared-helper candidate: WalletStack tilts cards the same way (Blocks has <c>ShowpieceProjection</c>, which the
/// Special package cannot reference). Rotations follow CSS (x right, y down, z toward the viewer): <c>rotateY(a)</c> maps
/// a plane point (x, y, 0) to (x·cos a, y, −x·sin a); <c>rotateX(a)</c> maps (x, y, z) to (x, y·cos a − z·sin a,
/// y·sin a + z·cos a); <c>scale(s)</c> scales x and y only; <c>perspective(d)</c> about the centre c sends a point at
/// depth z to <c>c + (x, y)·d/(d − z)</c>. Avalonia matrices act on row vectors with the perspective terms in
/// <c>M13</c>/<c>M23</c>.
/// </remarks>
internal static class HoloCardProjection
{
    /// <summary>Degrees to radians.</summary>
    public static double Radians(double degrees) => degrees * Math.PI / 180;

    /// <summary>
    /// The matrix that maps a face's local coordinates (0..<paramref name="width"/>, 0..<paramref name="height"/>) to the
    /// control's coordinates. <paramref name="yaw"/> is the total turn about the vertical axis in degrees: the tilt's
    /// rotateY plus the flip angle (plus 180 for the back face), since consecutive rotateY turns add up.
    /// </summary>
    public static Matrix PlaneMatrix(double width, double height, double perspective, double scale, double rotateX, double yaw)
    {
        var cx0 = width / 2;
        var cy0 = height / 2;
        var phi = Radians(yaw);
        var alpha = Radians(rotateX);
        double cp = Math.Cos(phi), sp = Math.Sin(phi), ca = Math.Cos(alpha), sa = Math.Sin(alpha);
        // Plane point (x, y, 0) relative to the centre → rotateY → rotateX → scale (x, y):
        // x' = s·x·cos φ; y' = s·(y·cos α + x·sin φ·sin α); z' = y·sin α − x·sin φ·cos α.
        double ax = scale * cp, ay = scale * sp * sa, az = -sp * ca;
        double bx = 0, by = scale * ca, bz = sa;
        double tx = -cx0 * ax - cy0 * bx, ty = -cx0 * ay - cy0 * by, tz = -cx0 * az - cy0 * bz;
        var d = perspective;
        if (double.IsPositiveInfinity(d) || d <= 0)
        {
            return new Matrix(ax, ay, 0, bx, by, 0, tx + cx0, ty + cy0, 1);
        }
        // Homogeneous image: X = d·P.x + cx·(d − P.z), Y = d·P.y + cy·(d − P.z), W = d − P.z.
        var m33 = d - tz;
        var k = Math.Abs(m33) > 1e-9 ? 1 / m33 : 1;
        return new Matrix(
            (d * ax - cx0 * az) * k, (d * ay - cy0 * az) * k, -az * k,
            (d * bx - cx0 * bz) * k, (d * by - cy0 * bz) * k, -bz * k,
            (d * tx + cx0 * (d - tz)) * k, (d * ty + cy0 * (d - tz)) * k, 1);
    }

    /// <summary>Applies a projective matrix to a point (divides by w).</summary>
    public static Point Apply(in Matrix m, Point p)
    {
        var x = p.X * m.M11 + p.Y * m.M21 + m.M31;
        var y = p.X * m.M12 + p.Y * m.M22 + m.M32;
        var w = p.X * m.M13 + p.Y * m.M23 + m.M33;
        if (Math.Abs(w) < 1e-12)
        {
            w = 1e-12;
        }
        return new Point(x / w, y / w);
    }

    /// <summary>
    /// Whether the plane's front faces the viewer: its projected corners keep their winding (CSS
    /// <c>backface-visibility: hidden</c> hides the face otherwise).
    /// </summary>
    public static bool FacesViewer(in Matrix m, double width, double height)
    {
        var o = Apply(m, new Point(0, 0));
        var r = Apply(m, new Point(width, 0));
        var b = Apply(m, new Point(0, height));
        var cross = (r.X - o.X) * (b.Y - o.Y) - (r.Y - o.Y) * (b.X - o.X);
        return cross > 0;
    }

    /// <summary>The axis-aligned bounds of the projected plane.</summary>
    public static Rect ProjectedBounds(in Matrix m, double width, double height)
    {
        var a = Apply(m, new Point(0, 0));
        var b = Apply(m, new Point(width, 0));
        var c = Apply(m, new Point(width, height));
        var e = Apply(m, new Point(0, height));
        var minX = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, e.X));
        var maxX = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, e.X));
        var minY = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, e.Y));
        var maxY = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, e.Y));
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}

/// <summary>
/// The measured rules that tie the pointer, the springs and the flip to what the card draws (Arc Holo card, design
/// reference; notes sections 4 and 6). Pure functions, tested against the measured numbers.
/// </summary>
internal static class HoloCardMath
{
    /// <summary>Width to height: the card is always 5:7.</summary>
    public const double AspectRatio = 5.0 / 7.0;

    /// <summary>CSS <c>perspective(900px)</c>.</summary>
    public const double Perspective = 900;

    /// <summary>Hover lifts the card to 1.035.</summary>
    public const double HoverScale = 0.035;

    /// <summary>Light strength at rest; hover brings it to 1.</summary>
    public const double RestShine = 0.35;

    /// <summary>The shadow leans 0.9 px per degree of tilt, against it.</summary>
    public const double ShadowLean = 0.9;

    /// <summary>The shadow's vertical offset at rest.</summary>
    public const double ShadowRestOffsetY = 18;

    /// <summary>The shadow's blur radius (CSS box-shadow blur).</summary>
    public const double ShadowBlur = 42;

    /// <summary>The shadow's spread (negative: smaller than its box).</summary>
    public const double ShadowSpread = -14;

    /// <summary>Shadow opacity at rest; hover adds 0.12.</summary>
    public const double ShadowRestAlpha = 0.28;

    /// <summary>The shadow's box is inset 5 % of the width at the sides and 3 % of the height at the top and bottom.</summary>
    public const double ShadowInsetX = 0.05;

    /// <inheritdoc cref="ShadowInsetX"/>
    public const double ShadowInsetY = 0.03;

    /// <summary>Foil opacity at full light.</summary>
    public const double FoilOpacity = 0.24;

    /// <summary>Glitter opacity at full light.</summary>
    public const double GlitterOpacity = 0.55;

    /// <summary>The glitter drifts up to 18 px against the pointer on each axis.</summary>
    public const double GlitterTravel = 18;

    /// <summary>Hue turned per unit of horizontal light travel (±60° across the card).</summary>
    public const double HuePerX = 120;

    /// <summary>Hue turned per unit of vertical light travel (±30°).</summary>
    public const double HuePerY = 60;

    /// <summary>Extra hue at the middle of a flip (90°·|sin θ|).</summary>
    public const double FlipHueSweep = 90;

    /// <summary>Extra foil travel at the middle of a flip (60 % of the foil's range, |sin θ|).</summary>
    public const double FlipFoilSweep = 0.6;

    /// <summary>The card's size for an available width and height: as wide as allowed, 5:7, never taller than allowed.</summary>
    public static Size CardSize(double availableWidth, double availableHeight, double maxWidth)
    {
        var width = double.IsFinite(availableWidth) ? availableWidth : maxWidth;
        if (double.IsFinite(maxWidth))
        {
            width = Math.Min(width, maxWidth);
        }
        if (!double.IsFinite(width))
        {
            width = 300;
        }
        if (double.IsFinite(availableHeight) && width / AspectRatio > availableHeight)
        {
            width = availableHeight * AspectRatio;
        }
        width = Math.Max(0, width);
        return new Size(width, width / AspectRatio);
    }

    /// <summary>The pointer's position on the card's layout box, each axis clamped to 0..1.</summary>
    public static Vector Normalize(Point position, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return new Vector(0.5, 0.5);
        }
        return new Vector(Math.Clamp(position.X / size.Width, 0, 1), Math.Clamp(position.Y / size.Height, 0, 1));
    }

    /// <summary>
    /// The tilt for a normalized pointer position, in degrees: X = rotateX = (0.5 − py)·2·max, Y = rotateY =
    /// (px − 0.5)·2·max. The corner under the pointer sinks away from the viewer.
    /// </summary>
    public static Vector Tilt(Vector pointer, double maxTilt)
        => new((0.5 - pointer.Y) * 2 * maxTilt, (pointer.X - 0.5) * 2 * maxTilt);

    /// <summary>The card's scale for a hover progress (0 at rest, 1 hovered).</summary>
    public static double Scale(double hover) => 1 + HoverScale * hover;

    /// <summary>
    /// The light strength: 0.35 at rest, 1 hovered, and raised toward 1 in the middle of a flip (|sin θ|), whichever is
    /// stronger.
    /// </summary>
    public static double Shine(double hover, double flipDegrees)
        => Math.Max(RestShine + (1 - RestShine) * hover, Math.Abs(Math.Sin(HoloCardProjection.Radians(flipDegrees))));

    /// <summary>The shadow's offset for a tilt (degrees): (−0.9·rotateY, 18 + 0.9·rotateX).</summary>
    public static Vector ShadowOffset(Vector tilt) => new(-ShadowLean * tilt.Y, ShadowRestOffsetY + ShadowLean * tilt.X);

    /// <summary>The shadow's opacity for a hover progress.</summary>
    public static double ShadowAlpha(double hover) => ShadowRestAlpha + 0.12 * hover;

    /// <summary>The foil's hue turn in degrees: 120·(lx − 0.5) + 60·(ly − 0.5) + 90·|sin θ|.</summary>
    public static double Hue(Vector light, double flipDegrees)
        => HuePerX * (light.X - 0.5) + HuePerY * (light.Y - 0.5)
           + FlipHueSweep * Math.Abs(Math.Sin(HoloCardProjection.Radians(flipDegrees)));

    /// <summary>The foil's background position (fractions of its travel): the light, pushed 60 % right mid-flip.</summary>
    public static Vector FoilPosition(Vector light, double flipDegrees)
        => new(light.X + FlipFoilSweep * Math.Abs(Math.Sin(HoloCardProjection.Radians(flipDegrees))), light.Y);

    /// <summary>The glitter's drift in px: up to 18 px against the light on each axis.</summary>
    public static Vector GlitterDrift(Vector light)
        => new(-(light.X - 0.5) * 2 * GlitterTravel, -(light.Y - 0.5) * 2 * GlitterTravel);

    /// <summary>Whether a flip angle (degrees, accumulated without wrapping) shows the back: an odd number of half turns.</summary>
    public static bool IsFlippedAt(double flipDegrees)
    {
        var halfTurns = (long)Math.Round(flipDegrees / 180);
        return (halfTurns & 1) != 0;
    }

    /// <summary>The next flip target: half a turn further to the right, or back to the left for a left-half click.</summary>
    public static double NextFlipTarget(double target, HoloCardFlipSide side)
        => side == HoloCardFlipSide.Left ? target - 180 : target + 180;
}
