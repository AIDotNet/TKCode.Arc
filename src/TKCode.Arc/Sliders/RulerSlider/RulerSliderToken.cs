using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class RulerSliderToken : AbstractControlDesignToken
{
    public double ViewportHeight { get; set; }

    /// <summary>Small and half ticks: <c>color-mix(in oklab, var(--foreground) 20%, transparent)</c>.</summary>
    public Color TickColor { get; set; }

    /// <summary>Labelled ticks: <c>color-mix(in oklab, var(--foreground) 48%, transparent)</c>.</summary>
    public Color MajorTickColor { get; set; }
    public double SmallTickHeight { get; set; }
    public double HalfTickHeight { get; set; }
    public double MajorTickHeight { get; set; }
    public double TickWidth { get; set; }
    public double NeedleWidth { get; set; }
    public double NeedleHeight { get; set; }
    public double LabelTop { get; set; }
    public double LabelLineHeight { get; set; }
    public double RowGap { get; set; }
    public Thickness ReadoutMargin { get; set; }

    /// <summary>The unit sits .3em after the digits, lifted so its baseline meets theirs.</summary>
    public Thickness UnitMargin { get; set; }
    public Thickness FocusRingMargin { get; set; }
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ViewportHeight = 72;
        TickColor = Oklch.WithAlpha(n.Foreground, 0.20);
        MajorTickColor = Oklch.WithAlpha(n.Foreground, 0.48);
        SmallTickHeight = 12;
        HalfTickHeight = 20;
        MajorTickHeight = 28;
        TickWidth = 1;
        NeedleWidth = 2;
        NeedleHeight = 36;
        LabelTop = 36;
        LabelLineHeight = 16.8;
        RowGap = 4;
        ReadoutMargin = new Thickness(0, 0, 0, 12);
        UnitMargin = new Thickness(8.4, 0, 0, 3);
        FocusRingMargin = new Thickness(-4);
        DisabledOpacity = 0.5;
    }
}
