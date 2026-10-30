using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ProviderKeys"/>: the card, its header and footer, a provider row (mark, columns, icon buttons,
/// the reveal ring) and the inline form. Corners come from the ArcDesign RadiusStep tokens (card 20, buttons 10, mark 9). Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ProviderKeysToken : AbstractControlDesignToken
{
    /// <summary>The header's padding (title and description).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>The title: 16/24 in the medium weight.</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    /// <summary>The description under the title: 13/20.</summary>
    public double DescriptionFontSize { get; set; }

    public double DescriptionLineHeight { get; set; }

    /// <summary>A row's padding; narrow cards use the tighter one.</summary>
    public Thickness RowPadding { get; set; }

    public Thickness RowPaddingNarrow { get; set; }

    /// <summary>The provider mark: a tinted rounded square with the glyph or monogram, and the space after it (corners: RadiusStep9).</summary>
    public double MarkSize { get; set; }


    public double MarkGlyphSize { get; set; }

    public double MarkFontSize { get; set; }

    public Thickness MarkMargin { get; set; }

    /// <summary>The provider name: 14/20 medium.</summary>
    public double NameFontSize { get; set; }

    public double NameLineHeight { get; set; }

    /// <summary>The key, the last-used line and the status: 13/20 (the key in the mono face, 12.5).</summary>
    public double KeyFontSize { get; set; }

    public double MetaFontSize { get; set; }

    public double StatusFontSize { get; set; }

    public double TextLineHeight { get; set; }

    /// <summary>The status icon and the gap after it.</summary>
    public double StatusGlyphSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>The wide status column (status over last used): fixed, so swaps never move the actions.</summary>
    public double StatusColumnWidth { get; set; }

    public Thickness StatusColumnMargin { get; set; }

    /// <summary>The wide actions column: four icon buttons, fixed so Add key and Undo sit in the same place.</summary>
    public double ActionsColumnWidth { get; set; }

    /// <summary>An icon button: its size, its glyph and the gap between buttons.</summary>
    public double IconButtonSize { get; set; }

    public double IconGlyphSize { get; set; }


    public double ActionGap { get; set; }

    /// <summary>The reveal countdown: a hairline ring around the reveal button.</summary>
    public double RevealRingSize { get; set; }

    public double RevealRingThickness { get; set; }

    /// <summary>The ring inside Undo that empties over the undo window.</summary>
    public double UndoRingSize { get; set; }

    public double UndoRingThickness { get; set; }

    /// <summary>The status button's inner padding and the space it leaves on its left (so the text lines up with the meta).</summary>
    public Thickness StatusButtonPadding { get; set; }

    public Thickness StatusButtonMargin { get; set; }

    /// <summary>The width under which the card stacks its rows and its form (Arc: 560px).</summary>
    public double NarrowBreakpoint { get; set; }

    /// <summary>The inline form: its margin under the row, the gap between input and buttons, the helper line.</summary>
    public Thickness FormMargin { get; set; }

    public double FormGap { get; set; }

    public double HelperFontSize { get; set; }

    public Thickness HelperMargin { get; set; }

    /// <summary>The footer: its padding, glyph and type.</summary>
    public Thickness FooterPadding { get; set; }

    public double FooterFontSize { get; set; }

    public double FooterGlyphSize { get; set; }

    /// <summary>The height of the placeholder bars while keys load.</summary>
    public double PlaceholderBarHeight { get; set; }


    /// <summary>The icon buttons' hover fill: SurfaceMuted at this opacity over the card.</summary>
    public double HoverOpacity { get; set; }

    /// <summary>The scale of a pressed icon button (full motion only).</summary>
    public double PressScale { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderPadding = new Thickness(20, 18, 20, 12);
        TitleFontSize = 16;
        TitleLineHeight = 24;
        DescriptionFontSize = 13;
        DescriptionLineHeight = 20;
        RowPadding = new Thickness(20, 14);
        RowPaddingNarrow = new Thickness(16, 12);
        MarkSize = 32;
        MarkGlyphSize = 17;
        MarkFontSize = 14;
        MarkMargin = new Thickness(0, 0, 12, 0);
        NameFontSize = 14;
        NameLineHeight = 20;
        KeyFontSize = 12.5;
        MetaFontSize = 12;
        StatusFontSize = 13;
        TextLineHeight = 20;
        StatusGlyphSize = 14;
        StatusGap = 6;
        StatusColumnWidth = 132;
        StatusColumnMargin = new Thickness(12, 0, 12, 0);
        IconButtonSize = 32;
        IconGlyphSize = 16;
        ActionGap = 2;
        ActionsColumnWidth = 4 * IconButtonSize + 3 * ActionGap;
        RevealRingSize = 28;
        RevealRingThickness = 1;
        UndoRingSize = 14;
        UndoRingThickness = 1.5;
        StatusButtonPadding = new Thickness(6, 0);
        // The status column starts 12px after the key column; the button's 6px padding is taken from that gap so the
        // status icon lines up with the last-used line under it.
        StatusButtonMargin = new Thickness(StatusColumnMargin.Left - StatusButtonPadding.Left, 0, 0, 0);
        NarrowBreakpoint = 560;
        FormMargin = new Thickness(0, 12, 0, 2);
        FormGap = 8;
        HelperFontSize = 12;
        HelperMargin = new Thickness(2, 8, 0, 0);
        FooterPadding = new Thickness(20, 12, 20, 14);
        FooterFontSize = 12;
        FooterGlyphSize = 13;
        PlaceholderBarHeight = 10;
        HoverOpacity = 1;
        PressScale = 0.94;
    }
}
