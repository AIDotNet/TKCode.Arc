using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent inks of <see cref="SlideToConfirm"/> (Arc Slide to confirm, design reference): a
/// 56 px track (64 px from 360 px wide) with the thumb inset 5 px, a 13 px label (15 px on the tall track), the status
/// 14 px left of the shrunken track, a 9 px spinner and 2.25 px glyph strokes, a press reach of 36 px around the thumb
/// and two drop shadows under the thumb. Colors that follow the accent (the fill, the glyph on it) come from ArcDesign
/// tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SlideToConfirmToken : AbstractControlDesignToken
{
    /// <summary>The track's height below <see cref="TallFromWidth"/> (56) and from it on (64).</summary>
    public double TrackHeight { get; set; }

    public double TallTrackHeight { get; set; }

    /// <summary>The width from which the track is tall (360).</summary>
    public double TallFromWidth { get; set; }

    /// <summary>The label's size on the short track (13) and on the tall one (15).</summary>
    public double LabelFontSize { get; set; }

    public double TallLabelFontSize { get; set; }

    /// <summary>The gap between the pending or confirmed text and the shrunken track (14).</summary>
    public double StatusGap { get; set; }

    /// <summary>How far left and right of the thumb's centre a press grabs it (36).</summary>
    public double GrabReach { get; set; }

    /// <summary>The control's opacity while disabled (0.52, as Button).</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>The inner shadow along the track's top edge (black 22% dark, 5% light).</summary>
    public Color InsetShadowColor { get; set; }

    /// <summary>The chevron and spinner ink on the thumb (the page background in dark, the foreground in light).</summary>
    public Color ThumbInk { get; set; }

    /// <summary>The thumb's two drop shadows: 0 1 2 and 0 3 7 (black 45% / 30% dark, 16% / 10% light).</summary>
    public BoxShadows ThumbShadow { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TrackHeight = 56;
        TallTrackHeight = 64;
        TallFromWidth = 360;
        LabelFontSize = 13;
        TallLabelFontSize = 15;
        StatusGap = 14;
        GrabReach = 36;
        DisabledOpacity = 0.52;
        InsetShadowColor = Color.FromArgb(isDarkMode ? (byte)56 : (byte)13, 0, 0, 0);
        ThumbInk = isDarkMode ? ArcNeutralColors.Dark.Background : ArcNeutralColors.Light.Foreground;
        ThumbShadow = isDarkMode
            ? BoxShadows.Parse("0 1 2 0 #73000000, 0 3 7 0 #4D000000")
            : BoxShadows.Parse("0 1 2 0 #29000000, 0 3 7 0 #1A000000");
    }
}
