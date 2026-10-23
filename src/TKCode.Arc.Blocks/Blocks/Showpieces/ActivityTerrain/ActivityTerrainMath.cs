using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// How the terrain fits a canvas: <see cref="Scale"/> is px per world unit at the camera target's depth (the focal
/// length divided by the camera distance), <see cref="Offset"/> is where the target lands relative to the canvas center.
/// </summary>
internal readonly record struct ActivityTerrainFit(double Scale, Vector Offset);

/// <summary>
/// The terrain's measured constants and pure math (Arc Activity terrain, design reference; spec sections 4, 6 and 8):
/// world layout, the fit-to-canvas rule and its pitch table, label thinning, wave delays and springs.
/// </summary>
internal static class ActivityTerrainMath
{
    public const int Days = 7;
    public const int Hours = 24;
    public const int Cells = Days * Hours;

    /// <summary>Bar footprint (units; cells are 1 apart, so 0.2 of floor shows between bars).</summary>
    public const double BarSize = 0.8;

    /// <summary>Height of the week's largest value (units).</summary>
    public const double MaxBarHeight = 5.384;

    /// <summary>How far a highlighted or focused bar rises off the floor (units).</summary>
    public const double Lift = 0.42;

    /// <summary>Height of every bar in the top view (units): a sliver, still drawn as three faces.</summary>
    public const double FlatHeight = 0.0025;

    /// <summary>Floor half extents: the grid plus 0.3 of a cell.</summary>
    public const double FloorHalfX = 12.3;

    public const double FloorHalfZ = 3.8;

    /// <summary>Where the row band starts (left of the floor) and the column band ends (in front of it).</summary>
    public const double BandOuterX = -12.55;

    public const double BandOuterZ = 4.05;

    /// <summary>Label anchors on the floor plane: day labels left of the grid, hour labels in front of it.</summary>
    public const double DayLabelX = -13.25;

    public const double HourLabelZ = 4.55;

    /// <summary>Camera distance in the perspective view and in the top view (units).</summary>
    public const double Distance = 42;

    public const double TopDistance = 160;

    /// <summary>Height of the default camera target above the grid's center (units).</summary>
    public const double TargetHeight = 0.864;

    public const double RestYawDegrees = 32;
    public const double NarrowRestYawDegrees = 50;
    public const double RestPitchDegrees = 30;
    public const double PitchMinDegrees = 10;
    public const double PitchMaxDegrees = 84;
    public const double ZoomMin = 0.7;
    public const double ZoomMax = 2.8;

    /// <summary>Zoom a click flies in to at least.</summary>
    public const double FlyZoom = 1.5;

    /// <summary>Keyboard steps: Shift + ←/→ turn 15°, Shift + ↑/↓ tilt 8°, + / − zoom by 1.25.</summary>
    public const double KeyYawStepDegrees = 15;

    public const double KeyPitchStepDegrees = 8;
    public const double KeyZoomFactor = 1.25;

    /// <summary>Ctrl + wheel zoom: exp(−webDeltaY × 0.01).</summary>
    public const double PinchSensitivity = 0.01;

    /// <summary>The intro starts far and from above: yaw 26° short of rest, pitch 76°, zoom 0.53.</summary>
    public const double IntroYawOffsetDegrees = -26;

    public const double IntroPitchDegrees = 76;
    public const double IntroZoom = 0.53;

    /// <summary>Containers narrower than this stack the toolbar, fix the plot at 280 and orbit from 50°.</summary>
    public const double NarrowWidth = 560;

    /// <summary>Containers at least this wide put the summary in a column beside the plot.</summary>
    public const double WideWidth = 880;

    /// <summary>Plot height: 44% of the container width, clamped to [280, 460] (always 280 when narrow).</summary>
    public const double PlotHeightRatio = 0.44;

    public const double PlotMinHeight = 280;
    public const double PlotMaxHeight = 460;

    /// <summary>
    /// Day labels are thinned until the first pair (Mon → next shown day) is this far apart (px): 24.7 is refused and 30.8
    /// accepted at rest, 29.5 refused at pitch 10° (top view: <see cref="TopDayLabelSpacing"/>).
    /// </summary>
    public const double DayLabelSpacing = 30;

