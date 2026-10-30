using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="GenerationStats"/>: the card, its head, the four cells with their sparkline, the error row, the
/// breakdown (latency bar and token table), the Details and Retry buttons and the inline line. Colors, fonts and the focus
/// ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class GenerationStatsToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The head row: its height and side padding, the space between its parts and the model's mark.</summary>
    public double HeadHeight { get; set; }

    public Thickness HeadPadding { get; set; }

    public double HeadGap { get; set; }

    public double MarkSize { get; set; }

    /// <summary>The model's name (14px medium) and the status (13px, its icon 14px, 6px apart).</summary>
    public double ModelFontSize { get; set; }

    public double StatusFontSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>A cell: its padding, its label (12px) over its value (18px on a 26px line), 4px apart.</summary>
    public Thickness CellPadding { get; set; }

    public double CellLabelFontSize { get; set; }

    public double CellValueFontSize { get; set; }

    public double CellValueHeight { get; set; }

    public double CellGap { get; set; }

    /// <summary>The speed cell's sparkline.</summary>
    public double SparklineWidth { get; set; }

    public double SparklineHeight { get; set; }

    /// <summary>How far the hairlines between cells stop short of the edges.</summary>
    public double CellLineInset { get; set; }

    /// <summary>The error row's padding and the breakdown's padding.</summary>
    public Thickness ErrorPadding { get; set; }

    public Thickness BreakdownPadding { get; set; }

    /// <summary>The breakdown's type (13px), the space between its sections and the latency bar's height.</summary>
    public double BreakdownFontSize { get; set; }

    public double SectionGap { get; set; }

    public double LatencyBarHeight { get; set; }

    /// <summary>A row of the token table.</summary>
    public double TableRowHeight { get; set; }

    /// <summary>The table's fixed number columns: tokens, rate and cost.</summary>
    public double TableTokensWidth { get; set; }

    public double TableRateWidth { get; set; }

    public double TableCostWidth { get; set; }

    /// <summary>Details, Retry: 28px high, 10px in from the sides; the hover fill is SurfaceMuted at 60%.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionInlinePadding { get; set; }

    public double HoverOpacity { get; set; }

    /// <summary>The inline line: its type, the gap between items, and the panel the breakdown opens in below it.</summary>
    public double InlineFontSize { get; set; }

    public double InlineGap { get; set; }

    public CornerRadius InlinePanelCornerRadius { get; set; }

    public Thickness InlinePanelMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(16);
        HeadHeight = 48;
        HeadPadding = new Thickness(14, 0, 8, 0);
        HeadGap = 10;
        MarkSize = 20;
        ModelFontSize = 14;
        StatusFontSize = 13;
        StatusGap = 6;
        CellPadding = new Thickness(14, 12);
        CellLabelFontSize = 12;
        CellValueFontSize = 18;
        CellValueHeight = 26;
        CellGap = 4;
        SparklineWidth = 56;
        SparklineHeight = 20;
        CellLineInset = 12;
        ErrorPadding = new Thickness(14, 8, 8, 8);
        BreakdownPadding = new Thickness(14, 12, 14, 10);
        BreakdownFontSize = 13;
        SectionGap = 14;
        LatencyBarHeight = 8;
        TableRowHeight = 30;
        TableTokensWidth = 72;
        TableRateWidth = 92;
        TableCostWidth = 76;
        ActionHeight = 28;
        ActionInlinePadding = new Thickness(10, 0);
        HoverOpacity = 0.6;
        InlineFontSize = 13;
        InlineGap = 8;
        InlinePanelCornerRadius = new CornerRadius(14);
        InlinePanelMargin = new Thickness(0, 8, 0, 0);
    }
}
