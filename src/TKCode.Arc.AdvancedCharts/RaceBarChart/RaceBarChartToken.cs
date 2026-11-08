namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class RaceBarChartToken : AbstractControlDesignToken
{
    /// <summary>Height of one bar.</summary>
    public double BarHeight { get; set; }

    /// <summary>Space between two bars.</summary>
    public double BarGap { get; set; }

    /// <summary>Radius of a bar's right end.</summary>
    public double BarRadius { get; set; }

    /// <summary>Widest column for the names left of the bars.</summary>
    public double NameColumnWidth { get; set; }

    /// <summary>Height of the value axis band above the bars.</summary>
    public double AxisHeight { get; set; }

    /// <summary>Approximate pixels per axis tick.</summary>
    public double TickSpacing { get; set; }

    /// <summary>Width below which names move inside the bars.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Height of the timeline's touch target.</summary>
    public double TimelineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarHeight = 28;
        BarGap = 6;
        BarRadius = 6;
        NameColumnWidth = 132;
        AxisHeight = 26;
        TickSpacing = 84;
        NarrowWidth = 400;
        TimelineHeight = 36;
    }
}
