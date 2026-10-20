using Avalonia;
using Avalonia.Input;
using TKCode.Arc.Gestures;

namespace TKCode.Arc;

/// <summary>Arc image-compare geometry and key model, kept pure so it can be tested without a window.</summary>
internal static class ImageCompareMath
{
    /// <summary>Rubber-band travel past an edge (px).</summary>
    public const double Stretch = 9;

    /// <summary>The handle's inset from the frame edge (px).</summary>
    public const double HandleMargin = 42;

    /// <summary>How far the handle gives inside that inset (px).</summary>
    public const double Give = 12;

    /// <summary>Speed of the kick a key press gives at a limit (px/s).</summary>
    public const double BumpSpeed = 150;

    /// <summary>Caption chip inset (px).</summary>
    public const double ChipInset = 12;

    public static double Clamp(double value, double low, double high) => Math.Min(high, Math.Max(low, value));

    public static double Lerp(double from, double to, double amount) => from + (to - from) * amount;

    /// <summary>JavaScript <c>Math.round</c> (halves round up).</summary>
    public static double JsRound(double value) => Math.Floor(value + 0.5);

    /// <summary>iOS style resistance past a limit, never more than <see cref="Stretch"/> px.</summary>
    public static double Rubber(double distance) => RubberBand.Apply(distance, Stretch);

    /// <summary>The handle keeps the line's pace in the middle and slows smoothly near an edge.</summary>
    public static double GiveAt(double distance) => Give * (1 - 1 / (distance / Give + 1));

    /// <summary>Where the handle sits along the axis for a line at <paramref name="at"/> px in a frame of <paramref name="size"/> px.</summary>
    public static double Soften(double at, double size)
    {
        if (at < HandleMargin)
        {
            return HandleMargin - GiveAt(HandleMargin - at);
        }
        if (at > size - HandleMargin)
        {
            return size - HandleMargin + GiveAt(at - size + HandleMargin);
        }
        return at;
    }

    /// <summary>Caption opacity for the room left between it and the line (fades over 28px).</summary>
    public static double Fade(double room) => Clamp(room / 28, 0, 1);

    /// <summary>
    /// The displayed position (percent) for a raw pointer position (percent): clamped to 0–100 with rubber-band
    /// resistance past the edges (none under reduced motion).
    /// </summary>
    public static double Place(double raw, double size, bool reduced)
    {
        var edge = Clamp(raw, 0, 100);
        if (reduced || size <= 0)
        {
            return edge;
        }
        return edge + Rubber((raw - edge) / 100 * size) / size * 100;
    }

    /// <summary>
    /// Arc key model. Keys move the divider the way they point along its axis; the other two follow the value, where up
    /// and right mean more of the after image. Arrows step 1% (Shift 10%), PageUp / PageDown 10%, Home shows all of the
    /// before image and End all of the after image. Returns null for keys the divider ignores. Unclamped.
    /// </summary>
    public static double? KeyTarget(Key key, bool shift, bool vertical, double latest)
    {
        var large = shift ? 10 : 1;
        double? step = key switch
        {
            Key.Up => -large,
            Key.Down => large,
            Key.Left => vertical ? large : -large,
            Key.Right => vertical ? -large : large,
            Key.PageUp => -10,
            Key.PageDown => 10,
            _ => null
        };
        if (key == Key.Home)
        {
            return 100;
        }
        if (key == Key.End)
        {
            return 0;
        }
        return step is { } s ? JsRound(latest) + s : null;
    }
}