    public const double TopDayLabelSpacing = 18;

    /// <summary>Hour labels read "HH:00" when three hours span at least this many px, "HH" otherwise.</summary>
    public const double LongHourLabelSpacing = 58;

    /// <summary>Per-bar delay of a metric switch: 12.2ms per day (Monday first) + 10.7ms per hour.</summary>
    public const double MetricDelayPerDay = 0.0122;

    public const double MetricDelayPerHour = 0.0107;

    /// <summary>Per-bar delay of the intro: 110ms + 21.6ms per day + 26ms per hour.</summary>
    public const double IntroDelayBase = 0.110;

    public const double IntroDelayPerDay = 0.0216;
    public const double IntroDelayPerHour = 0.026;

    /// <summary>Bars rising to new values on a metric switch or view change: k 162, c 26 (ζ ≈ 1.02).</summary>
    public static readonly SpringSpec BarSpring = SpringSpec.FromPhysical(162, 26);

    /// <summary>Bars growing in the intro: k 60, c 16.</summary>
    public static readonly SpringSpec IntroBarSpring = SpringSpec.FromPhysical(60, 16);

    /// <summary>The camera swinging down in the intro: k 7.6, c 5.4 (ζ ≈ 0.98, about 3s).</summary>
    public static readonly SpringSpec IntroCameraSpring = SpringSpec.FromPhysical(7.6, 5.4);

    /// <summary>A bar lifting under the pointer and dropping back: k 254, c 33.</summary>
    public static readonly SpringSpec LiftSpring = SpringSpec.FromPhysical(254, 33);

    /// <summary>Bars off the highlighted row and column fading back: k 594, c 52.5.</summary>
    public static readonly SpringSpec DimSpring = SpringSpec.FromPhysical(594, 52.5);

    /// <summary>The fit (focal length and anchor) chasing the fit of the current pitch: critical ω 14.</summary>
    public static readonly SpringSpec FitSpring = ShowpieceSprings.Critical(14);

    /// <summary>How much a dimmed face moves toward <c>SurfaceMuted</c>.</summary>
    public const double DimAmount = 0.3;

    /// <summary>Side faces are the top color with every OKLab channel scaled: the left-facing side and the right-facing side.</summary>
    public const double LeftShade = 0.905;

    public const double RightShade = 0.890;

    // The fit's pitch table (spec 6.1), measured on a 623 × 292 canvas: f / f(30°), and the target's screen offset from the
    // canvas center divided by f. 90° extends it for the top view transition.
    private static readonly double[] s_pitchKnots = [10, 30, 39.5, 48.9, 58.4, 62.2, 65.7, 69.4, 84, 90];
    private static readonly double[] s_focalRatio = [869.0 / 801, 1, 768.0 / 801, 697.0 / 801, 621.0 / 801, 587.0 / 801, 559.0 / 801, 527.0 / 801, 499.0 / 801, 0.62];
    private static readonly double[] s_offsetX = [20.4 / 869, 16.3 / 801, 15.1 / 768, 12.3 / 697, 9.2 / 621, 7.8 / 587, 6.4 / 559, 4.4 / 527, 1.2 / 499, 0];
    private static readonly double[] s_offsetY = [10.5 / 869, -24.8 / 801, -27.0 / 768, -31.3 / 697, -28.5 / 621, -25.3 / 587, -21.7 / 559, -15.9 / 527, -4.5 / 499, 0];
    private static readonly double[] s_focalSlopes = PchipSlopes(s_pitchKnots, s_focalRatio);
    private static readonly double[] s_offsetXSlopes = PchipSlopes(s_pitchKnots, s_offsetX);
    private static readonly double[] s_offsetYSlopes = PchipSlopes(s_pitchKnots, s_offsetY);
    private static readonly int[] s_dayLabelSteps = [1, 2, 3, 6];

    public static double CellX(int hour) => hour - 11.5;

