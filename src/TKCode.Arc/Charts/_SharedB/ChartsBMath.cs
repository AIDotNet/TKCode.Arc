using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Small numeric, color and motion helpers shared by team B's charts.</summary>
internal static class ChartsBMath
{
    public static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    public static double Clamp(double value, double low, double high) => Math.Min(high, Math.Max(low, value));

    public static double Mix(double a, double b, double t) => a + (b - a) * t;

    public static Point Mix(Point a, Point b, double t) => new(Mix(a.X, b.X, t), Mix(a.Y, b.Y, t));

    public static Rect Mix(Rect a, Rect b, double t)
        => new(Mix(a.X, b.X, t), Mix(a.Y, b.Y, t), Math.Max(0, Mix(a.Width, b.Width, t)), Math.Max(0, Mix(a.Height, b.Height, t)));

    /// <summary>CSS <c>color-mix(in oklab, <paramref name="color"/> percent%, <paramref name="other"/>)</c>.</summary>
    public static Color MixIn(Color color, double percent, Color other) => Oklch.MixOklab(other, color, Clamp(percent, 0, 1));

    /// <summary>CSS <c>color-mix(in oklab, color percent%, transparent)</c>: the color at that alpha.</summary>
    public static Color Alpha(Color color, double alpha) => Oklch.WithAlpha(color, color.A / 255.0 * Clamp(alpha, 0, 1));

    /// <summary>Straight sRGB interpolation, like a CSS background-color transition.</summary>
    public static Color Lerp(Color a, Color b, double t)
    {
        t = Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    /// <summary>Motion's <c>physical({ visualDuration, bounce })</c> as used in Arc's chart sources.</summary>
    public static SpringSpec Physical(double visualDuration, double bounce)
    {
        var root = 2 * Math.PI / (visualDuration * 1.2);
        return new SpringSpec(root * root, 2 * (1 - bounce) * root);
    }

    /// <summary>"Nice" step for about <paramref name="count"/> ticks: 1, 2 or 5 × 10ⁿ.</summary>
    public static double NiceStep(double span, int count)
    {
        var raw = span / Math.Max(1, count);
        if (!(raw > 0) || double.IsInfinity(raw))
        {
            raw = 1;
        }
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var unit = raw / power;
        return (unit >= 5 ? 5 : unit >= 2 ? 2 : 1) * power;
    }

    /// <summary>Arc's default number format: en-US grouping, at most one fraction digit.</summary>
    public static string Grouped(double value) => value.ToString("#,0.#", EnUs);

    /// <summary>A percentage with at most one fraction digit, like <c>Intl.NumberFormat({ style: "percent" })</c>.</summary>
    public static string Percent(double fraction, int maxDigits = 0)
        => (fraction * 100).ToString(maxDigits > 0 ? "#,0." + new string('#', maxDigits) : "#,0", EnUs) + "%";

    /// <summary>Linear easing for clock-style tweens.</summary>
    public static readonly Avalonia.Animation.Easings.Easing Linear = new Avalonia.Animation.Easings.LinearEasing();

    /// <summary>Evaluates an Arc easing curve (cubic-bezier) at <paramref name="t"/>.</summary>
    public static double Ease(Avalonia.Animation.Easings.Easing easing, double t) => easing.Ease(Clamp(t, 0, 1));

    /// <summary>Position and velocity of a spring released from <paramref name="from"/> toward <paramref name="to"/>.</summary>
    public static (double Value, double Velocity) Spring(in SpringSpec spec, double from, double to, double velocity, double t)
    {
        if (t <= 0)
        {
            return (from, velocity);
        }
        var (x, v) = SpringSolver.Evaluate(spec, from - to, velocity, t);
        return (to + x, v);
    }
}
