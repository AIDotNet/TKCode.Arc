namespace TKCode.Arc;

/// <summary>
/// The grid geometry of the week calendar, measured at every width (notes §3–§8). The breakpoints read the control's own
/// width. Pure constants and formulas.
/// </summary>
internal static class WeekCalendarMetrics
{
    /// <summary>From this width the sidebar (month and calendars list) shows (Arc: 1100).</summary>
    public const double WideWidth = 1100;

    /// <summary>Below this width the header dates, the title and the New event button tighten (Arc: content 900).</summary>
    public const double TightWidth = 900;

    /// <summary>Below this width the Calendars button shows only its swatches (Arc: content 760).</summary>
    public const double SwatchOnlyWidth = 760;

    /// <summary>Below this width the toolbar takes two rows (Arc: content ≈ 638, a little wider than the compact width).</summary>
    public const double StackedWidth = 680;

    /// <summary>Below this width the calendar shows three days, a day or the agenda (Arc: 640).</summary>
    public const double CompactWidth = 640;

    /// <summary>The time gutter and the header's corner are 60 wide.</summary>
    public const double GutterWidth = 60;

    /// <summary>The header row (weekday over date) is 60 high.</summary>
    public const double HeaderRowHeight = 60;

    /// <summary>The default grid height: 72% of the window's height, between 480 and 760 (Arc's <c>--calendar-height</c>).</summary>
    public static double GridHeightFor(double windowHeight)
    {
        if (!(windowHeight > 0) || !double.IsFinite(windowHeight))
        {
            return 720;
        }
        return Math.Clamp(0.72 * windowHeight, 480, 760);
    }

    /// <summary>The gutter's labels hide when the now pill is closer than this (their boxes would touch: (16.5 + 10.5) / 2).</summary>
    public const double NowLabelClearance = 13.5;

    /// <summary>An event's inner block: 2 in from the left of its slot and 3 from the right, radius 7, a 3px calendar bar.</summary>
    public const double EventInsetLeft = 2;

    public const double EventInsetRight = 3;

    public const double EventRadius = 7;

    public const double EventBarWidth = 3;

    /// <summary>Detail levels by the block's height: up to a 30 minute block is small, up to an hour medium, longer large.</summary>
    public const double SmallMaxHeight = 30;

    public const double MediumMaxHeight = 56;

    /// <summary>A small block shows its start time from this inner width; medium and large show times from 74.</summary>
    public const double SmallTimeMinWidth = 130;

    public const double TimeMinWidth = 74;

    /// <summary>The location line needs 89 of height and the time's width; the avatars 128 and 110.</summary>
    public const double LocationMinHeight = 89;

    public const double AvatarsMinHeight = 128;

    public const double AvatarsMinWidth = 110;

    /// <summary>Text padding of a block: 8 after the bar, 5 above, 5 at the end, 4 below.</summary>
    public const double EventPadLeft = 8;

    public const double EventPadTop = 5;

    public const double EventPadRight = 5;

    public const double EventPadBottom = 4;

    /// <summary>Event avatars: 18 across with a 2px ring, 5 overlap, at most four.</summary>
    public const double EventAvatarSize = 18;

    public const int EventAvatarMax = 4;

    /// <summary>Agenda rows: 64 time column, 4 color bar, 10 gaps, padding 8 10 8 4, radius 14.</summary>
    public const double AgendaTimeWidth = 64;

    public const double AgendaGap = 10;

    public const double AgendaRowRadius = 14;

    public const double AgendaAvatarSize = 22;

    public const int AgendaAvatarMax = 3;
}
