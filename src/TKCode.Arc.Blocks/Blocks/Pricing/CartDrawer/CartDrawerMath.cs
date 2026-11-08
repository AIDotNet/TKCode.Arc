using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;

namespace TKCode.Arc;

/// <summary>
/// The pure numbers of <see cref="CartDrawer"/>: totals, the free-shipping progress, the swipe-to-remove response, the
/// fly-to-cart path and the keyframed bumps. Measured on Arc's Cart drawer (design reference, ADR 0039); every formula
/// is checked against the observed values in the tests.
/// </summary>
internal static class CartDrawerMath
{
    /// <summary>Most of one product a cart row holds by default.</summary>
    public const int DefaultMaxQuantity = 9;

    // ---- Totals ----

    /// <summary>Σ price × quantity.</summary>
    public static decimal Subtotal(IEnumerable<CartDrawerLine> lines)
    {
        var sum = 0m;
        foreach (var line in lines)
        {
            sum += line.LineTotal;
        }
        return sum;
    }

    /// <summary>
    /// What a promo takes off: subtotal × percent, rounded to <paramref name="decimals"/> places with halves away from zero
    /// (Arc: 6.4 → 6, 18.6 → 19, 37.2 → 37).
    /// </summary>
    public static decimal Discount(decimal subtotal, decimal percentOff, int decimals)
    {
        if (subtotal <= 0 || percentOff <= 0)
        {
            return 0m;
        }
        return Math.Round(subtotal * Math.Min(percentOff, 1m), Math.Clamp(decimals, 0, 4), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Shipping: free once the subtotal before the discount reaches the threshold (a threshold of 0 or less means always
    /// free), the fee otherwise; an empty cart ships nothing.
    /// </summary>
    public static decimal Shipping(decimal subtotal, decimal threshold, decimal fee)
    {
        if (subtotal <= 0)
        {
            return 0m;
        }
        if (threshold <= 0 || subtotal >= threshold)
        {
            return 0m;
        }
        return Math.Max(0m, fee);
    }

    /// <summary>Subtotal − discount + shipping, never below zero.</summary>
    public static decimal Total(decimal subtotal, decimal discount, decimal shipping) => Math.Max(0m, subtotal - discount + shipping);

    /// <summary>Share of the free-shipping threshold reached, 0–1 (1 when there is no threshold).</summary>
    public static double Progress(decimal subtotal, decimal threshold)
    {
        if (threshold <= 0)
        {
            return 1;
        }
        return (double)Math.Clamp(subtotal / threshold, 0m, 1m);
    }

    /// <summary>What is still missing for free shipping (0 once reached).</summary>
    public static decimal Remaining(decimal subtotal, decimal threshold) => threshold <= 0 ? 0m : Math.Max(0m, threshold - subtotal);

    /// <summary>A quantity kept between 0 and the row's maximum.</summary>
    public static int ClampQuantity(int quantity, int max) => Math.Clamp(quantity, 0, Math.Max(1, max));

    // ---- Swipe to remove ----

    /// <summary>Distance (px) past which a released row is removed.</summary>
    public const double RemoveDistance = 110;

    /// <summary>Release speed (px/s, leftward) that removes a row that has moved at least <see cref="FlickMinDistance"/>.</summary>
    public const double RemoveVelocity = 600;

    /// <summary>The least travel (px) a flick needs.</summary>
    public const double FlickMinDistance = 20;

    /// <summary>Opacity of the red layer behind a row dragged |x| px: 0 → 0.35 at 28 → 1 at 140.</summary>
    public static double SwipeBackdropOpacity(double offset) => Interpolate(Math.Abs(offset), 0, 28, 140, 0, 0.35, 1);

    /// <summary>Scale of the "Remove" label: 0.7 up to 40px, 1 from 120px, linear between.</summary>
    public static double SwipeLabelScale(double offset)
    {
        var x = Math.Abs(offset);
        return 0.7 + 0.3 * Math.Clamp((x - 40) / 80, 0, 1);
    }

    /// <summary>The row would be removed if released now (the red layer turns a step deeper).</summary>
    public static bool IsSwipeArmed(double offset) => offset < -RemoveDistance;

    /// <summary>Whether a release at <paramref name="offset"/> (px, negative = left) and <paramref name="velocity"/> (px/s) removes the row.</summary>
    public static bool ShouldRemove(double offset, double velocity)
        => offset < -RemoveDistance || (velocity < -RemoveVelocity && offset < -FlickMinDistance);

    // ---- Fly to cart ----

    /// <summary>Flight length.</summary>
    public static readonly TimeSpan FlightDuration = TimeSpan.FromMilliseconds(780);

    /// <summary>The flight's progress curve, cubic-bezier(.3, 0, .2, 1).</summary>
    public static readonly Easing FlightEasing = new SplineEasing(0.3, 0, 0.2, 1);

    /// <summary>Side of the square the clone shrinks to.</summary>
    public const double FlightEndSize = 26;

    /// <summary>
    /// The quadratic Bézier's control point: 55% of the way across, 30px above the higher of the two ends.
    /// </summary>
    public static Point FlightControl(Point from, Point to, double ratio = 0.55, double arc = 30)
        => new(from.X + ratio * (to.X - from.X), Math.Min(from.Y, to.Y) - arc);

    /// <summary>The clone's center at progress <paramref name="p"/>: (1−p)²P0 + 2p(1−p)C + p²P2.</summary>
    public static Point FlightPoint(Point from, Point control, Point to, double p)
    {
        var u = 1 - p;
        return new Point(
            u * u * from.X + 2 * p * u * control.X + p * p * to.X,
            u * u * from.Y + 2 * p * u * control.Y + p * p * to.Y);
    }

    /// <summary>The clone's size: from the picture's size to 26 × 26 by p = 0.8, then held.</summary>
    public static Size FlightSize(Size start, double p, double end = FlightEndSize)
    {
        var t = Math.Clamp(p / 0.8, 0, 1);
        return new Size(start.Width + (end - start.Width) * t, start.Height + (end - start.Height) * t);
    }

    /// <summary>Corner radius 16 → 13, linear in p.</summary>
    public static double FlightRadius(double p) => 16 - 3 * Math.Clamp(p, 0, 1);

    /// <summary>Rotation in degrees: −10°·sin(πp) (leans forward, −10° half way).</summary>
    public static double FlightRotation(double p) => -10 * Math.Sin(Math.PI * Math.Clamp(p, 0, 1));

    /// <summary>Opacity: 1 until p = 0.86, then linear to 0 at the end.</summary>
    public static double FlightOpacity(double p) => 1 - Math.Clamp((p - 0.86) / 0.14, 0, 1);

    /// <summary>The lift s = sin(πp) the shadow follows: offset 4+14s, blur 10+26s, alpha 0.08+0.12s.</summary>
    public static (double OffsetY, double Blur, double Alpha) FlightShadow(double p)
    {
        var s = Math.Sin(Math.PI * Math.Clamp(p, 0, 1));
        return (4 + 14 * s, 10 + 26 * s, 0.08 + 0.12 * s);
    }

    // ---- Keyframes ----

    /// <summary>Length of the cart button's bump.</summary>
    public static readonly TimeSpan BumpDuration = TimeSpan.FromMilliseconds(430);

    private static readonly double[] s_bumpTimes = [0, 0.33, 0.70, 1];
    private static readonly double[] s_bumpValues = [1, 1.09, 0.98, 1];

    /// <summary>The cart button's scale at bump progress <paramref name="t"/> (0–1): 1 → 1.09 → 0.98 → 1, eased per segment.</summary>
    public static double BumpScale(double t) => Keyframes(s_bumpTimes, s_bumpValues, t);

    /// <summary>Length of the invalid-code shake.</summary>
    public static readonly TimeSpan ShakeDuration = TimeSpan.FromMilliseconds(380);

    private static readonly double[] s_shakeTimes = [0, 0.2, 0.4, 0.6, 0.8, 1];
    private static readonly double[] s_shakeValues = [0, -6, 5, -3, 2, 0];

    /// <summary>Horizontal offset of the code field at shake progress <paramref name="t"/>: 0, −6, 5, −3, 2, 0 evenly spaced.</summary>
    public static double ShakeOffset(double t) => Keyframes(s_shakeTimes, s_shakeValues, t);

    /// <summary>The ripple ring's scale 1 → 1.45 and opacity 0.45 → 0 for a spring position <paramref name="t"/> (0–1).</summary>
    public static (double Scale, double Opacity) Ripple(double t)
    {
        var c = Math.Clamp(t, 0, 1);
        return (1 + 0.45 * c, 0.45 * (1 - c));
    }

    // ---- Receipt ----

    /// <summary>The sample delivery window: 3 to 5 days after <paramref name="today"/> ("Nov 11 – Nov 13").</summary>
    public static string ArrivalWindow(DateTime today, CultureInfo culture)
    {
        var first = today.Date.AddDays(3);
        var last = today.Date.AddDays(5);
        return $"{first.ToString("MMM d", culture)} – {last.ToString("MMM d", culture)}";
    }

    // ---- Helpers ----

    private static double Interpolate(double x, double x0, double x1, double x2, double y0, double y1, double y2)
    {
        if (x <= x0)
        {
            return y0;
        }
        if (x <= x1)
        {
            return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
        }
        if (x <= x2)
        {
            return y1 + (y2 - y1) * (x - x1) / (x2 - x1);
        }
        return y2;
    }

    /// <summary>Keyframe value with an ease-in-out (sine) inside each segment, like WAAPI keyframes with an ease.</summary>
    private static double Keyframes(double[] times, double[] values, double t)
    {
        if (t <= 0)
        {
            return values[0];
        }
        if (t >= 1)
        {
            return values[^1];
        }
        for (var i = 1; i < times.Length; i++)
        {
            if (t <= times[i])
            {
                var local = (t - times[i - 1]) / (times[i] - times[i - 1]);
                var eased = 0.5 - 0.5 * Math.Cos(Math.PI * local);
                return values[i - 1] + (values[i] - values[i - 1]) * eased;
            }
        }
        return values[^1];
    }
}
