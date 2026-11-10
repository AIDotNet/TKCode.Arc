using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="GlassSegmented"/>: the inset between the glass track and the lens, and the gap between segments.
/// The material comes from <see cref="GlassPaneToken"/>; corners, shadows and text colors from the ArcDesign tokens.
/// </summary>
[ControlDesignToken]
internal sealed class GlassSegmentedToken : AbstractControlDesignToken
{
    /// <summary>Space between the track's edge and the segments (the lens sits in it).</summary>
    public Thickness TrackInset { get; set; }

    /// <summary>Gap between two segments.</summary>
    public double SegmentGap { get; set; }

    /// <summary>How far the lens swells on every side while pressed or dragged.</summary>
    public double LensSwell { get; set; }

    /// <summary>Opacity of a disabled control.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TrackInset = new Thickness(4);
        SegmentGap = 2;
        LensSwell = 2;
        DisabledOpacity = 0.5;
    }
}

/// <summary>Geometry of one <see cref="GlassSegment"/>: its height, inner space, icon and the keyboard focus ring.</summary>
[ControlDesignToken]
internal sealed class GlassSegmentToken : AbstractControlDesignToken
{
    /// <summary>Height of a segment.</summary>
    public double SegmentHeight { get; set; }

    /// <summary>Space around a segment's label.</summary>
    public Thickness SegmentPadding { get; set; }

    /// <summary>Size of a segment's icon.</summary>
    public double IconSize { get; set; }

    /// <summary>Gap between the icon and the label.</summary>
    public double IconGap { get; set; }

    /// <summary>The focus ring sits this far outside the segment.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SegmentHeight = 34;
        SegmentPadding = new Thickness(14, 0);
        IconSize = 16;
        IconGap = 6;
        FocusRingMargin = new Thickness(-2);
    }
}
