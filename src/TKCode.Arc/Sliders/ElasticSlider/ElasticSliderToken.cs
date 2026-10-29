using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ElasticSliderToken : AbstractControlDesignToken
{
    public double ThumbDiameter { get; set; }
    public double TrackThickness { get; set; }
    public double TickDiameter { get; set; }

    /// <summary>Ticks above the value: <c>color-mix(in oklab, var(--foreground) 32%, transparent)</c>.</summary>
    public Color TickColor { get; set; }

    /// <summary>Space between the thumb and the tag (Arc 10px; the rail adds 10px after a touch or pen press).</summary>
    public double TagGap { get; set; }
    public double TagMinHeight { get; set; }
    public Thickness TagInset { get; set; }
    public double RowGap { get; set; }
    public Thickness ReadoutMargin { get; set; }
    public Thickness StartMargin { get; set; }
    public Thickness EndMargin { get; set; }
    public double MarksHeight { get; set; }
    public double LabelLineHeight { get; set; }
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ThumbDiameter = 24;
        TrackThickness = 8;
        TickDiameter = 4;
        TickColor = Oklch.WithAlpha(n.Foreground, 0.32);
        TagGap = 10;
        TagMinHeight = 28;
        TagInset = new Thickness(12, 0);
        RowGap = 8;
        ReadoutMargin = new Thickness(16, 0, 0, 0);
        StartMargin = new Thickness(0, 0, 12, 0);
        EndMargin = new Thickness(12, 0, 0, 0);
        MarksHeight = 20;
        LabelLineHeight = 19.6;
        DisabledOpacity = 0.5;
    }
}