    public static double CellZ(int day) => day - 3;

    public static double Radians(double degrees) => degrees * Math.PI / 180;

    public static double RestYaw(bool narrow) => Radians(narrow ? NarrowRestYawDegrees : RestYawDegrees);

    /// <summary>The default camera target: above the grid's center.</summary>
    public static ShowpieceVec3 RestTarget => new(0, TargetHeight, 0);

    /// <summary>The camera pose the view returns to (Reset view) in each mode.</summary>
    public static ShowpieceOrbitPose RestPose(ActivityTerrainViewMode mode, bool narrow) => mode == ActivityTerrainViewMode.Top
        ? new ShowpieceOrbitPose { Yaw = 0, Pitch = Math.PI / 2, Zoom = 1, Distance = TopDistance, Target = RestTarget }
        : new ShowpieceOrbitPose { Yaw = RestYaw(narrow), Pitch = Radians(RestPitchDegrees), Zoom = 1, Distance = Distance, Target = RestTarget };

    /// <summary>The plot height for a container (the block's own width): 280 when narrow, else 44% clamped to [280, 460].</summary>
    public static double PlotHeight(double containerWidth) => containerWidth < NarrowWidth
        ? PlotMinHeight
        : Math.Clamp(PlotHeightRatio * containerWidth, PlotMinHeight, PlotMaxHeight);

    /// <summary>
    /// Focal length at rest (pitch 30°, distance 42) for a canvas (spec 8): min(1.535·(W − 52), 3.133·(H − 36)) at yaw
    /// 32°; narrow containers (yaw 50°) use min(1.709·(W − 36), 2.712·(H − 36)).
    /// </summary>
    public static double RestFocal(Size canvas, bool narrow)
    {
        var f = narrow
            ? Math.Min(1.709 * (canvas.Width - 36), 2.712 * (canvas.Height - 36))
            : Math.Min(1.535 * (canvas.Width - 52), 3.133 * (canvas.Height - 36));
        return Math.Max(1, f);
    }

    /// <summary>The target's offset from the canvas center at rest, as a fraction of the rest focal length.</summary>
    public static Vector RestAnchorRatio(bool narrow) => narrow ? new Vector(0.0428, -0.0529) : new Vector(0.02036, -0.0310);

    /// <summary>
    /// The perspective fit at a pitch (radians): the rest fit scaled by the measured pitch table (monotone cubic between
    /// the measured pitches; flat outside them).
    /// </summary>
    public static ActivityTerrainFit PerspectiveFit(Size canvas, double pitch, bool narrow)
    {
        var degrees = pitch * 180 / Math.PI;
        var focal = RestFocal(canvas, narrow) * Pchip(s_pitchKnots, s_focalRatio, s_focalSlopes, degrees);
        var rest = RestAnchorRatio(narrow);
        var ox = Pchip(s_pitchKnots, s_offsetX, s_offsetXSlopes, degrees) * (rest.X / s_offsetX[1]);
        var oy = Pchip(s_pitchKnots, s_offsetY, s_offsetYSlopes, degrees) * (rest.Y / s_offsetY[1]);
        return new ActivityTerrainFit(focal / Distance, new Vector(ox * focal, oy * focal));
    }

    /// <summary>
    /// The top view fit: the floor (24.6 × 7.6 units) centered, keeping 47.5px left and right and 64.3px above and below
    /// (the measured 623 × 292 canvas: 21.46px per unit), whichever side is tighter.
    /// </summary>
    public static ActivityTerrainFit TopFit(Size canvas)
    {
        var byWidth = (canvas.Width - 95) / (2 * FloorHalfX);
        var byHeight = (canvas.Height - 128.6) / (2 * FloorHalfZ);
        return new ActivityTerrainFit(Math.Max(1, Math.Min(byWidth, byHeight)), default);
    }

    /// <summary>The fit for a view mode and the rendered pitch.</summary>
    public static ActivityTerrainFit Fit(Size canvas, double pitch, bool narrow, ActivityTerrainViewMode mode) =>
        mode == ActivityTerrainViewMode.Top ? TopFit(canvas) : PerspectiveFit(canvas, pitch, narrow);

