using Avalonia.Animation.Easings;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Geometry, motion and gesture constants of <see cref="LiquidTabBar"/>, and the pure functions of its model: where the
/// lens edges are for a fractional tab index, and which fractional index a pointer position stands for.
/// </summary>
internal static class LiquidTabBarMetrics
{
    /// <summary>Height of the track.</summary>
    public const double TrackHeight = 48;

    /// <summary>Space between the track's edge and the tabs.</summary>
    public const double TrackPadding = 4;

    /// <summary>Height of a tab (and of the lens).</summary>
    public const double ItemHeight = 40;

    /// <summary>Gap between neighbouring tabs.</summary>
    public const double ItemSpacing = 2;

    /// <summary>Width of a tab that shows only its glyph.</summary>
    public const double SlotWidth = 44;

    /// <summary>Size of a tab's glyph.</summary>
    public const double GlyphSize = 20;

    /// <summary>Distance from a tab's left edge to its glyph.</summary>
    public const double GlyphInset = 12;

    /// <summary>Distance from a tab's left edge to its label (glyph inset + glyph + 8).</summary>
    public const double LabelOffset = 40;

    /// <summary>Space after an open label.</summary>
    public const double LabelTrailing = 14;

    /// <summary>Space on both sides of the label of a tab without a glyph.</summary>
    public const double TextPadding = 14;

    /// <summary>How much further right the label of a tab with a badge starts, clear of the badge.</summary>
    public const double BadgeLabelShift = 6;

    /// <summary>How far a closed tab's label sits to the left of its open place.</summary>
    public const double LabelSlide = 4;

    /// <summary>Openness at which an arriving label starts to show; it is fully shown at 1.</summary>
    public const double LabelRevealFrom = 0.9;

    /// <summary>Badge pill height (its digit line).</summary>
    public const double BadgeHeight = 16;

    /// <summary>Narrowest badge pill.</summary>
    public const double BadgeMinWidth = 16;

    /// <summary>Space on both sides of the badge digits.</summary>
    public const double BadgePadding = 5;

    /// <summary>Badge position relative to the glyph's left edge.</summary>
    public const double BadgeOffsetX = 13;

    /// <summary>Badge position relative to the glyph's top edge.</summary>
    public const double BadgeOffsetY = -5;

    /// <summary>Ring around a badge in the color behind it, so it reads as cut out of the glyph.</summary>
    public const double BadgeRing = 2;

    /// <summary>How far the lens draws in on every side while it is held.</summary>
    public const double PressInset = 2;

    /// <summary>How far, in tabs, the lens edge leans toward a hovered tab's side.</summary>
    public const double Lean = 0.14;

    /// <summary>Distance a press on the selected tab must travel before it drags the lens.</summary>
    public const double DragThreshold = 9;

    /// <summary>How long a release coasts at its velocity when picking where the lens lands.</summary>
    public static readonly TimeSpan FlickHorizon = TimeSpan.FromSeconds(0.16);

    /// <summary>A release at least this fast (px/s) never lands back on the tab the drag started from.</summary>
    public const double FlickVelocity = 200;

    /// <summary>Rubber band dimension past either end, as a share of the end tab's pitch.</summary>
    public const double Resistance = 0.45;

    /// <summary>The lens slide, the tabs opening and closing, the press release and the landing after a drag.</summary>
    public static readonly MotionSpec Slide = MotionSpec.Spring(SpringSpec.FromVisual(0.42, 0));

    /// <summary>The lens leaning toward a hovered tab and back.</summary>
    public static readonly MotionSpec LeanMotion = MotionSpec.Spring(SpringSpec.FromVisual(0.27, 0));

    /// <summary>The lens drawing in when it is pressed.</summary>
    public static readonly MotionSpec Press = MotionSpec.Spring(SpringSpec.FromVisual(0.2, 0));

    /// <summary>While dragging: the lens edge on the side the pointer moves toward.</summary>
    public static readonly MotionSpec DragLead = MotionSpec.Spring(SpringSpec.FromVisual(0.08, 0));

