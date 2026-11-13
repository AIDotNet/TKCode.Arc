using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="EmojiPicker"/>: the panel's size and padding, the grid's cells and titles, the category strip,
/// the skin tones and the preview line. Colors, radii and fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class EmojiPickerToken : AbstractControlDesignToken
{
    /// <summary>The panel's width (352px: nine 36px cells and the padding) and the grid's height (288px).</summary>
    public double PanelWidth { get; set; }

    public double GridHeight { get; set; }

    /// <summary>The padding inside the panel and the space between its rows.</summary>
    public Thickness PanelPadding { get; set; }

    public double RowSpacing { get; set; }

    /// <summary>An emoji cell (36px) and the emoji in it (22px).</summary>
    public double CellSize { get; set; }

    public double EmojiFontSize { get; set; }

    /// <summary>The color emoji fonts first: a body font that has its own plain glyph for an emoji such as ☺ or ❤ would draw it in one color.</summary>
    public FontFamily EmojiFontFamily { get; set; } = FontFamily.Default;

    /// <summary>A section title's row (28px) and its text (12px).</summary>
    public double HeaderHeight { get; set; }

    public double HeaderFontSize { get; set; }

    /// <summary>A category tab (32px) and its glyph (18px).</summary>
    public double TabSize { get; set; }

    public double TabGlyphSize { get; set; }

    /// <summary>A skin tone cell (32px) and its hand (18px).</summary>
    public double ToneCellSize { get; set; }

    public double ToneFontSize { get; set; }

    /// <summary>The space between the search field and the skin tones.</summary>
    public Thickness TonesMargin { get; set; }

    /// <summary>The preview line: its height (40px), its emoji (24px) and the space after the emoji.</summary>
    public double PreviewHeight { get; set; }

    public double PreviewEmojiSize { get; set; }

    public Thickness PreviewNameMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelWidth = 352;
        GridHeight = 288;
        PanelPadding = new Thickness(8);
        RowSpacing = 6;
        CellSize = 36;
        EmojiFontSize = 22;
        EmojiFontFamily = new FontFamily("Apple Color Emoji, Segoe UI Emoji, Noto Color Emoji, $Default");
        HeaderHeight = 28;
        HeaderFontSize = 12;
        TabSize = 32;
        TabGlyphSize = 18;
        ToneCellSize = 32;
        ToneFontSize = 18;
        TonesMargin = new Thickness(6, 0, 0, 0);
        PreviewHeight = 40;
        PreviewEmojiSize = 24;
        PreviewNameMargin = new Thickness(10, 0, 0, 0);
    }
}
