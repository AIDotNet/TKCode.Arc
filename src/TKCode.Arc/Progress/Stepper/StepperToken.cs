using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class StepperToken : AbstractControlDesignToken
{
    /// <summary>Error disc: 10% danger into surface; hover 18%; halo: 16% danger (accent independent).</summary>
    public Color ErrorDisc { get; set; }

    public Color ErrorDiscHover { get; set; }
    public Color ErrorHalo { get; set; }

    /// <summary>Space after a horizontal step (before the next column).</summary>
    public Thickness StepGapHorizontal { get; set; }

    /// <summary>Space below a vertical step; compact vertical steppers use a little more.</summary>
    public Thickness StepGapVertical { get; set; }

    public Thickness StepGapVerticalCompact { get; set; }

    /// <summary>Gap between the marker and the text (row gap horizontally, column gap vertically).</summary>
    public Thickness MarkerGapHorizontal { get; set; }

    public Thickness MarkerGapVertical { get; set; }

    /// <summary>Centers the first label line on the marker in vertical steppers.</summary>
    public Thickness TextOffsetVertical { get; set; }

    public double LabelLineHeight { get; set; }
    public double DetailLineHeight { get; set; }
    public Thickness DetailMargin { get; set; }
    public Thickness CaptionMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ErrorDisc = Oklch.MixOklab(n.Surface, n.Danger, 0.10);
        ErrorDiscHover = Oklch.MixOklab(n.Surface, n.Danger, 0.18);
        ErrorHalo = Oklch.WithAlpha(n.Danger, 0.16);
        StepGapHorizontal = new Thickness(0, 0, 16, 0);
        StepGapVertical = new Thickness(0, 0, 0, 20);
        StepGapVerticalCompact = new Thickness(0, 0, 0, 24);
        MarkerGapHorizontal = new Thickness(0, 0, 0, 12);
        MarkerGapVertical = new Thickness(0, 0, 12, 0);
        TextOffsetVertical = new Thickness(0, 4, 0, 0);
        LabelLineHeight = 20;
        DetailLineHeight = 16.8;
        DetailMargin = new Thickness(0, 2, 0, 0);
        CaptionMargin = new Thickness(0, 12, 0, 0);
    }
}
