using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="LiveVisitors"/>: the card (at most 640 wide, 20 padding, 16 below 520), the header, the rolling
/// count, the map's dots and pings, the lists' rows and bars, and the land dots' accent-independent color. Surface,
/// borders, text, the accent and the status colors come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class LiveVisitorsToken : AbstractControlDesignToken
{
    /// <summary>The card: at most 640 wide, radius 20, 20 padding; below 520 the lists stack and the padding drops to 16.</summary>
    public double CardMaxWidth { get; set; }

    public CornerRadius CardCornerRadius { get; set; }

    public Thickness CardPadding { get; set; }

    public Thickness NarrowCardPadding { get; set; }

    /// <summary>The card width under which the two lists stack.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Space between the header, the count, the map and the lists.</summary>
    public double SectionGap { get; set; }

    /// <summary>Space between the two lists side by side, and between them when stacked.</summary>
    public double ColumnGap { get; set; }

    public double StackedGap { get; set; }

    /// <summary>The header: 28 high; the status dot is 8 with a pulse ring up to 2.6 times its size; 12/16 status text.</summary>
    public double HeaderHeight { get; set; }

    public double StatusDotSize { get; set; }

    public double StatusPulseScale { get; set; }

    public double StatusFontSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>The pause button: 28 square, an 14px glyph, radius 8.</summary>
    public double PauseButtonSize { get; set; }

    public double PauseGlyphSize { get; set; }

    public CornerRadius PauseCornerRadius { get; set; }

    /// <summary>The count: a 36px display number on a 40 line, −1 letter spacing, then 14px text 8 to its right.</summary>
    public double CountFontSize { get; set; }

    public double CountLineHeight { get; set; }

    public double CountLabelGap { get; set; }

    /// <summary>The placeholder that holds the number's space while connecting.</summary>
    public double CountPlaceholderWidth { get; set; }

    /// <summary>The map: land dots at 0.28 of the pitch, visit dots 2.5 / event dots 3.5 px (at a 640 card), rings to 12 / 18.</summary>
    public double PingDot { get; set; }

    public double EventPingDot { get; set; }

    public double RingReach { get; set; }

    public double EventRingReach { get; set; }

    public double RingThickness { get; set; }

    public double EventRingThickness { get; set; }

    /// <summary>The city label on the map: 11px text in a pill 6 above the dot.</summary>
    public double MapLabelFontSize { get; set; }

    /// <summary>The land dots: the foreground mixed 18% into the surface (accent-independent).</summary>
    public Color LandColor { get; set; }

    /// <summary>A list's heading: 12/16, 8 above the first row.</summary>
    public double HeadingFontSize { get; set; }

    public double HeadingGap { get; set; }

    /// <summary>A row: 32 high; the bar is 28 high with radius 6; 13px text 10 from the bar's start; the count column 44 wide.</summary>
    public double RowHeight { get; set; }

    public double BarHeight { get; set; }

    public CornerRadius BarCornerRadius { get; set; }

    public double RowFontSize { get; set; }

    public double RowInset { get; set; }

    public double CountColumnWidth { get; set; }

    /// <summary>The country code chip at the start of an event row: 24 × 16, radius 4, 10px semibold mono text.</summary>
    public double CodeChipWidth { get; set; }

    public double CodeChipHeight { get; set; }

    public CornerRadius CodeChipCornerRadius { get; set; }

    public double CodeChipFontSize { get; set; }

    /// <summary>The time column at the end of an event row.</summary>
    public double TimeColumnWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardMaxWidth = 640;
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(20);
        NarrowCardPadding = new Thickness(16);
        NarrowWidth = 520;
        SectionGap = 16;
        ColumnGap = 24;
        StackedGap = 16;
        HeaderHeight = 28;
        StatusDotSize = 8;
        StatusPulseScale = 2.6;
        StatusFontSize = 12;
        StatusGap = 8;
        PauseButtonSize = 28;
        PauseGlyphSize = 14;
        PauseCornerRadius = new CornerRadius(8);
        CountFontSize = 36;
        CountLineHeight = 40;
        CountLabelGap = 8;
        CountPlaceholderWidth = 72;
        PingDot = 2.5;
        EventPingDot = 3.5;
        RingReach = 12;
        EventRingReach = 18;
        RingThickness = 1.25;
        EventRingThickness = 2;
        MapLabelFontSize = 11;
        LandColor = Oklch.MixOklab(n.Surface, n.Foreground, isDarkMode ? 0.22 : 0.16);
        HeadingFontSize = 12;
        HeadingGap = 8;
        RowHeight = 32;
        BarHeight = 28;
        BarCornerRadius = new CornerRadius(6);
        RowFontSize = 13;
        RowInset = 10;
        CountColumnWidth = 44;
        CodeChipWidth = 24;
        CodeChipHeight = 16;
        CodeChipCornerRadius = new CornerRadius(4);
        CodeChipFontSize = 10;
        TimeColumnWidth = 36;
    }
}
