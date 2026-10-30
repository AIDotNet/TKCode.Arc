using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and type sizes of <see cref="UpgradePrompt"/> (Arc Upgrade prompt, design reference). Surface, borders, text,
/// accent and status colors, the card radius and the font families come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class UpgradePromptToken : AbstractControlDesignToken
{
    /// <summary>The card: fluid up to 440 wide, 24 padding (20 below 420), 20 between sections.</summary>
    public double CardMaxWidth { get; set; }

    public Thickness CardPadding { get; set; }

    public Thickness NarrowCardPadding { get; set; }

    /// <summary>The card width below which the padding drops to <see cref="NarrowCardPadding"/>.</summary>
    public double NarrowWidth { get; set; }

    public double SectionGap { get; set; }

    /// <summary>The header: a 36px tinted tile with an 18px glyph, 14 before the title.</summary>
    public double BadgeSize { get; set; }

    public double BadgeGlyphSize { get; set; }

    public double HeaderGap { get; set; }

    /// <summary>The title: 18/24 semibold. The description: 14/20 on two reserved lines.</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>Small text: 12/16 (meter status, notes, headings over lists).</summary>
    public double SmallFontSize { get; set; }

    public double SmallLineHeight { get; set; }

    /// <summary>The meter: an 8px bar, 8 between its label row, the bar and the status row; status icons are 14.</summary>
    public double MeterHeight { get; set; }

    public double MeterGap { get; set; }

    public double StatusIconSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>The price: 32px display digits on a 40px line, the period 6 after them, the toggle 12 away.</summary>
    public double PriceFontSize { get; set; }

    public double PriceHeight { get; set; }

    public double PeriodGap { get; set; }

    public double PriceToggleGap { get; set; }

    /// <summary>Lists of unlocks and limits: rows 28 high, 10 apart; marks 16 in a 28px tile with 12 after it.</summary>
    public double RowHeight { get; set; }

    public double RowGap { get; set; }

    public double MarkTileSize { get; set; }

    public double MarkSize { get; set; }

    public double MarkGap { get; set; }

    /// <summary>The footer: buttons 10 apart.</summary>
    public double ButtonGap { get; set; }

    /// <summary>The tile behind a mark: the muted surface mixed toward the background.</summary>
    public Color MarkTileFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardMaxWidth = 440;
        CardPadding = new Thickness(24);
        NarrowCardPadding = new Thickness(20);
        NarrowWidth = 420;
        SectionGap = 20;
        BadgeSize = 36;
        BadgeGlyphSize = 18;
        HeaderGap = 14;
        TitleFontSize = 18;
        TitleLineHeight = 24;
        TitleLetterSpacing = -0.27;
        BodyLineHeight = 20;
        SmallFontSize = 12;
        SmallLineHeight = 16;
        MeterHeight = 8;
        MeterGap = 8;
        StatusIconSize = 14;
        StatusGap = 6;
        PriceFontSize = 32;
        PriceHeight = 40;
        PeriodGap = 6;
        PriceToggleGap = 12;
        RowHeight = 28;
        RowGap = 10;
        MarkTileSize = 28;
        MarkSize = 16;
        MarkGap = 12;
        ButtonGap = 10;
        MarkTileFill = Oklch.MixOklab(n.SurfaceMuted, n.Surface, 0.25);
    }
}
