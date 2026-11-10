using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MetricTicker"/>: the strip's padding, the gap between metrics, the faded edges of the scrolling
/// line, the narrowest column and the value sizes. Colors, radii and type come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class MetricTickerToken : AbstractControlDesignToken
{
    /// <summary>The strip's padding (6 around, 12 on the left).</summary>
    public Thickness StripPadding { get; set; }

    /// <summary>Space between two metrics (24) and between lines of columns (8).</summary>
    public double ItemGap { get; set; }

    public double RowGap { get; set; }

    /// <summary>The faded edges of the scrolling line (28).</summary>
    public double FadeWidth { get; set; }

    /// <summary>The narrowest column in the column layout (150).</summary>
    public double MinColumnWidth { get; set; }

    /// <summary>A cell's padding inside its flash tint (8 / 4).</summary>
    public Thickness CellPadding { get; set; }

    /// <summary>The value in one line (14) and in columns (22).</summary>
    public double ValueFontSize { get; set; }

    public double StackedValueFontSize { get; set; }

    /// <summary>Space between the line and the pause button (8).</summary>
    public double ButtonGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        StripPadding = new Thickness(12, 6, 6, 6);
        ItemGap = 24;
        RowGap = 8;
        FadeWidth = 28;
        MinColumnWidth = 150;
        CellPadding = new Thickness(8, 4);
        ValueFontSize = 14;
        StackedValueFontSize = 22;
        ButtonGap = 8;
    }
}
