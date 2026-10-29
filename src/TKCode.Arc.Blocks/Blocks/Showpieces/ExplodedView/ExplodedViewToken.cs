using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ExplodedView"/> (Arc Exploded view, design reference) and its one accent-independent fill;
/// every other color comes from the ArcDesign tokens in the theme. The responsive layout (breakpoints, paddings, title
/// size) and the scene's fit are measured formulas in <see cref="ExplodedViewLayout"/> and <see cref="ExplodedViewGeometry"/>.
/// </summary>
[ControlDesignToken]
internal sealed class ExplodedViewToken : AbstractControlDesignToken
{
    /// <summary>Frame fill: 55% SurfaceMuted over 45% Surface (OKLab).</summary>
    public Color FrameBackground { get; set; }

    /// <summary>The frame's keyboard focus ring: 2px, 2px outside the 34px corner.</summary>
    public Thickness FrameFocusRingMargin { get; set; }

    public CornerRadius FrameFocusRingRadius { get; set; }

    /// <summary>Space between the title and the description.</summary>
    public double IntroGap { get; set; }

    /// <summary>Description leading (16 px type; 14 px when compact).</summary>
    public double DescriptionLeading { get; set; }

    public double CompactDescriptionLeading { get; set; }

    /// <summary>Caption: title size, leading and tracking (display face, −0.03em), body leading and the gap between them.</summary>
    public double CaptionTitleFontSize { get; set; }

    public double CaptionTitleLeading { get; set; }

    public double CaptionTitleTracking { get; set; }

    public double CaptionBodyLeading { get; set; }

    public double CaptionGap { get; set; }

    /// <summary>Stop button: 36 × 44 target, 12px corner, focus ring 2px outside.</summary>
    public CornerRadius StopButtonRadius { get; set; }

    public Thickness StopFocusRingMargin { get; set; }

    public CornerRadius StopFocusRingRadius { get; set; }

    /// <summary>Tick: 20 px for a layer, 8 px at the two ends, 3 px thick, fully round.</summary>
    public double TickLength { get; set; }

    public double EndTickLength { get; set; }

    public double TickThickness { get; set; }

    public CornerRadius TickRadius { get; set; }

    /// <summary>Label dot: 7 px with a 1.5 px inset ring; the focused layer's ring fills it (3.5 px).</summary>
    public double LabelDotSize { get; set; }

    public CornerRadius LabelDotRadius { get; set; }

    public Thickness LabelDotStroke { get; set; }

    public Thickness LabelDotActiveStroke { get; set; }

    /// <summary>Leader: 24 × 1 from 4 px into the dot; the text starts 29 px after the anchor.</summary>
    public double LeaderLength { get; set; }

    public Thickness LeaderMargin { get; set; }

    public Thickness LabelTextMargin { get; set; }

    /// <summary>Label leading: name (14 px) and note (12 px).</summary>
    public double LabelNameLeading { get; set; }

    public double LabelNoteLeading { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        FrameBackground = Oklch.MixOklab(n.SurfaceMuted, n.Surface, 0.45);
        FrameFocusRingMargin = new Thickness(-4);
        FrameFocusRingRadius = new CornerRadius(38);
        IntroGap = 8;
        DescriptionLeading = 22.4;
        CompactDescriptionLeading = 19.6;
        CaptionTitleFontSize = 18;
        CaptionTitleLeading = 19.8;
        CaptionTitleTracking = -0.54;
        CaptionBodyLeading = 19.6;
        CaptionGap = 4;
        StopButtonRadius = new CornerRadius(12);
        StopFocusRingMargin = new Thickness(-4);
        StopFocusRingRadius = new CornerRadius(16);
        TickLength = 20;
        EndTickLength = 8;
        TickThickness = 3;
        TickRadius = new CornerRadius(1.5);
        LabelDotSize = 7;
        LabelDotRadius = new CornerRadius(3.5);
        LabelDotStroke = new Thickness(1.5);
        LabelDotActiveStroke = new Thickness(3.5);
        LeaderLength = 24;
        LeaderMargin = new Thickness(4, 0, 0, 0);
        LabelTextMargin = new Thickness(29, 0, 0, 0);
        LabelNameLeading = 19.6;
        LabelNoteLeading = 16.8;
    }
}
