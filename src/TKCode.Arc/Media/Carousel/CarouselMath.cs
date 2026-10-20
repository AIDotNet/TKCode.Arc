using System.Buffers;
using TKCode.Arc.Gestures;

namespace TKCode.Arc;

/// <summary>Arc carousel geometry and release physics, kept pure for tests.</summary>
internal static class CarouselMath
{
    /// <summary>Movement in px before a press is read as a drag or handed to the page as a vertical scroll.</summary>
    public const double Slop = 6;

    /// <summary>Release speed in px/s that always moves at least one slide.</summary>
    public const double Flick = 360;

    /// <summary>Paging deceleration rate of the momentum projection.</summary>
    public const double DecayRate = 0.995;

    /// <summary>Width of a resting dot and of the active pill, in px.</summary>
    public const double Dot = 6;

    public const double Pill = 22;

    /// <summary>Scale and opacity of a slide one full step from the center.</summary>
    public const double NeighbourScale = 0.9;

    public const double NeighbourOpacity = 0.5;

    /// <summary>Carousel widths at or below this tighten the control row (Arc container query).</summary>
    public const double CompactWidth = 340;

    /// <summary><c>min(ratio × width, max)</c>.</summary>
    public static double SlideWidth(double width, double ratio, double max) => Math.Max(0, Math.Min(width * ratio, max));

    /// <summary>Past an edge every pixel costs more, like pulling against elastic.</summary>
    public static double Band(double raw, double min, double max, double size)
        => RubberBand.Clamp(raw, min, max, Math.Max(1, size));

    /// <summary>Inverse of <see cref="Band"/>: the raw offset that shows <paramref name="shown"/>.</summary>
    public static double Unband(double shown, double min, double max, double size)
    {
        size = Math.Max(1, size);
        if (shown > max)
        {
            return max + RubberBand.Invert(shown - max, size);
        }
        if (shown < min)
        {
            return min + RubberBand.Invert(shown - min, size);
        }
        return shown;
    }

    /// <summary>How much of the pill an indicator shows (1 at its slide, 0 one step away).</summary>
    public static double Emphasis(double progress, int index, int last)
        => Math.Max(0, 1 - Math.Abs(Math.Clamp(progress, 0, Math.Max(0, last)) - index));

    /// <summary>Distance of a slide from the center, in steps, capped at one.</summary>
    public static double Distance(double progress, int index) => Math.Min(1, Math.Abs(progress - index));

    /// <summary>
    /// The slide a release lands on: where the flick would carry the track (not where the finger let go); a release
    /// faster than <see cref="Flick"/> always moves at least one slide in its direction.
    /// </summary>
    public static int SelectTarget(double offset, double velocity, double step, int from, int count)
    {
        if (count <= 0 || step <= 0)
        {
            return 0;
        }
        var rented = count > 256 ? ArrayPool<double>.Shared.Rent(count) : null;
        try
        {
            var points = rented is null ? stackalloc double[count] : rented.AsSpan(0, count);
            for (var i = 0; i < count; i++)
            {
                points[i] = -i * step;
            }
            var to = MomentumProjector.SelectSnapPoint(points, offset, velocity,
                SnapProjection.Decay(DecayRate, Math.Clamp(from, 0, count - 1), Flick));
            if (Math.Abs(velocity) > Flick)
            {
                var direction = velocity < 0 ? 1 : -1;
                if ((to - from) * direction < 1)
                {
                    to = from + direction;
                }
            }
            return Math.Clamp(to, 0, count - 1);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Default slide name: "2 of 5".</summary>
    public static string DefaultSlideLabel(int index, int count) => $"{index + 1} of {count}";
}
