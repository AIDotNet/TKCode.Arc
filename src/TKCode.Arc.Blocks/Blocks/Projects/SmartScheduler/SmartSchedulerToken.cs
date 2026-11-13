using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SmartScheduler"/> (an original TKCode design). Colors, radii, type sizes and fonts come from
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SmartSchedulerToken : AbstractControlDesignToken
{
    /// <summary>The widest the scheduler grows (1040).</summary>
    public double SchedulerMaxWidth { get; set; }

    /// <summary>The card's padding (24) and its compact form (16).</summary>
    public Thickness CardPadding { get; set; }

    public Thickness CompactCardPadding { get; set; }

    /// <summary>At or below this width the form moves above the week (760).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The form column's width (300) and the gap after it (28).</summary>
    public double FormWidth { get; set; }

    public double ColumnGap { get; set; }

    /// <summary>The week's height (396).</summary>
    public double WeekHeight { get; set; }

    /// <summary>Space between sections (20) and between a heading and its content (8).</summary>
    public double SectionGap { get; set; }

    public double HeadingGap { get; set; }

    /// <summary>Padding of a proposal row (12, 10) and the rank disc (24).</summary>
    public Thickness OptionPadding { get; set; }

    public double RankSize { get; set; }

    /// <summary>Padding of a limit chip (10, 3).</summary>
    public Thickness ChipPadding { get; set; }

    /// <summary>Focus rings sit 2 outside the item.</summary>
    public Thickness FocusMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SchedulerMaxWidth = 1040;
        CardPadding = new Thickness(24);
        CompactCardPadding = new Thickness(16);
        CompactWidth = 760;
        FormWidth = 300;
        ColumnGap = 28;
        WeekHeight = 396;
        SectionGap = 20;
        HeadingGap = 8;
        OptionPadding = new Thickness(12, 10);
        RankSize = 24;
        ChipPadding = new Thickness(10, 3);
        FocusMargin = new Thickness(-2);
    }
}
