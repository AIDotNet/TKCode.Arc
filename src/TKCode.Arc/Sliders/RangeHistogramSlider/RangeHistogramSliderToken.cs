using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class RangeHistogramSliderToken : AbstractControlDesignToken
{
    public double ChartHeight { get; set; }
    public double ThumbDiameter { get; set; }
    public double TrackThickness { get; set; }
    public double BarGap { get; set; }

    /// <summary>Bars outside the range: <c>color-mix(in oklab, var(--foreground) 11%, transparent)</c>.</summary>
    public Color BarColor { get; set; }

    /// <summary>The hairline over a held thumb: <c>color-mix(in oklab, var(--foreground) 40%, transparent)</c>.</summary>
    public Color GuideColor { get; set; }

    /// <summary>How far the guide rises past the top of the chart.</summary>
    public double GuideOverhang { get; set; }
    public Thickness ReadoutMargin { get; set; }
    public double RowGap { get; set; }
    public double ReadoutGap { get; set; }
    public double LabelLineHeight { get; set; }
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ChartHeight = 76;
        ThumbDiameter = 22;
        TrackThickness = 4;
        BarGap = 2;
        BarColor = Oklch.WithAlpha(n.Foreground, 0.11);
        GuideColor = Oklch.WithAlpha(n.Foreground, 0.40);
        GuideOverhang = 2;
        ReadoutMargin = new Thickness(16, 0, 0, 0);
        RowGap = 12;
        ReadoutGap = 4.2;
        LabelLineHeight = 19.6;
        DisabledOpacity = 0.5;
    }
}
