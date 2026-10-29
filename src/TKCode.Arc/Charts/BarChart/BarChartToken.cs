using Avalonia;

namespace TKCode.Arc;

/// <summary>BarChart geometry (Arc: 52px value gutter, 26px axis, bars at most 28px wide, 20px between headline and plot).</summary>
[ControlDesignToken]
internal sealed class BarChartToken : AbstractControlDesignToken
{
    /// <summary>Width of the right-hand value axis (holds "Avg 1,234").</summary>
    public double GutterWidth { get; set; }

    /// <summary>Height of the category axis row under the bars.</summary>
    public double AxisRowHeight { get; set; }

    /// <summary>Widest bar; a bar fills 58% of its slot up to this width.</summary>
    public double MaxBarWidth { get; set; }

    /// <summary>Space between the headline and the plot.</summary>
    public double SectionGap { get; set; }

    /// <summary>Each headline text line keeps its height (1em × body leading) so swaps never move the plot.</summary>
    public double HeadlineLineHeight { get; set; }

    /// <summary>Lifts the unit onto the headline number's baseline.</summary>
    public Thickness UnitMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GutterWidth = 52;
        AxisRowHeight = 26;
        MaxBarWidth = 28;
        SectionGap = 20;
        HeadlineLineHeight = 14 * 1.4;
        UnitMargin = new Thickness(0, 0, 0, 6);
    }
}
