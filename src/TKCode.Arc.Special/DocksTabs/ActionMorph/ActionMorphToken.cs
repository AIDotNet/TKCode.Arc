using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ActionMorph"/> and the few colors it derives from the neutral palette (tints that do not follow
/// the accent). Fills, borders, radii and shadows that follow the theme come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ActionMorphToken : AbstractControlDesignToken
{
    /// <summary>Side of the round button.</summary>
    public double ButtonSize { get; set; }

    /// <summary>Size of the button's plus and check.</summary>
    public double ButtonGlyphSize { get; set; }

    /// <summary>Stroke of the button's plus and check (24-unit viewBox).</summary>
    public double ButtonGlyphStroke { get; set; }

    /// <summary>Width of the open menu (capped by the viewport).</summary>
    public double MenuWidth { get; set; }

    /// <summary>Width of an open composer (capped by the viewport).</summary>
    public double ComposerWidth { get; set; }

    /// <summary>Room the menu and composer keep from each side of the viewport.</summary>
    public double ViewportMargin { get; set; }

    /// <summary>Space between the open surface's edge and its content.</summary>
    public Thickness PanelPadding { get; set; }

    /// <summary>Height of the title row of the menu and of a composer.</summary>
    public double HeaderHeight { get; set; }

    /// <summary>Space before the menu's title.</summary>
    public Thickness MenuTitlePadding { get; set; }

    /// <summary>Gap between the menu's title row and its rows, and between rows.</summary>
    public double RowGap { get; set; }

    /// <summary>Space inside a menu row.</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>Gap between a row's glyph, its text and its key cap.</summary>
    public double RowSpacing { get; set; }

    /// <summary>Box of a row's glyph.</summary>
    public double RowGlyphBox { get; set; }

    /// <summary>Size of a row's glyph.</summary>
    public double RowGlyphSize { get; set; }

    /// <summary>Gap between a row's name and its description.</summary>
    public double RowTextGap { get; set; }

    /// <summary>Line height of a row's name and of the titles.</summary>
    public double LabelLineHeight { get; set; }

    /// <summary>Line height of a row's description and of the small texts.</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Height (and least width) of a key cap.</summary>
    public double KeySize { get; set; }

    /// <summary>Space inside a key cap.</summary>
    public Thickness KeyPadding { get; set; }

    /// <summary>Side of the round back and close buttons.</summary>
    public double IconButtonSize { get; set; }

    /// <summary>Size of the back and close glyphs.</summary>
    public double IconButtonGlyphSize { get; set; }

    /// <summary>Gap between a composer's back button and its title.</summary>
    public double ComposerTitleGap { get; set; }

    /// <summary>Space before a composer's title.</summary>
    public Thickness ComposerTitlePadding { get; set; }

    /// <summary>Gap between the parts of a composer.</summary>
    public double ComposerSpacing { get; set; }

    /// <summary>Space inside the field.</summary>
    public Thickness FieldPadding { get; set; }

    /// <summary>Line height of the field's text.</summary>
    public double FieldLineHeight { get; set; }

    /// <summary>Height of a multiline field.</summary>
    public double MultilineHeight { get; set; }

    /// <summary>Inset of the choices block from the field's edges.</summary>
    public Thickness ChoicesInset { get; set; }

    /// <summary>Gap between the choices heading and the radio group.</summary>
    public double ChoicesLabelGap { get; set; }

    /// <summary>Height of the radio group.</summary>
    public double ChoicesHeight { get; set; }

    /// <summary>Space inside the radio group around its options.</summary>
    public Thickness ChoicesPadding { get; set; }

    /// <summary>Space inside an option.</summary>
    public Thickness ChoicePadding { get; set; }

    /// <summary>Height of the bottom row (message and submit).</summary>
    public double FooterHeight { get; set; }

    /// <summary>Space before the bottom row's message.</summary>
    public Thickness FooterPadding { get; set; }

    /// <summary>Space inside the submit button.</summary>
    public Thickness SubmitPadding { get; set; }

    /// <summary>Size of the small glyphs (submit key, spinner, message icon).</summary>
    public double SmallGlyphSize { get; set; }

    /// <summary>Gap between a small glyph and its text.</summary>
    public double SmallGlyphGap { get; set; }

    /// <summary>Margin of the keyboard focus rings (negative: outside the part).</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>The highlight behind the active menu row.</summary>
    public Color HighlightFill { get; set; }

    /// <summary>The back and close buttons' fill under the pointer.</summary>
    public Color IconButtonHoverFill { get; set; }

    /// <summary>The track of the radio group.</summary>
    public Color ChoicesTrackFill { get; set; }

    /// <summary>The field's fill.</summary>
    public Color FieldFill { get; set; }

    /// <summary>The field's edge while it is invalid.</summary>
    public Color FieldInvalidStroke { get; set; }

    /// <summary>The check drawn on the success fill.</summary>
    public Color SuccessGlyph { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ButtonSize = 56;
        ButtonGlyphSize = 22;
        ButtonGlyphStroke = 2;
        MenuWidth = 256;
        ComposerWidth = 320;
        ViewportMargin = 16;
        PanelPadding = new Thickness(6);
        HeaderHeight = 36;
        MenuTitlePadding = new Thickness(10, 0, 0, 0);
        RowGap = 2;
        RowPadding = new Thickness(10, 9);
        RowSpacing = 12;
        RowGlyphBox = 20;
        RowGlyphSize = 18;
        RowTextGap = 1;
        LabelLineHeight = 19.6;
        SmallLineHeight = 16.8;
        KeySize = 22;
        KeyPadding = new Thickness(6, 0);
        IconButtonSize = 32;
        IconButtonGlyphSize = 16;
        ComposerTitleGap = 4;
        ComposerTitlePadding = new Thickness(2, 0, 0, 0);
        ComposerSpacing = 12;
        FieldPadding = new Thickness(12, 10);
        FieldLineHeight = 22.4;
        MultilineHeight = 88;
        ChoicesInset = new Thickness(2, 0);
        ChoicesLabelGap = 6;
        ChoicesHeight = 36;
        ChoicesPadding = new Thickness(3);
        ChoicePadding = new Thickness(6, 0);
        FooterHeight = 36;
        FooterPadding = new Thickness(4, 0, 0, 0);
        SubmitPadding = new Thickness(14, 0);
        SmallGlyphSize = 14;
        SmallGlyphGap = 6;
        FocusRingMargin = new Thickness(-4);
        HighlightFill = Oklch.WithAlpha(n.Foreground, 0.065);
        IconButtonHoverFill = Oklch.WithAlpha(n.Foreground, 0.07);
        ChoicesTrackFill = Oklch.WithAlpha(n.Foreground, 0.05);
        FieldFill = Oklch.WithAlpha(n.SurfaceMuted, 0.8);
        FieldInvalidStroke = Oklch.WithAlpha(n.Danger, 0.6);
        // White on the light success fill, the page color on the brighter dark one.
        SuccessGlyph = n.Background;
    }
}