    /// <summary>The smallest of 1, 2, 3, 6 days between labels that keeps them <see cref="DayLabelSpacing"/> px apart.</summary>
    public static int DayLabelStep(double pxPerDay, bool top)
    {
        var minimum = top ? TopDayLabelSpacing : DayLabelSpacing;
        foreach (var step in s_dayLabelSteps)
        {
            if (pxPerDay * step >= minimum)
            {
                return step;
            }
        }
        return 6;
    }

    /// <summary>Hour labels read "HH:00" when three hours span at least 58px.</summary>
    public static bool UsesLongHourLabels(double pxPerThreeHours) => pxPerThreeHours >= LongHourLabelSpacing;

    /// <summary>Delay of a bar in the metric switch wave (seconds).</summary>
    public static double MetricDelay(int day, int hour) => MetricDelayPerDay * day + MetricDelayPerHour * hour;

    /// <summary>Delay of a bar in the intro wave (seconds).</summary>
    public static double IntroDelay(int day, int hour) => IntroDelayBase + IntroDelayPerDay * day + IntroDelayPerHour * hour;

    /// <summary>A bar's height for a value: linear in the value, the week's largest reaching <see cref="MaxBarHeight"/>.</summary>
    public static double BarHeight(double value, double max) => max > 0 ? Math.Max(0, value) / max * MaxBarHeight : 0;

    /// <summary>
    /// Where the camera is for a pose (world units): it orbits <paramref name="target"/> at <paramref name="distance"/>,
    /// <paramref name="pitch"/> above the floor and turned by <paramref name="yaw"/> (the inverse of the orbit view's
    /// rotation).
    /// </summary>
    public static ShowpieceVec3 CameraPosition(double yaw, double pitch, double distance, ShowpieceVec3 target)
    {
        var ground = Math.Cos(pitch) * distance;
        return new ShowpieceVec3(target.X - ground * Math.Sin(yaw), target.Y + Math.Sin(pitch) * distance, target.Z + ground * Math.Cos(yaw));
    }

    // Monotone cubic (Fritsch–Carlson) ---------------------------------------------------------------------------------

    private static double[] PchipSlopes(double[] x, double[] y)
    {
        var n = x.Length;
        var d = new double[n];
        var delta = new double[n - 1];
        for (var i = 0; i < n - 1; i++)
        {
            delta[i] = (y[i + 1] - y[i]) / (x[i + 1] - x[i]);
        }
        d[0] = delta[0];
        d[n - 1] = delta[n - 2];
        for (var i = 1; i < n - 1; i++)
        {
            if (delta[i - 1] * delta[i] <= 0)
            {
                d[i] = 0;
            }
            else
            {
                var h0 = x[i] - x[i - 1];
                var h1 = x[i + 1] - x[i];
                var w1 = 2 * h1 + h0;
                var w2 = h1 + 2 * h0;
                d[i] = (w1 + w2) / (w1 / delta[i - 1] + w2 / delta[i]);
            }
        }
        return d;
    }

    internal static double Pchip(double[] x, double[] y, double[] d, double at)
    {
        if (double.IsNaN(at) || at <= x[0])
        {
            return y[0];
        }
        var last = x.Length - 1;
        if (at >= x[last])
        {
            return y[last];
        }
        var i = 0;
        while (at > x[i + 1])
        {
            i++;
        }
        var h = x[i + 1] - x[i];
        var t = (at - x[i]) / h;
        var t2 = t * t;
        var t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * y[i] + (t3 - 2 * t2 + t) * h * d[i] + (-2 * t3 + 3 * t2) * y[i + 1] + (t3 - t2) * h * d[i + 1];
    }
}

