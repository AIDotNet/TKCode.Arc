using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ProviderStatus"/>: the card, its header, the column widths of wide rows and the two-line layout
/// of narrow ones, the uptime ticks, the incident panel with its rail, and the day tooltip. Colors, fonts and the focus
/// ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ProviderStatusToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The header's padding.</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>The card title: 15/22.</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    /// <summary>The summary and the countdown: 13/18.</summary>
    public double MetaFontSize { get; set; }

    public double MetaLineHeight { get; set; }

    /// <summary>The countdown ring: its size and stroke.</summary>
    public double RingSize { get; set; }

    public double RingThickness { get; set; }

    /// <summary>The square refresh button.</summary>
    public double RefreshSize { get; set; }

    /// <summary>The left and right padding of the rows, the column header and the footnote.</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>A wide row's minimum height and a narrow row's vertical padding.</summary>
    public double RowHeight { get; set; }

    public Thickness NarrowRowPadding { get; set; }

    /// <summary>The 16px mark and the space after it.</summary>
    public double MarkSize { get; set; }

    public double MarkGap { get; set; }

    /// <summary>The monogram's rounded square.</summary>
    public CornerRadius MonogramCornerRadius { get; set; }

    /// <summary>Name 14/20, detail 12/16.</summary>
    public double NameLineHeight { get; set; }

    public double DetailFontSize { get; set; }

    public double DetailLineHeight { get; set; }

    /// <summary>The wide columns: status, latency, percentage, the chevron and the gap between columns.</summary>
    public double StatusColumnWidth { get; set; }

    public double LatencyColumnWidth { get; set; }

    public double UptimeColumnWidth { get; set; }

    public double ChevronColumnWidth { get; set; }

    public double ColumnGap { get; set; }

    /// <summary>The name column's share of the flexible width, and its smallest and largest width.</summary>
    public double NameShare { get; set; }

    public double NameMinWidth { get; set; }

    public double NameMaxWidth { get; set; }

    /// <summary>The space between a narrow row's two lines.</summary>
    public double NarrowLineGap { get; set; }

    /// <summary>The column header row: its height and type size.</summary>
    public double ColumnHeaderHeight { get; set; }

    public double ColumnHeaderFontSize { get; set; }

    /// <summary>The health icon and its gap.</summary>
    public double HealthIconSize { get; set; }

    public double HealthGap { get; set; }

    /// <summary>The uptime bar: tick height (wide and narrow), the gap between ticks, their corners and the extra height of the shown day.</summary>
    public double TickHeight { get; set; }

    public double NarrowTickHeight { get; set; }

    public double TickGap { get; set; }

    public CornerRadius TickCornerRadius { get; set; }

    public double TickLift { get; set; }

    /// <summary>The incident panel: its padding (left aligns with the name) and the rail of updates.</summary>
    public Thickness PanelPadding { get; set; }

    public double RailWidth { get; set; }

    public double DotSize { get; set; }

    public double DotTop { get; set; }

    public double UpdateGap { get; set; }

    /// <summary>Derived places on the rail: the dot's margin, the hairline's margin (first update: from the dot's center), the last hairline's height and the space under an update.</summary>
    public Thickness DotMargin { get; set; }

    public Thickness RailLineMargin { get; set; }

    public Thickness RailFirstLineMargin { get; set; }

    public double RailLastLineHeight { get; set; }

    public Thickness UpdateMargin { get; set; }

    public double UpdateFontSize { get; set; }

    public double UpdateLineHeight { get; set; }

    /// <summary>The day tooltip: its padding, the space between its lines, and its distance from the tick.</summary>
    public Thickness TipPadding { get; set; }

    public double TipGap { get; set; }

    public double TipOffset { get; set; }

    public double TipMaxWidth { get; set; }

    /// <summary>A row's hover fill: SurfaceMuted at this opacity.</summary>
    public double HoverOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        HeaderPadding = new Thickness(20, 16, 14, 14);
        TitleFontSize = 15;
        TitleLineHeight = 22;
        MetaFontSize = 13;
        MetaLineHeight = 18;
        RingSize = 14;
        RingThickness = 2;
        RefreshSize = 32;
        RowPadding = new Thickness(20, 0, 14, 0);
        RowHeight = 60;
        NarrowRowPadding = new Thickness(16, 12, 12, 12);
        MarkSize = 16;
        MarkGap = 10;
        MonogramCornerRadius = new CornerRadius(4);
        NameLineHeight = 20;
        DetailFontSize = 12;
        DetailLineHeight = 16;
        StatusColumnWidth = 112;
        LatencyColumnWidth = 68;
        UptimeColumnWidth = 60;
        ChevronColumnWidth = 16;
        ColumnGap = 12;
        NameShare = 0.42;
        NameMinWidth = 110;
        NameMaxWidth = 240;
        NarrowLineGap = 10;
        ColumnHeaderHeight = 30;
        ColumnHeaderFontSize = 12;
        HealthIconSize = 15;
        HealthGap = 6;
        TickHeight = 24;
        NarrowTickHeight = 22;
        TickGap = 2;
        TickCornerRadius = new CornerRadius(2);
        TickLift = 2;
        PanelPadding = new Thickness(46, 0, 20, 16);
        RailWidth = 18;
        DotSize = 8;
        DotTop = 6;
        UpdateGap = 14;
        DotMargin = new Thickness(0, DotTop, 0, 0);
        RailLineMargin = new Thickness((DotSize - 1) / 2, 0, 0, 0);
        RailFirstLineMargin = new Thickness((DotSize - 1) / 2, DotTop + DotSize / 2, 0, 0);
        RailLastLineHeight = DotTop + DotSize / 2;
        UpdateMargin = new Thickness(0, 0, 0, UpdateGap);
        UpdateFontSize = 13;
        UpdateLineHeight = 19;
        TipPadding = new Thickness(12, 9);
        TipGap = 2;
        TipOffset = 8;
        TipMaxWidth = 240;
        HoverOpacity = 0.6;
    }
}
