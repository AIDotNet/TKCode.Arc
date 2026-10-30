using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="LlmCostCalculator"/>: the panel's cap and padding, the type of the heading and the large total,
/// the controls' spacing, the model rows (44px, 58px with the bar on its own line), the mark, the bar and the cost column,
/// and the breakdown. Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class LlmCostCalculatorToken : AbstractControlDesignToken
{
    /// <summary>The widest the panel grows.</summary>
    public double PanelMaxWidth { get; set; }

    public Thickness PanelPadding { get; set; }

    /// <summary>The space between the panel's sections.</summary>
    public double SectionGap { get; set; }

    /// <summary>The heading (16/22) and the line under it (13/18).</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double DescriptionFontSize { get; set; }

    public double DescriptionLineHeight { get; set; }

    /// <summary>The large total and its unit.</summary>
    public double TotalFontSize { get; set; }

    public double TotalUnitFontSize { get; set; }

    /// <summary>The space between the sliders, side by side or stacked.</summary>
    public double SliderGap { get; set; }

    /// <summary>A model row: 44px, 58px when the bar takes its own line.</summary>
    public double RowHeight { get; set; }

    public double CompactRowHeight { get; set; }

    public Thickness RowPadding { get; set; }

    public CornerRadius RowCornerRadius { get; set; }

    /// <summary>The space between a row's mark, name, bar and cost.</summary>
    public double RowGap { get; set; }

    /// <summary>The selected row's accent bar at its left edge.</summary>
    public double AccentBarWidth { get; set; }

    public Thickness AccentBarMargin { get; set; }

    public double MarkSize { get; set; }

    /// <summary>The monogram of a model without a mark: its square's radius and letter size.</summary>
    public CornerRadius MonogramCornerRadius { get; set; }

    public double MonogramFontSize { get; set; }

    public double NameFontSize { get; set; }

    public double ProviderFontSize { get; set; }

    /// <summary>The cost bar's thickness.</summary>
    public double BarHeight { get; set; }

    /// <summary>The cost column's width, right-aligned, so a changing figure never moves the bar.</summary>
    public double CostWidth { get; set; }

    public double CostFontSize { get; set; }

    /// <summary>The breakdown: label and figure sizes and the space between cells.</summary>
    public double FigureLabelFontSize { get; set; }

    public double FigureFontSize { get; set; }

    public double FigureGap { get; set; }

    /// <summary>The price line and the fine print.</summary>
    public double FinePrintFontSize { get; set; }

    public double FinePrintLineHeight { get; set; }

    /// <summary>How many placeholder rows the list area holds while there are no models to size it.</summary>
    public int PlaceholderRows { get; set; }

    /// <summary>The loading pulse: the lowest opacity and the period in seconds.</summary>
    public double PulseLowOpacity { get; set; }

    public double PulsePeriod { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelMaxWidth = 720;
        PanelPadding = new Thickness(20);
        SectionGap = 18;
        TitleFontSize = 16;
        TitleLineHeight = 22;
        DescriptionFontSize = 13;
        DescriptionLineHeight = 18;
        TotalFontSize = 28;
        TotalUnitFontSize = 14;
        SliderGap = 16;
        RowHeight = 44;
        CompactRowHeight = 58;
        RowPadding = new Thickness(12, 0, 12, 0);
        RowCornerRadius = new CornerRadius(10);
        RowGap = 10;
        AccentBarWidth = 3;
        AccentBarMargin = new Thickness(0, 10, 0, 10);
        MarkSize = 16;
        MonogramCornerRadius = new CornerRadius(4);
        MonogramFontSize = 10;
        NameFontSize = 13;
        ProviderFontSize = 12;
        BarHeight = 6;
        CostWidth = 84;
        CostFontSize = 13;
        FigureLabelFontSize = 12;
        FigureFontSize = 15;
        FigureGap = 12;
        FinePrintFontSize = 12;
        FinePrintLineHeight = 17;
        PlaceholderRows = 8;
        PulseLowOpacity = 0.45;
        PulsePeriod = 1.4;
    }
}
