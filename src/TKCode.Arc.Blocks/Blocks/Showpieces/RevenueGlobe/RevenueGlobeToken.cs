using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="RevenueGlobe"/> (Arc Revenue globe, design reference). Colors, type and shadows come from the
/// ArcDesign tokens in the theme; the canvas reads its colors from the same tokens.
/// </summary>
[ControlDesignToken]
internal sealed class RevenueGlobeToken : AbstractControlDesignToken
{
    /// <summary>Corner radius of the block (16px).</summary>
    public CornerRadius BlockRadius { get; set; }

    /// <summary>Header inset: 20px left and right, 16px top and bottom.</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Space between the title and the description (2px).</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>Timeline area inset: 4px above, 20px at the sides (16 when narrow), 16px below.</summary>
    public Thickness TimelinePadding { get; set; }

    public Thickness NarrowTimelinePadding { get; set; }

    /// <summary>Space under the control row (12px) and above the hour marks (6px).</summary>
    public Thickness ControlsMargin { get; set; }

    public Thickness HoursMargin { get; set; }

    /// <summary>The control row's buttons: height 32, radius 10, inner padding 12, icon gap 6.</summary>
    public double ButtonHeight { get; set; }

    public CornerRadius ButtonRadius { get; set; }

    public Thickness ButtonPadding { get; set; }

    public double ButtonIconGap { get; set; }

    /// <summary>The Live pill's dot (7px).</summary>
    public double LiveDotSize { get; set; }

    /// <summary>Width of the side panel in the wide layout (320px).</summary>
    public double PanelWidth { get; set; }

    /// <summary>Panel inset: 12px at the sides, 16px top and bottom.</summary>
    public Thickness PanelPadding { get; set; }

    /// <summary>Inset of the figures and the list titles inside the panel (8px at the sides).</summary>
    public Thickness PanelInset { get; set; }

    /// <summary>Space between the figures block and the lists (20px).</summary>
    public double PanelGap { get; set; }

    /// <summary>Space above the two small figures (16px) and between them (16px).</summary>
    public Thickness StatsMargin { get; set; }

    public double StatsGap { get; set; }

    /// <summary>Space under a list title (6px) and between rows (2px).</summary>
    public Thickness GroupHeaderMargin { get; set; }

    public double RowGap { get; set; }

    /// <summary>Rows: radius 8, padding 7 / 8; region rows are 44 tall with a 6px gap above the bar, city rows 34.</summary>
    public CornerRadius RowRadius { get; set; }

    public Thickness RowPadding { get; set; }

    public double RegionRowGap { get; set; }

    public double CityRowHeight { get; set; }

    /// <summary>Region progress bar height (4px).</summary>
    public double ProgressHeight { get; set; }

    /// <summary>Hover card width (216px) and inset (12 / 10).</summary>
    public double TipWidth { get; set; }

    public Thickness TipPadding { get; set; }

    /// <summary>Space above the card's figures (10px) and between its columns (8px).</summary>
    public Thickness TipFiguresMargin { get; set; }

    public double TipColumnGap { get; set; }

    /// <summary>Focus ring: 2px, 2px outside.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockRadius = new CornerRadius(16);
        HeaderPadding = new Thickness(20, 16);
        DescriptionMargin = new Thickness(0, 2, 0, 0);
        TimelinePadding = new Thickness(20, 4, 20, 16);
        NarrowTimelinePadding = new Thickness(16, 4, 16, 16);
        ControlsMargin = new Thickness(0, 0, 0, 12);
        HoursMargin = new Thickness(0, 6, 0, 0);
        ButtonHeight = 32;
        ButtonRadius = new CornerRadius(10);
        ButtonPadding = new Thickness(12, 0);
        ButtonIconGap = 6;
        LiveDotSize = 7;
        PanelWidth = 320;
        PanelPadding = new Thickness(12, 16);
        PanelInset = new Thickness(8, 0);
        PanelGap = 20;
        StatsMargin = new Thickness(0, 16, 0, 0);
        StatsGap = 16;
        GroupHeaderMargin = new Thickness(8, 0, 8, 6);
        RowGap = 2;
        RowRadius = new CornerRadius(8);
        RowPadding = new Thickness(8, 7);
        RegionRowGap = 6;
        CityRowHeight = 34;
        ProgressHeight = 4;
        TipWidth = 216;
        TipPadding = new Thickness(12, 10);
        TipFiguresMargin = new Thickness(0, 10, 0, 0);
        TipColumnGap = 8;
        FocusRingMargin = new Thickness(-4);
    }
}
