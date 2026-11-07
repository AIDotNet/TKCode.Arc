using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="StretchRefresh"/> (Arc Stretch refresh, design reference), as measured: a header padded
/// 16 16 12 20 with a 36 round refresh button, a 72 × 3 line over a 9 px gap and a 16 px status line in a 34 px slot,
/// rows padded 12 20 in a list padded 4 0 12, and the sheet's top shadow (black at 10%, the same in both themes).
/// </summary>
[ControlDesignToken]
internal sealed class StretchRefreshToken : AbstractControlDesignToken
{
    /// <summary>The header's padding (left 20, top 16, right 16, bottom 12).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>The gap between the title block and the refresh button (12).</summary>
    public double HeaderGap { get; set; }

    /// <summary>The refresh button's size (36) and its glyph size (16).</summary>
    public double ButtonSize { get; set; }

    public double ButtonGlyphSize { get; set; }

    /// <summary>The title's size (16), line height (17.6) and letter spacing (−0.48).</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    /// <summary>The subtitle's line height (16.8; size from TypeXS).</summary>
    public double SubtitleLineHeight { get; set; }

    /// <summary>The height of the indicator's slot at the top of the well (34).</summary>
    public double AnchorHeight { get; set; }

    /// <summary>The line's length (72) and thickness (3).</summary>
    public double MeterWidth { get; set; }

    public double MeterThickness { get; set; }

    /// <summary>The gap between the line and the status (9) and between the status mark and its text (6).</summary>
    public double IndicatorGap { get; set; }

    public double StatusGap { get; set; }

    /// <summary>The status line's height (16) and its mark's size (14).</summary>
    public double StatusHeight { get; set; }

    public double MarkSize { get; set; }

    /// <summary>The list's padding (top 4, bottom 12) and each row's padding (12 20).</summary>
    public Thickness ListPadding { get; set; }

    public Thickness RowPadding { get; set; }

    /// <summary>The shadow over the sheet's top edge: a hairline and a soft 24 px shadow 10 px up.</summary>
    public BoxShadows SheetShadow { get; set; }

    /// <summary>The window width at or under which the card drops its frame (475).</summary>
    public double NarrowWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderPadding = new Thickness(20, 16, 16, 12);
        HeaderGap = 12;
        ButtonSize = 36;
        ButtonGlyphSize = 16;
        TitleFontSize = 16;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.48;
        SubtitleLineHeight = 16.8;
        AnchorHeight = 34;
        MeterWidth = 72;
        MeterThickness = 3;
        IndicatorGap = 9;
        StatusGap = 6;
        StatusHeight = 16;
        MarkSize = 14;
        ListPadding = new Thickness(0, 4, 0, 12);
        RowPadding = new Thickness(20, 12);
        SheetShadow = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = -10,
            Blur = 24,
            Spread = -6,
            Color = Color.FromArgb(26, 0, 0, 0)
        });
        NarrowWidth = 475;
    }
}
