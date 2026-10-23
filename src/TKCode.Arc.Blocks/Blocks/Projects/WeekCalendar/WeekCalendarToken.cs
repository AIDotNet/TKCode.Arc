using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="WeekCalendar"/>'s toolbar, frame, sidebar and calendars list (Arc Week calendar, design reference
/// only, ADR 0017; notes §3). The grid's own geometry (hour height, gutter, header row, all-day lanes, event boxes) lives
/// in <c>WeekCalendarMetrics</c> and the layout engines, which are unit tested without a theme. Colors, radii, shadows and
/// fonts come from ArcDesign tokens in the theme; the calendar colors from <c>WeekCalendarPalette</c>.
/// </summary>
[ControlDesignToken]
internal sealed class WeekCalendarToken : AbstractControlDesignToken
{
    /// <summary>Space between toolbar controls (12) and between its two rows on a narrow calendar (10).</summary>
    public double ToolbarGap { get; set; }

    public double ToolbarRowGap { get; set; }

    /// <summary>Space between the toolbar and the frame (14).</summary>
    public double FrameGap { get; set; }

    /// <summary>Toolbar controls are 36 high, 40 when the toolbar takes two rows.</summary>
    public double ToolbarItemHeight { get; set; }

    public double StackedItemHeight { get; set; }

    /// <summary>Padding of the Today and Calendars buttons (14 at the sides; 12 and 11 on two rows).</summary>
    public Thickness PillPadding { get; set; }

    public Thickness StackedPillPadding { get; set; }

    /// <summary>The previous / next buttons: 36 square (40 on two rows), chevron 18.</summary>
    public double StepButtonSize { get; set; }

    public double StackedStepButtonSize { get; set; }

    public double StepGlyphSize { get; set; }

    /// <summary>The range title: 18/19.8 (16/17.6 below the tight width), letter spacing −0.03em.</summary>
    public double TitleFontSize { get; set; }

    public double TightTitleFontSize { get; set; }

    public double TitleLetterSpacing { get; set; }

    /// <summary>Gap between the New event button's plus and its label (8), plus 16.</summary>
    public double ButtonContentGap { get; set; }

    public double PlusGlyphSize { get; set; }

    /// <summary>The sidebar: 248 wide with its right border, padding 16 / 14, 20 between the month and the calendars.</summary>
    public double SidebarWidth { get; set; }

    public Thickness SidebarPadding { get; set; }

    public double SidebarGap { get; set; }

    /// <summary>The calendars heading: 12/16.8, 8 to the side; 8 above the list.</summary>
    public Thickness CalendarsHeadingMargin { get; set; }

    /// <summary>A calendar toggle row: 34 high, 8 to the sides, radius 10, 10 between box and name, rows 2 apart.</summary>
    public double CalendarRowHeight { get; set; }

    public Thickness CalendarRowPadding { get; set; }

    public CornerRadius CalendarRowRadius { get; set; }

    public double CalendarRowGap { get; set; }

    public double CalendarListGap { get; set; }

    /// <summary>The toggle's box: 16 square, radius 5, check 12 at stroke 2.5.</summary>
    public double CheckboxSize { get; set; }

    public CornerRadius CheckboxRadius { get; set; }

    public double CheckGlyphSize { get; set; }

    /// <summary>The keyboard ring of a toggle row: 2px, 2 outside the row (radius 14).</summary>
    public CornerRadius CalendarRowFocusRadius { get; set; }

    /// <summary>The Calendars popover: 240 wide (the list inside its 16px padding), 8 below the button.</summary>
    public double PopoverListWidth { get; set; }

    public double PopoverOffset { get; set; }

    /// <summary>The month's header: 28 high with 8 before the title; 28 square buttons with radius 10, chevrons 16.</summary>
    public double MiniHeaderHeight { get; set; }

    public double MiniButtonSize { get; set; }

    public CornerRadius MiniButtonRadius { get; set; }

    public double MiniGlyphSize { get; set; }

    public double MiniRowGap { get; set; }

    /// <summary>The all-day row's expand button: 20 square with a 14px chevron.</summary>
    public double ExpandButtonSize { get; set; }

    public double ExpandGlyphSize { get; set; }

    // Editing (notes B §3.5–3.7) ----------------------------------------------------------------------------------------

    /// <summary>The quick create popover is 320 wide, the details 340; radius 20, padding 14 14 14 16, rows 14 apart.</summary>
    public double QuickCreateWidth { get; set; }

    public double DetailsWidth { get; set; }

    public CornerRadius EditorRadius { get; set; }

    public Thickness EditorPadding { get; set; }

    public double EditorRowGap { get; set; }

    /// <summary>The title boxes: 38 high, radius 12, 8 to the sides (their text lines up with the rows under them).</summary>
    public double TitleBoxHeight { get; set; }