    /// <summary>While dragging: the lens edge on the other side.</summary>
    public static readonly MotionSpec DragTrail = MotionSpec.Spring(SpringSpec.FromVisual(0.21, 0));

    /// <summary>A label that should no longer show fades out at once.</summary>
    public static readonly MotionSpec LabelFadeOut = MotionSpec.Tween(TimeSpan.FromMilliseconds(50), new LinearEasing());

    /// <summary>Badge digits rolling to a new count.</summary>
    public static readonly MotionSpec BadgeRoll = MotionSpec.Spring(ArcSpring.Smooth);

    /// <summary>A badge appearing or going away.</summary>
    public static readonly MotionSpec BadgePop = MotionSpec.Spring(ArcSpring.Snappy);

    /// <summary>The x of an edge at a fractional tab <paramref name="index"/>: linear between neighbouring tabs, extrapolated past the ends.</summary>
    public static double EdgeAt(ReadOnlySpan<double> edges, double index)
    {
        var n = edges.Length;
        if (n == 0)
        {
            return 0;
        }
        if (n == 1)
        {
            return edges[0] + index * (SlotWidth + ItemSpacing);
        }
        if (index <= 0)
        {
            return edges[0] + index * (edges[1] - edges[0]);
        }
        if (index >= n - 1)
        {
            return edges[n - 1] + (index - (n - 1)) * (edges[n - 1] - edges[n - 2]);
        }
        var i = (int)Math.Floor(index);
        return edges[i] + (edges[i + 1] - edges[i]) * (index - i);
    }

    /// <summary>
    /// The fractional tab index a pointer at <paramref name="x"/> stands for: linear between neighbouring tab centers
    /// (<paramref name="centers"/>), extrapolated past the ends. The inverse of <see cref="EdgeAt"/> over the centers.
    /// </summary>
    public static double IndexAt(ReadOnlySpan<double> centers, double x)
    {
        var n = centers.Length;
        if (n == 0)
        {
            return 0;
        }
        if (n == 1)
        {
            return (x - centers[0]) / (SlotWidth + ItemSpacing);
        }
        if (x <= centers[0])
        {
            return (x - centers[0]) / Pitch(centers, 0);
        }
        for (var i = 0; i < n - 1; i++)
        {
            if (x <= centers[i + 1])
            {
                return i + (x - centers[i]) / Pitch(centers, i);
            }
        }
        return n - 1 + (x - centers[n - 1]) / Pitch(centers, n - 2);
    }

    /// <summary>The distance between tab centers around a fractional <paramref name="index"/> (px per tab there).</summary>
    public static double PitchAt(ReadOnlySpan<double> centers, double index)
    {
        var n = centers.Length;
        if (n < 2)
        {
            return SlotWidth + ItemSpacing;
        }
        var i = (int)Math.Clamp(Math.Floor(index), 0, n - 2);
        return Pitch(centers, i);
    }

    /// <summary>
    /// Applies rubber-band resistance to a fractional index past either end of <paramref name="count"/> tabs, in pixels
    /// of the end tab's pitch, like a surface pulled past its stop.
    /// </summary>
    public static double Resist(double index, int count, ReadOnlySpan<double> centers)
    {
        if (count <= 0)
        {
            return 0;
        }
        var last = count - 1;
        if (index >= 0 && index <= last)
        {
            return index;
        }
        var pitch = index < 0 ? PitchAt(centers, 0) : PitchAt(centers, last);
        var bound = index < 0 ? 0 : last;
        var overflow = (index - bound) * pitch;
        return bound + RubberBand.Apply(overflow, Resistance * pitch) / pitch;
    }

    /// <summary>How visible a label is: hidden until its tab is 90% open, and gone at once when it should no longer show.</summary>
    public static double LabelOpacity(double openness, double fade)
        => Math.Min(fade, Math.Clamp((openness - LabelRevealFrom) / (1 - LabelRevealFrom), 0, 1));

    private static double Pitch(ReadOnlySpan<double> centers, int i) => Math.Max(1, centers[i + 1] - centers[i]);
}
