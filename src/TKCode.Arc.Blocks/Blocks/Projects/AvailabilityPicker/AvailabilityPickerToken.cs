using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AvailabilityPicker"/> (Arc Availability picker, design reference): the header, the day strip and
/// its cards, the slots heading and legend, the time rows and the footer, wide and compact (below 520). Colors, radii of the
/// frame, shadows and type come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AvailabilityPickerToken : AbstractControlDesignToken
{
    /// <summary>Below this width the picker is compact (one column of times, stacked footer, smaller day cards).</summary>
    public double CompactWidth { get; set; }

    /// <summary>At or below this width the legend and the cards' "n open" line are hidden.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>The header: 24 24 20 (compact 20 16 16), 16 between the text and the avatars, 4 between title and subtitle.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness CompactHeaderPadding { get; set; }

    public Thickness AttendeesMargin { get; set; }

    public double TitleGap { get; set; }

    /// <summary>The title: 22/24.2, letter spacing −0.66 (−0.03em); the subtitle 14/19.6.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double SubtitleLineHeight { get; set; }

    /// <summary>The day strip: 24 to the sides (compact 16), cards 6 apart (compact 4).</summary>
    public Thickness StripMargin { get; set; }

    public Thickness CompactStripMargin { get; set; }

    public double DayGap { get; set; }

    public double CompactDayGap { get; set; }

    /// <summary>A day card: padding 10 4 (compact 8 2), radius 14 (12), lines 2 apart; its keyboard ring 2px, 2px out.</summary>
    public Thickness DayPadding { get; set; }

    public Thickness CompactDayPadding { get; set; }

    public CornerRadius DayRadius { get; set; }

    public CornerRadius CompactDayRadius { get; set; }

    public CornerRadius DayFocusRadius { get; set; }

    public CornerRadius CompactDayFocusRadius { get; set; }

    public double DayLineGap { get; set; }

    /// <summary>The day number: 22/24.2 at −0.66 (compact 18/19.8 at −0.54); weekday and count 12/16.8 at 72%.</summary>
    public double DayNumberLineHeight { get; set; }

    public double DayNumberLetterSpacing { get; set; }

    public double CompactDayNumberLineHeight { get; set; }

    public double CompactDayNumberLetterSpacing { get; set; }

    public double CaptionLineHeight { get; set; }

    public double DayCaptionOpacity { get; set; }

    /// <summary>The slots heading: 20 under the strip, padding 16 24 0 (compact 16 16 0); 14/15.4 at −0.42.</summary>
    public Thickness HeadMargin { get; set; }

    public Thickness HeadPadding { get; set; }

    public Thickness CompactHeadPadding { get; set; }

    public double HeadingLineHeight { get; set; }

    public double HeadingLetterSpacing { get; set; }

    /// <summary>The legend: a mark, 6, the word, then 8 more before the second mark.</summary>
    public double LegendGap { get; set; }

    /// <summary>At least 12 between the heading and the legend.</summary>
    public Thickness LegendMargin { get; set; }

    public Thickness LegendSecondMargin { get; set; }

    /// <summary>An availability mark: 4 × 14, radius 2, 3 apart.</summary>
    public double MarkWidth { get; set; }

    public double MarkHeight { get; set; }

    public double MarkRadius { get; set; }

    public double MarkGap { get; set; }

    /// <summary>The times: padding 12 24 24 (compact 12 16 16), rows and columns 8 apart.</summary>
    public Thickness StagePadding { get; set; }

    public Thickness CompactStagePadding { get; set; }

    public double SlotGap { get; set; }

    /// <summary>
    /// The horizontal room the times keep outside their box while the stage height springs (the list slides 16 to the
    /// side and its keyboard rings stand 4 out).
    /// </summary>
    public double StageClipOverflow { get; set; }

    /// <summary>A time row: 48 high, padding 0 12 0 14, radius 14, 12 between parts, the time at least 42 wide, check 14.</summary>
    public double SlotHeight { get; set; }

    public Thickness SlotPadding { get; set; }

    public CornerRadius SlotRadius { get; set; }

    public CornerRadius SlotFocusRadius { get; set; }

    public double SlotContentGap { get; set; }

    public double SlotTimeMinWidth { get; set; }

    public double CheckGlyphSize { get; set; }

    public double TimeLineHeight { get; set; }

    /// <summary>A time that cannot be booked is drawn at half opacity.</summary>
    public double DisabledSlotOpacity { get; set; }

    /// <summary>The footer: padding 16 24 (compact 16), 16 between the summary and the button (compact 12, stacked), lines 2 apart.</summary>
    public Thickness FooterPadding { get; set; }

    public Thickness CompactFooterPadding { get; set; }

    public Thickness ConfirmMargin { get; set; }

    public Thickness CompactConfirmMargin { get; set; }

    public double SelectionLineGap { get; set; }

    public double ValueLineHeight { get; set; }

    /// <summary>The booked button's check: 16, 6 before the label.</summary>
    public double BookedGlyphSize { get; set; }

    public double BookedGlyphGap { get; set; }

    /// <summary>Focus ring offset outside the day cards and time rows (2px gap + 2px ring).</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CompactWidth = 520;
        NarrowWidth = 360;
        HeaderPadding = new Thickness(24, 24, 24, 20);
        CompactHeaderPadding = new Thickness(16, 20, 16, 16);
        AttendeesMargin = new Thickness(16, 0, 0, 0);
        TitleGap = 4;
        TitleLineHeight = 24.2;
        TitleLetterSpacing = -0.66;
        SubtitleLineHeight = 19.6;
        StripMargin = new Thickness(24, 0);
        CompactStripMargin = new Thickness(16, 0);
        DayGap = 6;
        CompactDayGap = 4;
        DayPadding = new Thickness(4, 10);
        CompactDayPadding = new Thickness(2, 8);
        DayRadius = new CornerRadius(14);
        CompactDayRadius = new CornerRadius(12);
        DayFocusRadius = new CornerRadius(18);
        CompactDayFocusRadius = new CornerRadius(16);
        DayLineGap = 2;
        DayNumberLineHeight = 24.2;
        DayNumberLetterSpacing = -0.66;
        CompactDayNumberLineHeight = 19.8;
        CompactDayNumberLetterSpacing = -0.54;
        CaptionLineHeight = 16.8;
        DayCaptionOpacity = 0.72;
        HeadMargin = new Thickness(0, 20, 0, 0);
        HeadPadding = new Thickness(24, 16, 24, 0);
        CompactHeadPadding = new Thickness(16, 16, 16, 0);
        HeadingLineHeight = 15.4;
        HeadingLetterSpacing = -0.42;
        LegendGap = 6;
        LegendMargin = new Thickness(12, 0, 0, 0);
        LegendSecondMargin = new Thickness(8, 0, 0, 0);
        MarkWidth = 4;
        MarkHeight = 14;
        MarkRadius = 2;
        MarkGap = 3;
        StagePadding = new Thickness(24, 12, 24, 24);
        CompactStagePadding = new Thickness(16, 12, 16, 16);
        SlotGap = 8;
        StageClipOverflow = 20;
        SlotHeight = 48;
        SlotPadding = new Thickness(14, 0, 12, 0);
        SlotRadius = new CornerRadius(14);
        SlotFocusRadius = new CornerRadius(18);
        SlotContentGap = 12;
        SlotTimeMinWidth = 42;
        CheckGlyphSize = 14;
        TimeLineHeight = 19.6;
        DisabledSlotOpacity = 0.5;
        FooterPadding = new Thickness(24, 16);
        CompactFooterPadding = new Thickness(16);
        ConfirmMargin = new Thickness(16, 0, 0, 0);
        CompactConfirmMargin = new Thickness(0, 12, 0, 0);
        SelectionLineGap = 2;
        ValueLineHeight = 19.6;
        BookedGlyphSize = 16;
        BookedGlyphGap = 6;
        FocusRingMargin = new Thickness(-4);
    }
}
