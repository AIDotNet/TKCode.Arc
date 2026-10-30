using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="DictationButton"/>: the card's padding, the field's type and padding (it grows to six lines,
/// then scrolls), the 36px toolbar, the keyboard hints and the container widths where the toolbar adapts. The muted tone of
/// interim words sits between TextMuted and TextSecondary. Other colors come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class DictationButtonToken : AbstractControlDesignToken
{
    /// <summary>The card's padding.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The field: 16/24 text, its padding and its height at six lines.</summary>
    public double FieldFontSize { get; set; }

    public double FieldLineHeight { get; set; }

    public Thickness FieldPadding { get; set; }

    public int FieldMaxLines { get; set; }

    public double FieldMaxHeight { get; set; }

    public double FieldMinHeight { get; set; }

    /// <summary>The toolbar under the field: 36px high, 4px below it, 8px between its parts.</summary>
    public double ToolbarHeight { get; set; }

    public Thickness ToolbarMargin { get; set; }

    public Thickness DictationMargin { get; set; }

    /// <summary>The toolbar label's 13px text and its 16px glyph.</summary>
    public double LabelFontSize { get; set; }

    public double LabelGlyphSize { get; set; }

    public Thickness LabelMargin { get; set; }

    /// <summary>A keyboard hint: a key cap (11px, 1px border, 6px corners) and its words (12px).</summary>
    public double KeyFontSize { get; set; }

    public Thickness KeyPadding { get; set; }

    public CornerRadius KeyCornerRadius { get; set; }

    public double HintFontSize { get; set; }

    public double HintGap { get; set; }

    /// <summary>Under this card width the keyboard hints hide.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Under this card width error copy shortens.</summary>
    public double CompactWidth { get; set; }

    /// <summary>At or under this card width the toolbar label gives way while recording.</summary>
    public double TinyWidth { get; set; }

    /// <summary>Interim words: halfway between TextMuted and TextSecondary, so they read as unsettled in both themes.</summary>
    public Color InterimForeground { get; set; }

    /// <summary>The card's border under the pointer: BorderStrong mixed 45% into Border.</summary>
    public Color HoverBorder { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardPadding = new Thickness(10);
        FieldFontSize = 16;
        FieldLineHeight = 24;
        FieldPadding = new Thickness(8, 6, 8, 6);
        FieldMaxLines = 6;
        FieldMaxHeight = FieldMaxLines * FieldLineHeight + FieldPadding.Top + FieldPadding.Bottom;
        FieldMinHeight = FieldLineHeight + FieldPadding.Top + FieldPadding.Bottom;
        ToolbarHeight = 36;
        ToolbarMargin = new Thickness(0, 4, 0, 0);
        DictationMargin = new Thickness(8, 0, 8, 0);
        LabelFontSize = 13;
        LabelGlyphSize = 16;
        LabelMargin = new Thickness(6, 0, 0, 0);
        KeyFontSize = 11;
        KeyPadding = new Thickness(5, 1, 5, 1);
        KeyCornerRadius = new CornerRadius(6);
        HintFontSize = 12;
        HintGap = 5;
        NarrowWidth = 460;
        CompactWidth = 400;
        TinyWidth = 360;
        InterimForeground = Oklch.MixOklab(n.TextMuted, n.TextSecondary, 0.5);
        HoverBorder = Oklch.MixOklab(n.Border, n.BorderStrong, 0.45);
    }
}
