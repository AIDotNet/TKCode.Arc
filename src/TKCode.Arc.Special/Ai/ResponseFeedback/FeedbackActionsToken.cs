using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="FeedbackActions"/>: the thumbs and their glyphs, the panel under the row (its space, padding,
/// radius and inner gaps), the reason chips (28px, 32px on coarse pointers), the close button, the footer's quiet text and
/// the thank-you badge. Colors come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class FeedbackActionsToken : AbstractControlDesignToken
{
    /// <summary>The glyph inside the 36px row buttons.</summary>
    public double GlyphSize { get; set; }

    /// <summary>The space between the row's buttons.</summary>
    public double ActionGap { get; set; }

    /// <summary>The space between the thumbs and the row's error line.</summary>
    public Thickness RowErrorMargin { get; set; }

    /// <summary>The panel: its space above, padding, radius and the space between its rows.</summary>
    public Thickness PanelMargin { get; set; }

    public Thickness PanelPadding { get; set; }

    public CornerRadius PanelCornerRadius { get; set; }

    public double PanelGap { get; set; }

    /// <summary>
    /// The note's smallest height: two lines of text (2 × 19.6) plus the field's 12px padding and 1px border on each side,
    /// rounded up, instead of Arc's 110px textarea default.
    /// </summary>
    public double NoteMinHeight { get; set; }

    /// <summary>A reason chip: its height (and on coarse pointers), padding, type and the gap between chips.</summary>
    public double ChipHeight { get; set; }

    public double CoarseChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipFontSize { get; set; }

    public double ChipGap { get; set; }

    /// <summary>The panel's close button and its glyph.</summary>
    public double CloseSize { get; set; }

    public double CloseGlyphSize { get; set; }

    /// <summary>The quiet line in the footer, the error line and the trailing content.</summary>
    public double QuietFontSize { get; set; }

    public double StatusGlyphSize { get; set; }

    /// <summary>The thank-you state: the round badge with its tick.</summary>
    public double ThanksBadgeSize { get; set; }

    public double ThanksGlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GlyphSize = 16;
        ActionGap = 2;
        RowErrorMargin = new Thickness(8, 0, 0, 0);
        PanelMargin = new Thickness(0, 8, 0, 0);
        PanelPadding = new Thickness(14, 12, 14, 12);
        PanelCornerRadius = new CornerRadius(16);
        PanelGap = 10;
        NoteMinHeight = 66;
        ChipHeight = 28;
        CoarseChipHeight = 32;
        ChipPadding = new Thickness(12, 0);
        ChipFontSize = 13;
        ChipGap = 6;
        CloseSize = 28;
        CloseGlyphSize = 14;
        QuietFontSize = 12;
        StatusGlyphSize = 14;
        ThanksBadgeSize = 22;
        ThanksGlyphSize = 14;
    }
}
