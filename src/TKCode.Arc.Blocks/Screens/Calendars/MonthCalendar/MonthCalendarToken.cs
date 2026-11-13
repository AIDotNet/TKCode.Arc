namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MonthCalendar"/> (Arc Month calendar, design reference: notes §2). The grid's lane rules (19 high
/// chips every 21, the first lane 29 below the cell top) are pure constants in <c>MonthCalendarModel</c> and unit tested;
/// the shell's widths and breakpoints are in <c>ScreenShell</c>. Colors, radii and fonts come from ArcDesign tokens and the
/// calendar tones from <c>MonthCalendarPalette</c>.
/// </summary>
[ControlDesignToken]
internal sealed class MonthCalendarToken : AbstractControlDesignToken
{
    /// <summary>The screen's text: 13 (line height 18.2, letter spacing −0.13).</summary>
    public double TextSize { get; set; }

    public double TextLineHeight { get; set; }

    public double TextTracking { get; set; }

    /// <summary>The day panel's default width (340; the user resizes it between 300 and 460).</summary>
    public double PanelWidth { get; set; }

    /// <summary>The weekday row: 32 high, 12px muted labels inset 12.</summary>
    public double WeekdayRowHeight { get; set; }

    /// <summary>A chip: 19 high, radius 5, padding 0 6, gap 5.</summary>
    public double ChipHeight { get; set; }

    public double ChipRadius { get; set; }

    /// <summary>The day number circle: 24 (32 in the compact month).</summary>
    public double DayNumberSize { get; set; }

    public double CompactDayNumberSize { get; set; }

    /// <summary>The compact month's row height (50) and its load meter ticks (5 × 3, gap 2, up to 4).</summary>
    public double CompactRowHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TextSize = 13;
        TextLineHeight = 18.2;
        TextTracking = -0.13;
        PanelWidth = 340;
        WeekdayRowHeight = 32;
        ChipHeight = 19;
        ChipRadius = 5;
        DayNumberSize = 24;
        CompactDayNumberSize = 32;
        CompactRowHeight = 50;
    }
}
