using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SliderToken : AbstractControlDesignToken
{
    public double ThumbDiameter { get; set; }
    public double TrackThickness { get; set; }
    public double TickDiameter { get; set; }

    /// <summary>Ticks outside the fill: <c>color-mix(in oklab, var(--foreground) 30%, transparent)</c>.</summary>
    public Color TickColor { get; set; }

    /// <summary>Space between the thumb and the bubble (Arc 8px; the rail adds 10px after a touch or pen press).</summary>
    public double BubbleGap { get; set; }
    public double BubbleMinHeight { get; set; }
    public Thickness BubbleInset { get; set; }

    /// <summary>The bubble's edge: <c>color-mix(in oklab, var(--background) 14%, var(--foreground))</c>.</summary>
    public Color BubbleBorderColor { get; set; }
    public Thickness BubbleBorder { get; set; }
    public Thickness ReadoutMargin { get; set; }
    public double ReadoutGap { get; set; }
    public Thickness StartMargin { get; set; }
    public Thickness EndMargin { get; set; }
    public double MarksHeight { get; set; }
    public Thickness MarksMargin { get; set; }
    public double LabelLineHeight { get; set; }
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ThumbDiameter = 22;
        TrackThickness = 6;
        TickDiameter = 4;
        TickColor = Oklch.WithAlpha(n.Foreground, 0.30);
        BubbleGap = 8;
        BubbleMinHeight = 28;
        BubbleInset = new Thickness(12, 0);
        BubbleBorderColor = Oklch.MixOklab(n.Foreground, n.Background, 0.14);
        BubbleBorder = new Thickness(1);
        ReadoutMargin = new Thickness(16, 0, 0, 0);
        ReadoutGap = 4;
        StartMargin = new Thickness(0, 0, 12, 0);
        EndMargin = new Thickness(12, 0, 0, 0);
        MarksHeight = 20;
        MarksMargin = new Thickness(0, -4, 0, 0);
        LabelLineHeight = 19.6;
        DisabledOpacity = 0.5;
    }
}