    public CornerRadius TitleBoxRadius { get; set; }

    public Thickness TitleBoxPadding { get; set; }

    /// <summary>Row icons are 16 with 10 after them, so the rows' text starts 26 in.</summary>
    public double EditorIconSize { get; set; }

    public double EditorIconGap { get; set; }

    public Thickness EditorIndent { get; set; }

    /// <summary>The date and time fields of the details: 32 high, radius 10; the start and end fields 6 apart.</summary>
    public double FieldHeight { get; set; }

    public CornerRadius FieldRadius { get; set; }

    public double FieldGap { get; set; }

    /// <summary>The calendar choice: pills 30 high, padding 0 11 0 9, an 8px dot 8 before the name, 6 apart.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public CornerRadius ChipRadius { get; set; }

    public double ChipGap { get; set; }

    public double ChipDotSize { get; set; }

    /// <summary>The details' close button (36, an 18px ×) and its calendar bar (4 × 22, radius 2).</summary>
    public double CloseButtonSize { get; set; }

    public double CloseGlyphSize { get; set; }

    public double CalendarBarWidth { get; set; }

    public double CalendarBarHeight { get; set; }

    /// <summary>The undo toast: 42 high, padding 6 6 6 16; Undo 30 high with 12 at the sides; the × 28 square, radius 10.</summary>
    public double ToastHeight { get; set; }

    public Thickness ToastPadding { get; set; }

    public double ToastActionHeight { get; set; }

    public Thickness ToastActionPadding { get; set; }

    public double ToastDismissSize { get; set; }

    public CornerRadius ToastDismissRadius { get; set; }

    /// <summary>The toast's colors on its inverted pill: the Undo fill (the text color at 16%) and the × (at 70%).</summary>
    public Color ToastActionFill { get; set; }

    public Color ToastDismissForeground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        QuickCreateWidth = 320;
        DetailsWidth = 340;
        EditorRadius = new CornerRadius(20);
        EditorPadding = new Thickness(16, 14, 14, 14);
        EditorRowGap = 14;
        TitleBoxHeight = 38;
        TitleBoxRadius = new CornerRadius(12);
        TitleBoxPadding = new Thickness(8, 0);
        EditorIconSize = 16;
        EditorIconGap = 10;
        EditorIndent = new Thickness(26, 0, 0, 0);
        FieldHeight = 32;
        FieldRadius = new CornerRadius(10);
        FieldGap = 6;
        ChipHeight = 30;
        ChipPadding = new Thickness(9, 0, 11, 0);
        ChipRadius = new CornerRadius(15);
        ChipGap = 6;
        ChipDotSize = 8;
        CloseButtonSize = 36;
        CloseGlyphSize = 18;
        CalendarBarWidth = 4;
        CalendarBarHeight = 22;
        ToastHeight = 42;
        ToastPadding = new Thickness(16, 6, 6, 6);
        ToastActionHeight = 30;
        ToastActionPadding = new Thickness(12, 0);
        ToastDismissSize = 28;
        ToastDismissRadius = new CornerRadius(10);
        ToastActionFill = Oklch.WithAlpha(neutrals.Background, 0.16);
        ToastDismissForeground = Oklch.WithAlpha(neutrals.Background, 0.70);
        ToolbarGap = 12;
        ToolbarRowGap = 10;
        FrameGap = 14;
        ToolbarItemHeight = 36;
        StackedItemHeight = 40;
        PillPadding = new Thickness(14, 0);
        StackedPillPadding = new Thickness(12, 0);
        StepButtonSize = 36;
        StackedStepButtonSize = 40;
        StepGlyphSize = 18;
        TitleFontSize = 18;
        TightTitleFontSize = 16;
        TitleLetterSpacing = -0.54;
        ButtonContentGap = 8;
        PlusGlyphSize = 16;
        SidebarWidth = 248;
        SidebarPadding = new Thickness(14, 16);
        SidebarGap = 20;
        CalendarsHeadingMargin = new Thickness(8, 0, 8, 8);
        CalendarRowHeight = 34;
        CalendarRowPadding = new Thickness(8, 0);
        CalendarRowRadius = new CornerRadius(10);
        CalendarRowGap = 10;
        CalendarListGap = 2;
        CheckboxSize = 16;
        CheckboxRadius = new CornerRadius(5);
        CheckGlyphSize = 12;
        CalendarRowFocusRadius = new CornerRadius(14);
        PopoverListWidth = 208;
        PopoverOffset = 8;
        MiniHeaderHeight = 28;
        MiniButtonSize = 28;
        MiniButtonRadius = new CornerRadius(10);
        MiniGlyphSize = 16;
        MiniRowGap = 6;
        ExpandButtonSize = 20;
        ExpandGlyphSize = 14;
    }
}
