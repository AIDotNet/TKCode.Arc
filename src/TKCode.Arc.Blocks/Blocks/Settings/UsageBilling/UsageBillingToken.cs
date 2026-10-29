using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the neutral track color of <see cref="UsageBilling"/>.</summary>
[ControlDesignToken]
internal sealed class UsageBillingToken : AbstractControlDesignToken
{
    /// <summary>Gap between the cards.</summary>
    public double GroupGap { get; set; }

    /// <summary>Block width below which the summary stacks the limit under the spend.</summary>
    public double NarrowThreshold { get; set; }

    /// <summary>Space around the summary's figures inside the card.</summary>
    public Thickness SummaryInset { get; set; }

    /// <summary>Gap between the spend and the limit columns.</summary>
    public double SummaryColumnGap { get; set; }

    /// <summary>Gap above the limit column once it sits under the spend.</summary>
    public Thickness StackedLimitMargin { get; set; }

    /// <summary>Gap between the lines of the limit column.</summary>
    public double LimitLineGap { get; set; }

    /// <summary>Height of the limit bar, alert tick included.</summary>
    public double LimitBarHeight { get; set; }

    /// <summary>Thickness of the limit bar's track.</summary>
    public double LimitTrackThickness { get; set; }

    /// <summary>Thickness of a meter bar.</summary>
    public double MeterTrackThickness { get; set; }

    /// <summary>A meter bar under a row without an icon (pulled up toward the row's text).</summary>
    public Thickness MeterBarMargin { get; set; }

    /// <summary>A meter bar under a row with an icon: aligned with the text after the tile.</summary>
    public Thickness MeterBarIndentedMargin { get; set; }

    /// <summary>Gap between the included part and the overage of a bar.</summary>
    public double BarGap { get; set; }

    /// <summary>Height of the daily spend plot.</summary>
    public double ChartPlotHeight { get; set; }

    /// <summary>Space around the chart inside its card.</summary>
    public Thickness ChartInset { get; set; }

    /// <summary>Space around the limit editor (aligned with the setting's text after the tile).</summary>
    public Thickness EditorInset { get; set; }

    /// <summary>Widest the limit amount field grows.</summary>
    public double EditorMaxWidth { get; set; }

    /// <summary>Gap between the amount and the rate of a meter's charge.</summary>
    public double ChargeLineGap { get; set; }

    /// <summary>Bar tracks: 7% foreground into the surface (accent independent).</summary>
    public Color TrackColor { get; set; }

    /// <summary>Line height of secondary text (1.4 × text-sm).</summary>
    public double BodyLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        GroupGap = 16;
        NarrowThreshold = 600;
        SummaryInset = new Thickness(0, 16, 0, 20);
        SummaryColumnGap = 40;
        StackedLimitMargin = new Thickness(0, 24, 0, 0);
        LimitLineGap = 8;
        LimitBarHeight = 16;
        LimitTrackThickness = 8;
        MeterTrackThickness = 6;
        MeterBarMargin = new Thickness(0, -4, 0, 18);
        MeterBarIndentedMargin = new Thickness(50, -4, 0, 18);
        BarGap = 2;
        ChartPlotHeight = 160;
        ChartInset = new Thickness(0, 16, 0, 18);
        EditorInset = new Thickness(50, 0, 0, 20);
        EditorMaxWidth = 320;
        ChargeLineGap = 2;
        TrackColor = Oklch.MixOklab(n.Surface, n.Foreground, 0.07);
        BodyLineHeight = 14 * 1.4;
    }
}
