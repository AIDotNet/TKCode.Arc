using Avalonia;
using Avalonia.Input;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The measured numbers of Arc Control center (design reference) and the pure functions built on them: the dial (one turn
/// is 120 minutes, relative drag, rubber band past 5 and 120, 5-minute snap), the meters (relative drag, 28 segments, edge
/// stretch) and the keyboard steps. Kept free of UI so tests check them against the notes directly.
/// </summary>
internal static class ControlCenterMath
{
    public const int DialMinimum = 5;
    public const int DialMaximum = 120;
    public const int DialStep = 5;

    /// <summary>One full turn is 120 minutes: 3° per minute.</summary>
    public const double DegreesPerMinute = 3;

    /// <summary>The dial's resistance past its ends: c = 90° (30 minutes), factor 0.4 (fitted, rms 0.0055°).</summary>
    public const double DialStretchLimit = 90;

    public const double DialStretchFactor = 0.4;

    /// <summary>The meters: 28 segments 3px apart, a track that swells to 1.18 while held and stretches up to 6px (factor 0.55).</summary>
    public const int MeterSegments = 28;

    public const double MeterGap = 3;
    public const double MeterSwell = 1.18;
    public const double MeterStretchLimit = 6;
    public const double MeterStretchFactor = 0.55;

    public const int VolumeMinimum = 0;
    public const int VolumeMaximum = 100;
    public const int TextSizeMinimum = 85;
    public const int TextSizeMaximum = 130;
    public const int TextSizeStep = 5;

    /// <summary>The layout spring (ω ≈ 12.5, ζ = 1): the morph, the mode pill, tile presses and returns from overshoot.</summary>
    public static readonly SpringSpec LayoutSpring = SpringSpec.FromVisual(0.42, 0);

    /// <summary>The quick spring (ω ≈ 20, ζ = 1): dial snaps and keys, meter fills and swell.</summary>
    public static readonly SpringSpec QuickSpring = ArcMotionPresets.Quick;

    /// <summary>The direction of <paramref name="point"/> from <paramref name="center"/> in degrees clockwise from 12 o'clock, in [0, 360).</summary>
    public static double AngleOf(Point point, Point center)
    {
        var degrees = Math.Atan2(point.X - center.X, center.Y - point.Y) * 180 / Math.PI;
        return degrees < 0 ? degrees + 360 : degrees;
    }

    /// <summary>The shortest signed turn from <paramref name="from"/> to <paramref name="to"/>, in (−180, 180] degrees (angle unwrapping).</summary>
    public static double AngleDelta(double from, double to)
    {
        var delta = (to - from) % 360;
        if (delta > 180)
        {
            delta -= 360;
        }
        else if (delta <= -180)
        {
            delta += 360;
        }
        return delta;
    }

    /// <summary>
    /// Where the knob is drawn for the finger's continuous minutes: 1:1 inside [5, 120], then with resistance
    /// c·(1 − 1/(0.4·x/c + 1)) on the overshoot angle x, c = 90°.
    /// </summary>
    public static double DialKnobMinutes(double raw)
    {
        if (raw > DialMaximum)
        {
            return DialMaximum + RubberBand.Apply((raw - DialMaximum) * DegreesPerMinute, DialStretchLimit, DialStretchFactor) / DegreesPerMinute;
        }
        if (raw < DialMinimum)
        {
            return DialMinimum + RubberBand.Apply((raw - DialMinimum) * DegreesPerMinute, DialStretchLimit, DialStretchFactor) / DegreesPerMinute;
        }
        return raw;
    }

    /// <summary>The value a release snaps to (and the value reported while dragging): the nearest 5 minutes within [5, 120].</summary>
    public static int SnapMinutes(double minutes) =>
        Math.Clamp((int)Math.Round(minutes / DialStep, MidpointRounding.AwayFromZero) * DialStep, DialMinimum, DialMaximum);

    /// <summary>The number in the dial's center: the knob's minutes rounded, held at 5 and 120 in the overshoot.</summary>
    public static int DialNumber(double knobMinutes) =>
        (int)Math.Round(Math.Clamp(knobMinutes, DialMinimum, DialMaximum), MidpointRounding.AwayFromZero);

    /// <summary>The share of the ring the arc covers: minutes / 120, held at the limits.</summary>
    public static double DialArc(double knobMinutes) => Math.Clamp(knobMinutes, DialMinimum, DialMaximum) / DialMaximum;

    /// <summary>The dial's keys: arrows ±5, Page Up / Down ±15, Home 5, End 120; null for any other key.</summary>
    public static int? DialKey(int value, Key key) => key switch
    {
        Key.Left or Key.Down => Math.Max(DialMinimum, value - DialStep),
        Key.Right or Key.Up => Math.Min(DialMaximum, value + DialStep),
        Key.PageDown => Math.Max(DialMinimum, value - 3 * DialStep),
        Key.PageUp => Math.Min(DialMaximum, value + 3 * DialStep),
        Key.Home => DialMinimum,
        Key.End => DialMaximum,
        _ => null
    };

    /// <summary>A meter's segment width for a track <paramref name="width"/> wide (28 segments, 27 gaps of 3).</summary>
    public static double SegmentWidth(double width) => Math.Max(0, (width - (MeterSegments - 1) * MeterGap) / MeterSegments);

    /// <summary>The filled share of a meter: (value − minimum) / (maximum − minimum).</summary>
    public static double MeterFraction(double value, double minimum, double maximum) =>
        maximum <= minimum ? 0 : Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);

    /// <summary>The finger's continuous value: the value at the grab plus the horizontal travel as a share of the track (1:1).</summary>
    public static double MeterDragValue(double start, double dx, double width, double minimum, double maximum) =>
        width <= 0 ? start : start + dx / width * (maximum - minimum);

    /// <summary>The value under a point <paramref name="x"/> pixels into the track (a tap jumps there on release).</summary>
    public static double MeterValueAt(double x, double width, double minimum, double maximum) =>
        width <= 0 ? minimum : minimum + Math.Clamp(x / width, 0, 1) * (maximum - minimum);

    /// <summary>The track's stretch for a finger <paramref name="overflow"/> pixels past an end: c·(1 − 1/(0.55·o/c + 1)), c = 6px.</summary>
    public static double MeterStretch(double overflow) => RubberBand.Apply(overflow, MeterStretchLimit, MeterStretchFactor);

    /// <summary>The nearest step within the range.</summary>
    public static int Snap(double value, int minimum, int maximum, int step)
    {
        var steps = Math.Round((value - minimum) / step, MidpointRounding.AwayFromZero);
        return Math.Clamp(minimum + (int)steps * step, minimum, maximum);
    }

    /// <summary>A meter's keys: arrows ±1 step, Page Up / Down ±4 steps, Home and End; null for any other key.</summary>
    public static int? MeterKey(int value, Key key, int minimum, int maximum, int step) => key switch
    {
        Key.Left or Key.Down => Math.Max(minimum, value - step),
        Key.Right or Key.Up => Math.Min(maximum, value + step),
        Key.PageDown => Math.Max(minimum, value - 4 * step),
        Key.PageUp => Math.Min(maximum, value + 4 * step),
        Key.Home => minimum,
        Key.End => maximum,
        _ => null
    };

    /// <summary>The end of a session that starts at <paramref name="now"/>.</summary>
    public static DateTimeOffset Until(DateTimeOffset now, int minutes) => now.AddMinutes(minutes);

    /// <summary>The first word of a name (the footer sentence uses first names).</summary>
    public static string FirstName(string name)
    {
        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ');
        return space < 0 ? trimmed : trimmed[..space];
    }

    /// <summary>Up to two initials: the first letters of the first and last words.</summary>
    public static string Initials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "";
        }
        var first = char.ToUpperInvariant(words[0][0]).ToString();
        return words.Length == 1 ? first : first + char.ToUpperInvariant(words[^1][0]);
    }

    /// <summary>circOut, the curve of the tile fading back in over the first half of the closing morph.</summary>
    public static double CircOut(double t)
    {
        var x = Math.Clamp(t, 0, 1) - 1;
        return Math.Sqrt(1 - x * x);
    }

    /// <summary>
    /// The crossfade while the detail closes, for closing progress <paramref name="q"/> (0 open, 1 back at the tile): the
    /// tile comes in with circOut over [0, 0.5], the detail goes out linearly over [0.5, 0.95].
    /// </summary>
    public static (double Tile, double Detail) CloseCrossfade(double q)
    {
        var tile = CircOut(q / 0.5);
        var detail = 1 - Math.Clamp((q - 0.5) / 0.45, 0, 1);
        return (tile, detail);
    }
}