/// <summary>
/// The terrain's color scale from ArcDesign tokens (spec 4.3): the low end is halfway (OKLab) between
/// <c>SurfaceMuted</c> and <c>BorderStrong</c>, the high end is the metric's tone; the legend shows the scale at
/// 0, 0.2 … 1 and every bar's top takes it at value ÷ the week's largest.
/// </summary>
internal static class ActivityTerrainPalette
{
    public static ActivityTerrainOklab Low(Control owner) =>
        ActivityTerrainOklab.FromColor(ArcTokens.GetColor(owner, DesignTokens.ArcDesignTokenKind.SurfaceMuted))
            .Mix(ActivityTerrainOklab.FromColor(ArcTokens.GetColor(owner, DesignTokens.ArcDesignTokenKind.BorderStrong)), 0.5);

    public static Color ToneColor(Control owner, ActivityTerrainTone tone) => tone switch
    {
        ActivityTerrainTone.Neutral => Oklch.MixOklab(ArcTokens.GetColor(owner, DesignTokens.ArcDesignTokenKind.Foreground),
            ArcTokens.GetColor(owner, DesignTokens.ArcDesignTokenKind.Background), 0.15),
        ActivityTerrainTone.Success => ArcTokens.GetColor(owner, DesignTokens.ArcDesignTokenKind.Success),
        ActivityTerrainTone.Warning => ArcTokens.GetColor(owner, DesignTokens.ArcDesignTokenKind.Warning),
        ActivityTerrainTone.Danger => ArcTokens.GetColor(owner, DesignTokens.ArcDesignTokenKind.Danger),
        _ => ArcTokens.GetColor(owner, DesignTokens.ArcDesignTokenKind.Accent)
    };

    /// <summary>The scale at <paramref name="t"/> (0 = low, 1 = tone).</summary>
    public static Color At(in ActivityTerrainOklab low, in ActivityTerrainOklab tone, double t) => low.Mix(tone, Math.Clamp(t, 0, 1)).ToColor();
}

/// <summary>A color in OKLab (with alpha), for the terrain's per-face shading: mixes and channel scales stay linear here.</summary>
internal readonly record struct ActivityTerrainOklab(double L, double A, double B, double Alpha = 1)
{
    public static ActivityTerrainOklab FromColor(Color color)
    {
        var r = Decode(color.R / 255.0);
        var g = Decode(color.G / 255.0);
        var b = Decode(color.B / 255.0);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return new ActivityTerrainOklab(
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s,
            color.A / 255.0);
    }

    public Color ToColor()
    {
        var l_ = L + 0.3963377774 * A + 0.2158037573 * B;
        var m_ = L - 0.1055613458 * A - 0.0638541728 * B;
        var s_ = L - 0.0894841775 * A - 1.2914855480 * B;
        var ll = l_ * l_ * l_;
        var mm = m_ * m_ * m_;
        var ss = s_ * s_ * s_;
        var r = 4.0767416621 * ll - 3.3077115913 * mm + 0.2309699292 * ss;
        var g = -1.2684380046 * ll + 2.6097574011 * mm - 0.3413193965 * ss;
        var b = -0.0041960863 * ll - 0.7034186147 * mm + 1.7076147010 * ss;
        return Color.FromArgb(ToByte(Alpha), ToByte(Encode(r)), ToByte(Encode(g)), ToByte(Encode(b)));
    }

    /// <summary>Linear mix in OKLab (t = 0 → this, t = 1 → <paramref name="other"/>).</summary>
    public ActivityTerrainOklab Mix(in ActivityTerrainOklab other, double t) => new(
        L + (other.L - L) * t, A + (other.A - A) * t, B + (other.B - B) * t, Alpha + (other.Alpha - Alpha) * t);

    /// <summary>Every channel (not alpha) times <paramref name="factor"/>: the side faces' shading.</summary>
    public ActivityTerrainOklab Scale(double factor) => new(L * factor, A * factor, B * factor, Alpha);

    private static double Decode(double x) => x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);

    private static double Encode(double x)
    {
        x = x < 0 ? 0 : x > 1 ? 1 : x;
        return x <= 0.0031308 ? 12.92 * x : 1.055 * Math.Pow(x, 1 / 2.4) - 0.055;
    }

    private static byte ToByte(double unit) => (byte)Math.Round(Math.Clamp(unit, 0, 1) * 255.0);
}
