using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The geometry of <see cref="CustomerGalaxy"/> as pure math (Arc Customer galaxy, design reference; every number is a
/// measurement of the Arc preview, see <c>design/components/blocks/showpieces/customer-galaxy.md</c> 4.2–4.7).
/// World units are px of a 663×360 stage at fit (zoom 1). Angles are radians, screen y points down, so a growing angle
/// turns clockwise on screen.
/// </summary>
internal static class GalaxyLayout
{
    /// <summary>The overview disc is squashed to 0.677 of its height (a disc tilted about 47° seen head on).</summary>
    public const double Squash = 0.677;

    /// <summary>Rigid rotation of the galaxy and of each cluster: 0.028 rad/s (1.6°/s, one turn in 224 s).</summary>
    public const double RotationSpeed = 0.028;

    /// <summary>Dot radius: <c>max(0.74, 0.30 + 0.028·√MRR)</c> world px (area roughly proportional to MRR).</summary>
    public const double MinDotRadius = 0.74;

    public const double DotRadiusBase = 0.30;

    public const double DotRadiusPerRootMrr = 0.028;

    /// <summary>The core's radius in the reference galaxy (about 500 of 2,400 accounts); the arms scale with it.</summary>
    public const double ReferenceCoreRadius = 95;

    /// <summary>Arms run from the core's edge 140 units outward (95 → 235 in the reference galaxy).</summary>
    public const double ArmLength = 140;

    /// <summary>
    /// Arms turn back 1.25° per unit of radius (trailing the clockwise rotation; about 190° from the core to the tip, as
    /// the deprojected Arc preview shows; the spec's straight-line fit of 0.85° covers only the arm's middle).
    /// </summary>
    public static readonly double ArmTwist = 1.25 * Math.PI / 180;

    /// <summary>Front dots (lower on screen) are drawn larger: <c>1 + toward / 1500</c> (about ±12% across the disc).</summary>
    public const double DepthDivisor = 1500;

    /// <summary>Dots cover 58% of a packed disc (the core and every cluster).</summary>
    public const double PackDensity = 0.58;

    /// <summary>The overview's core packs at 54% (jittered, and drawn up to 6% larger at the front).</summary>
    public const double CoreDensity = 0.54;

    /// <summary>Small dots still take this much room in a packed disc, so clusters of tiny accounts stay airy.</summary>
    public const double MinCellRadius = 1.1;

    /// <summary>Share of the accounts in the core (the largest MRR).</summary>
    public const double CoreShare = 0.21;

    /// <summary>Share of the other accounts scattered as a halo between the arms.</summary>
    public const double HaloShare = 0.05;

    /// <summary>The overview's framing box at fit (reference galaxy) and its insets: the galaxy sits 7 px above center.</summary>
    public const double AllFrameWidth = 391;

    public const double AllFrameHeight = 312.5;

    public static readonly Thickness AllFrameInsets = new(27.5, 16.75, 27.5, 30.75);

    /// <summary>Clusters at fit keep 20 px above, 56 px below (the hint and the zoom bar) and 24 px at the sides.</summary>
    public static readonly Thickness GroupFrameInsets = new(24, 20, 24, 56);

    /// <summary>A cluster label sits 10 px above its disc; two lines are 37.4 px high.</summary>
    public const double LabelGap = 10;

    public const double LabelHeight = 37.4;

    /// <summary>Space kept between neighbouring discs and between neighbouring labels, in screen px at fit.</summary>
    public const double ClusterGap = 24;

    /// <summary>A label may tuck closer to a neighbouring disc (it sits under the disc's curve).</summary>
    public const double LabelDiscGap = 8;

    /// <summary>
    /// Label sizes are turned into world units at this scale when clusters are arranged (a typical fit; arranged once, a
    /// resize does not move the clusters).
    /// </summary>
    public const double ArrangeScale = 0.85;

    /// <summary>The stage the world units are defined on (663 × 360).</summary>
    public static readonly Size ReferenceStage = new(663, 360);

    /// <summary>A flight frames the cluster, its gap and its label in 75% of the stage height.</summary>
    public const double FlightFill = 0.75;

    /// <summary>The narrow layout's stage height: 380 under 620 px, otherwise <c>max(360, width × 0.52)</c>.</summary>
    public static double StageHeight(double width) => width < 620 ? 380 : Math.Max(360, width * 0.52);

    /// <summary>
    /// The stage height beside the totals panel: 16:9 of the stage's width up to 560, but never shorter than the panel
    /// (Arc: 1022 wide → 560, 700 wide → 514 = the panel) or <paramref name="minimum"/>.
    /// </summary>
    public static double SideStageHeight(double stageWidth, double panelHeight, double minimum = 360) =>
        Math.Max(Math.Max(minimum, panelHeight), Math.Min(560, stageWidth * 9 / 16));

    /// <summary>The world radius of a dot for <paramref name="mrr"/>.</summary>
    public static double DotRadius(double mrr) =>
        Math.Max(MinDotRadius, DotRadiusBase + DotRadiusPerRootMrr * Math.Sqrt(Math.Max(0, mrr)));

    /// <summary>How much larger a dot is drawn at <paramref name="toward"/> units toward the viewer (disc coordinates).</summary>
    public static double DepthScale(double toward) => 1 + toward / DepthDivisor;

    /// <summary>
    /// The overview's fit scale for a stage: <c>min((W − 55) / 391, (H − 47.5) / 312.5)</c> for the reference galaxy
    /// (1.00 at 663×360, 1.49 at 700×514, 1.64 at 1022×560, 0.77 at 356×380), scaled by the actual core radius.
    /// </summary>
    public static double AllFit(Size stage, double coreRadius)
    {
        var size = Math.Max(1e-3, coreRadius / ReferenceCoreRadius);
        var i = AllFrameInsets;
        var sx = (stage.Width - i.Left - i.Right) / (AllFrameWidth * size);
        var sy = (stage.Height - i.Top - i.Bottom) / (AllFrameHeight * size);
        return Math.Max(0.05, Math.Min(sx, sy));
    }

    /// <summary>
    /// The overview's camera content (world): the framing box, its bottom extended so that its center sits where the
    /// galaxy's center belongs (7 px above the stage center at fit).
    /// </summary>
    public static Rect AllContent(double coreRadius, double fit)
    {
        var size = coreRadius / ReferenceCoreRadius;
        var w = AllFrameWidth * size;
        var h = AllFrameHeight * size;
        var i = AllFrameInsets;
        var extra = (i.Bottom - i.Top) / Math.Max(1e-6, fit);
        return new Rect(-w / 2, -h / 2, w, h + extra);
    }

    // Spiral -----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The overview: lays the accounts (by rank, largest MRR first, with their world radii) on a two-armed spiral disc.
    /// The largest <see cref="CoreShare"/> pack a solid core around the center (largest in the middle); the rest follow
    /// two trailing arms from the core's edge outward (bigger ones nearer the core), a few scatter as a halo. Writes disc
    /// coordinates (radius, angle) and slow drift parameters; returns the core radius.
    /// </summary>
    public static double Spiral(ReadOnlySpan<double> radii, ShowpieceSeed seed, Span<double> rho, Span<double> theta,
                                Span<double> driftRadius, Span<double> driftAngle, Span<double> driftRate, Span<double> driftPhase)
    {
        var n = radii.Length;
        if (n == 0)
        {
            return ReferenceCoreRadius;
        }
        var coreCount = n <= 24 ? n : Math.Max(24, (int)Math.Round(n * CoreShare));
        coreCount = Math.Min(n, coreCount);
        var golden = Math.PI * (3 - Math.Sqrt(5));
        var jitterAngle = 5 * Math.PI / 180;
        double area = 0;
        for (var i = 0; i < coreCount; i++)
        {
            var cell = Math.Max(radii[i], MinCellRadius);
            var a = cell * cell;
            // The core packs a little looser than a cluster so its jitter and depth swell rarely make dots overlap.
            var r = i == 0 ? 0 : Math.Sqrt((area + a * 0.5) / CoreDensity);
            area += a;
            r = Math.Max(0, r + seed.NextRange(-0.8, 0.8));
            rho[i] = r;
            theta[i] = i * golden + seed.NextRange(-jitterAngle, jitterAngle);
        }
        var core = Math.Max(4, Math.Sqrt(area / CoreDensity));
        var size = core / ReferenceCoreRadius;
        var rest = n - coreCount;
        for (var j = 0; j < rest; j++)
        {
            var i = coreCount + j;
            var q = rest > 1 ? j / (double)(rest - 1) : 0;
            if (seed.NextDouble() < HaloShare)
            {
                rho[i] = (120 + 140 * seed.NextDouble()) * size;
                theta[i] = seed.NextDouble() * 2 * Math.PI;
                continue;
            }
            var arm = seed.NextDouble() < 0.5 ? 0 : 1;
            // Dense next to the core and thinning outward; bigger accounts sit nearer the core.
            var u = Math.Pow(Math.Clamp(0.5 * q + 0.5 * seed.NextDouble(), 0, 1), 1.9);
            var along = ArmLength * u;
            var r0 = core + along * size;
            var t0 = arm * Math.PI - ArmTwist * along;
            // Scatter across the arm (perpendicular to the curve): a band 15–25 units wide, looser toward its end.
            var spread = (4 + 6 * u) * size * seed.NextGaussian();
            var c = Math.Cos(t0);
            var s = Math.Sin(t0);
            var k = ArmTwist / size;
            var tx = c + r0 * k * s;
            var ty = s - r0 * k * c;
            var tl = Math.Sqrt(tx * tx + ty * ty);
            var px = r0 * c + spread * -ty / tl;
            var py = r0 * s + spread * tx / tl;
            rho[i] = Math.Sqrt(px * px + py * py);
            theta[i] = Math.Atan2(py, px);
        }
        var outer = 255 * size;
        for (var i = 0; i < n; i++)
        {
            // Radial wander ±(3–15) units (wider outside), a few degrees of angle, periods of 2–4 minutes.
            var reach = Math.Clamp(rho[i] / outer, 0, 1.2);
            driftRadius[i] = (3 + 12 * reach) * (0.6 + 0.4 * seed.NextDouble()) * size;
            driftAngle[i] = (1 + 2 * seed.NextDouble()) * Math.PI / 180;
            driftRate[i] = 2 * Math.PI / (120 + 120 * seed.NextDouble());
            driftPhase[i] = seed.NextDouble() * 2 * Math.PI;
        }
        return core;
    }

    // Clusters ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Packs one cluster: its members (largest MRR first, with their radii) on a sunflower spiral whose rings follow the
    /// running area of the cells, so big dots sit in the middle without overlapping and small ones fill the rim. Writes
    /// offsets from the cluster center and returns the cluster's outer radius (edge of its outermost dot).
    /// </summary>
    public static double Pack(ReadOnlySpan<double> radii, ShowpieceSeed seed, Span<double> x, Span<double> y)
    {
        var golden = Math.PI * (3 - Math.Sqrt(5));
        var jitter = 6 * Math.PI / 180;
        double area = 0;
        double outer = 0;
        for (var i = 0; i < radii.Length; i++)
        {
            var cell = Math.Max(radii[i], MinCellRadius);
            var a = cell * cell;
            var r = i == 0 ? 0 : Math.Sqrt((area + a * 0.5) / PackDensity);
            area += a;
            // A little jitter keeps the sunflower's spiral lines from showing among the small dots.
            r = Math.Max(0, r + seed.NextRange(-0.45, 0.45) * cell);
            var angle = i * golden + seed.NextRange(-jitter, jitter);
            x[i] = r * Math.Cos(angle);
            y[i] = r * Math.Sin(angle);
            outer = Math.Max(outer, r + radii[i]);
        }
        return Math.Max(outer, radii.Length > 0 ? radii[0] : 1);
    }

    /// <summary>Directions (degrees, screen y down) the 2nd, 3rd … clusters take from the largest: left, right, lower left, upper right, upper left …</summary>
    private static readonly double[] s_directions = [180, 0, 128, -57, -120, 52, 90, -90, 160, 20, -160, -20];

    /// <summary>
    /// Arranges clusters (largest MRR first): the first at the origin, every other one along its direction at the
    /// smallest distance where its footprint (disc and the label above it, <paramref name="labels"/> in world units)
    /// keeps <paramref name="gap"/> from every disc and label placed before (<paramref name="labelDiscGap"/> between a label
    /// and a disc).
    /// </summary>
    public static void Arrange(ReadOnlySpan<double> radii, ReadOnlySpan<Size> labels, double labelGap, double gap, double labelDiscGap,
                               Span<Point> centers)
    {
        if (radii.Length == 0)
        {
            return;
        }
        centers[0] = default;
        var golden = 180 * (3 - Math.Sqrt(5));
        for (var k = 1; k < radii.Length; k++)
        {
            var degrees = k - 1 < s_directions.Length ? s_directions[k - 1] : s_directions[^1] + golden * (k - s_directions.Length);
            var angle = degrees * Math.PI / 180;
            var dx = Math.Cos(angle);
            var dy = Math.Sin(angle);
            var t = Math.Max(0, radii[0] * 0.5);
            var step = Math.Max(0.5, radii[k] * 0.05);
            for (var guard = 0; guard < 4000; guard++)
            {
                var candidate = new Point(dx * t, dy * t);
                if (Clear(candidate, k, radii, labels, labelGap, gap, labelDiscGap, centers))
                {
                    centers[k] = candidate;
                    break;
                }
                t += step;
                centers[k] = candidate;
            }
        }
    }

    private static bool Clear(Point c, int k, ReadOnlySpan<double> radii, ReadOnlySpan<Size> labels, double labelGap, double gap,
                              double labelDiscGap, ReadOnlySpan<Point> centers)
    {
        var rect = LabelRect(c, radii[k], labels[k], labelGap);
        for (var j = 0; j < k; j++)
        {
            var other = centers[j];
            var otherRect = LabelRect(other, radii[j], labels[j], labelGap);
            var d = Distance(c, other);
            if (d < radii[k] + radii[j] + gap)
            {
                return false;
            }
            if (RectDistance(rect, otherRect) < gap)
            {
                return false;
            }
            if (CircleRectDistance(c, radii[k], otherRect) < labelDiscGap || CircleRectDistance(other, radii[j], rect) < labelDiscGap)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The label box of a cluster at <paramref name="center"/>: centered, its bottom <paramref name="labelGap"/> above the disc.</summary>
    public static Rect LabelRect(Point center, double radius, Size label, double labelGap) =>
        new(center.X - label.Width / 2, center.Y - radius - labelGap - label.Height, label.Width, label.Height);

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double RectDistance(Rect a, Rect b)
    {
        var dx = Math.Max(0, Math.Max(a.Left - b.Right, b.Left - a.Right));
        var dy = Math.Max(0, Math.Max(a.Top - b.Bottom, b.Top - a.Bottom));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double CircleRectDistance(Point c, double r, Rect rect)
    {
        var dx = Math.Max(0, Math.Max(rect.Left - c.X, c.X - rect.Right));
        var dy = Math.Max(0, Math.Max(rect.Top - c.Y, c.Y - rect.Bottom));
        return Math.Sqrt(dx * dx + dy * dy) - r;
    }

    /// <summary>
    /// The fit scale of clusters for a stage: the largest scale at which every disc (scaled) and label (fixed px, above its
    /// disc) fits inside the stage minus <see cref="GroupFrameInsets"/>; also returns the camera content (world) whose
    /// center is the view's center at fit.
    /// </summary>
    public static double GroupFit(ReadOnlySpan<Point> centers, ReadOnlySpan<double> radii, ReadOnlySpan<Size> labelsPx, Size stage,
                                  out Rect content)
    {
        var i = GroupFrameInsets;
        var room = new Size(Math.Max(1, stage.Width - i.Left - i.Right), Math.Max(1, stage.Height - i.Top - i.Bottom));
        double lo = 0.02, hi = 50;
        for (var iteration = 0; iteration < 48; iteration++)
        {
            var mid = Math.Sqrt(lo * hi);
            var box = Bounds(centers, radii, labelsPx, mid);
            if (box.Width * mid <= room.Width && box.Height * mid <= room.Height)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }
        var fit = lo;
        var bounds = Bounds(centers, radii, labelsPx, fit);
        // The view's center at fit is the middle of the safe area: extend the bottom so the content's center lands there.
        var extra = (i.Bottom - i.Top) / fit;
        content = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height + extra);
        return fit;
    }

    /// <summary>The world box of the discs and labels (labels in px, turned into world units at <paramref name="scale"/>).</summary>
    public static Rect Bounds(ReadOnlySpan<Point> centers, ReadOnlySpan<double> radii, ReadOnlySpan<Size> labelsPx, double scale)
    {
        if (centers.Length == 0)
        {
            return new Rect(-1, -1, 2, 2);
        }
        double left = double.PositiveInfinity, top = double.PositiveInfinity;
        double right = double.NegativeInfinity, bottom = double.NegativeInfinity;
        for (var k = 0; k < centers.Length; k++)
        {
            var c = centers[k];
            var r = radii[k];
            var label = new Size(labelsPx[k].Width / scale, labelsPx[k].Height / scale);
            var rect = LabelRect(c, r, label, LabelGap / scale);
            left = Math.Min(left, Math.Min(c.X - r, rect.Left));
            right = Math.Max(right, Math.Max(c.X + r, rect.Right));
            top = Math.Min(top, rect.Top);
            bottom = Math.Max(bottom, c.Y + r);
        }
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// The camera for a flight into a cluster: the disc, its 10 px gap and its 37.4 px label fill 75% of the stage
    /// height (no more than 85% of its width), centered; returns the world point at the stage center and the scale.
    /// </summary>
    public static (Point Center, double Scale) ClusterFlight(Point center, double radius, Size stage)
    {
        var label = LabelGap + LabelHeight;
        var byHeight = (FlightFill * stage.Height - label) / (2 * Math.Max(0.5, radius));
        var byWidth = 0.85 * stage.Width / (2 * Math.Max(0.5, radius));
        var scale = Math.Max(0.01, Math.Min(byHeight, byWidth));
        // The pair (label above, disc below) is centered: the disc's center sits half a label below the stage center.
        return (new Point(center.X, center.Y - label / 2 / scale), scale);
    }

    /// <summary>
    /// The 7 dimming levels (1/7 … 7/7): a smoothed factor is drawn at the nearest level so dots batch by
    /// (color, level).
    /// </summary>
    public const int DimLevels = 7;

    /// <summary>Dimmed clusters fade to 1/7 of their opacity (0.72 → 0.10, 0.95 → 0.14).</summary>
    public const double DimFactor = 1.0 / DimLevels;

    /// <summary>The level (0 = most dimmed … 6 = full) of a dimming factor.</summary>
    public static int DimLevel(double factor) => Math.Clamp((int)Math.Round(factor * DimLevels) - 1, 0, DimLevels - 1);
}
