using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and breakpoints of <see cref="Webhooks"/>, measured on Arc's Webhooks block (design reference only, ADR 0027).
/// Colors, radii and fonts come from ArcDesign tokens in the theme and the code; widths are the control's own width.
/// </summary>
[ControlDesignToken]
internal sealed class WebhooksToken : AbstractControlDesignToken
{
    /// <summary>Side padding of the header, the rows, the detail and the chart (20; 16 when compact).</summary>
    public double InlineGutter { get; set; }

    /// <summary>Side padding when the block is compact (16).</summary>
    public double CompactInlineGutter { get; set; }

    /// <summary>Width of an endpoint row's rate column (72).</summary>
    public double RateColumnWidth { get; set; }

    /// <summary>Width of an endpoint row's badge column (104).</summary>
    public double StateColumnWidth { get; set; }

    /// <summary>Gap between an endpoint row's columns (20).</summary>
    public double RowColumnGap { get; set; }

    /// <summary>Event chips an endpoint row shows before "+N" (3).</summary>
    public int MaxChips { get; set; }

    /// <summary>Where the hover, selection and flash plates sit inside a row (8 at the sides, 2 above and below).</summary>
    public Thickness RowPlateInset { get; set; }

    /// <summary>Height of the traffic chart's bars area (56).</summary>
    public double ChartHeight { get; set; }

    /// <summary>Gap between the chart's bars (5).</summary>
    public double BarGap { get; set; }

    /// <summary>Height of a delivery row (40).</summary>
    public double DeliveryRowHeight { get; set; }

    /// <summary>Width of a delivery row's action column (100).</summary>
    public double ActionColumnWidth { get; set; }

    /// <summary>Height of the payload panel when the inspector sits under the deliveries (256).</summary>
    public double StackedPanelHeight { get; set; }

    /// <summary>Smallest height of the payload panel beside the deliveries (160).</summary>
    public double MinPanelHeight { get; set; }

    /// <summary>At or below this width the deliveries and the inspector stack (880).</summary>
    public double StackedWidth { get; set; }

    /// <summary>At or below this panel width the form's events take two columns (800).</summary>
    public double EventsTwoColumnWidth { get; set; }

    /// <summary>At or below this width endpoint rows keep only the name, the rate and the badge (760).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>At or below this panel width the form takes one column (560).</summary>
    public double FormSingleColumnWidth { get; set; }

    /// <summary>At or below this width the paddings tighten and the toolbar note hides (540).</summary>
    public double CompactWidth { get; set; }

    /// <summary>At or below this width of the delivery list its rows take two lines (460).</summary>
    public double DeliveryTwoLineWidth { get; set; }

    /// <summary>At or below this width of the headers panel names sit above values (420).</summary>
    public double HeadersStackedWidth { get; set; }

    /// <summary>Widest the header's description gets (540).</summary>
    public double DescriptionMaxWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        InlineGutter = 20;
        CompactInlineGutter = 16;
        RateColumnWidth = 72;
        StateColumnWidth = 104;
        RowColumnGap = 20;
        MaxChips = 3;
        RowPlateInset = new Thickness(8, 2);
        ChartHeight = 56;
        BarGap = 5;
        DeliveryRowHeight = 40;
        ActionColumnWidth = 100;
        StackedPanelHeight = 256;
        MinPanelHeight = 160;
        StackedWidth = 880;
        EventsTwoColumnWidth = 800;
        NarrowWidth = 760;
        FormSingleColumnWidth = 560;
        CompactWidth = 540;
        DeliveryTwoLineWidth = 460;
        HeadersStackedWidth = 420;
        DescriptionMaxWidth = 540;
    }
}
