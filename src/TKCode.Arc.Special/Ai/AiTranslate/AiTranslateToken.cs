using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AiTranslate"/>: the card (radius, padding, widest size), the byline, the paragraph's type and dimming, the 32px actions and the
/// status line. Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AiTranslateToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The card's padding, and the padding below the 470px breakpoint (phones: 16px).</summary>
    public Thickness CardPadding { get; set; }

    public Thickness CardPaddingNarrow { get; set; }

    /// <summary>The card fills its container up to this width.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The space between the byline, the paragraph and the controls.</summary>
    public double SectionGap { get; set; }

    /// <summary>The byline: the gap after the avatar, the name and meta type.</summary>
    public double BylineGap { get; set; }

    public double NameFontSize { get; set; }

    public double MetaFontSize { get; set; }

    /// <summary>The paragraph: 15/24.</summary>
    public double TextFontSize { get; set; }

    public double TextLineHeight { get; set; }

    /// <summary>How far the text on screen dims while a translation replaces it (45%).</summary>
    public double DimOpacity { get; set; }

    /// <summary>The picker, the toggle and Retry: 32px high, 10px in from the sides, 13px text, 16px glyphs.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionInlinePadding { get; set; }

    public double ActionFontSize { get; set; }

    public double ActionGlyphSize { get; set; }

    public double ActionGap { get; set; }

    /// <summary>The status line: 12px text after a 14px mark.</summary>
    public double StatusFontSize { get; set; }

    public double StatusMarkSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>The status line's margin when it drops under the controls (in line with the text).</summary>
    public Thickness StatusNarrowMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(16);
        CardPadding = new Thickness(20, 18, 20, 14);
        CardPaddingNarrow = new Thickness(16, 16, 16, 12);
        CardMaxWidth = 620;
        SectionGap = 14;
        BylineGap = 10;
        NameFontSize = 14;
        MetaFontSize = 12;
        TextFontSize = 15;
        TextLineHeight = 24;
        DimOpacity = 0.45;
        ActionHeight = 32;
        ActionInlinePadding = new Thickness(10, 0);
        ActionFontSize = 13;
        ActionGlyphSize = 16;
        ActionGap = 2;
        StatusFontSize = 12;
        StatusMarkSize = 14;
        StatusGap = 6;
        StatusNarrowMargin = new Thickness(10, 4, 0, 2);
    }
}
