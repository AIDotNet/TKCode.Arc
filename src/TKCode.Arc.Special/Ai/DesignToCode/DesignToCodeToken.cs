using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="DesignToCode"/>: the card, the two panes and their breakpoints, the pane headers and the
/// footer, the screenshot's frame, region boxes and tags, the scan line, the code's type and gutter, and the drop zone.
/// Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class DesignToCodeToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The card width from which the design and code panes sit side by side (below it they stack).</summary>
    public double SplitWidth { get; set; }

    /// <summary>Below this card width the footer drops the file name and the import button shows only its icon.</summary>
    public double CompactWidth { get; set; }

    /// <summary>Below this card width the meta text and the model name hide (the mark stays).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>The design pane's width when the panes sit side by side.</summary>
    public double DesignPaneWidth { get; set; }

    /// <summary>The height of each pane (side by side, and each of the two when stacked).</summary>
    public double PaneHeight { get; set; }

    /// <summary>The pane headers: their height and padding.</summary>
    public double HeaderHeight { get; set; }

    public Thickness HeaderPadding { get; set; }

    /// <summary>The space between the parts of a header or the footer.</summary>
    public double HeaderGap { get; set; }

    /// <summary>The header glyphs and the model mark.</summary>
    public double GlyphSize { get; set; }

    /// <summary>The footer's padding and minimum height.</summary>
    public Thickness FooterPadding { get; set; }

    public double FooterHeight { get; set; }

    /// <summary>The space around the screenshot inside the design pane.</summary>
    public double ShotPadding { get; set; }

    /// <summary>The screenshot's corner radius.</summary>
    public CornerRadius ShotCornerRadius { get; set; }

    /// <summary>A region box's corner radius.</summary>
    public CornerRadius BoxCornerRadius { get; set; }

    /// <summary>A region box's stroke at rest and when highlighted.</summary>
    public double BoxStrokeThickness { get; set; }

    public double BoxHighlightStrokeThickness { get; set; }

    /// <summary>How strongly a box shows at rest (its stroke's opacity) and the fill of a highlighted box.</summary>
    public double BoxRestOpacity { get; set; }

    public double BoxFillOpacity { get; set; }

    /// <summary>The scan line's thickness and the height of the soft band trailing it.</summary>
    public double ScanLineThickness { get; set; }

    public double ScanBandHeight { get; set; }

    /// <summary>Region tags: height, padding, type and radius.</summary>
    public double TagHeight { get; set; }

    public Thickness TagPadding { get; set; }

    public double TagFontSize { get; set; }

    public CornerRadius TagCornerRadius { get; set; }

    /// <summary>The code: type size, line height and the padding around it.</summary>
    public double CodeFontSize { get; set; }

    public double CodeLineHeight { get; set; }

    public Thickness CodePadding { get; set; }

    /// <summary>The space between the line numbers and the code.</summary>
    public double GutterGap { get; set; }

    /// <summary>How many characters a wrapped code line hangs in from its own indentation.</summary>
    public double HangingIndent { get; set; }

    /// <summary>The opacity of the tint on the lines being written and on the lines of a highlighted region.</summary>
    public double WritingTintOpacity { get; set; }

    public double HighlightTintOpacity { get; set; }

    /// <summary>The drop zone: its inset in the pane, radius, dashes, and the glyph tile at its center.</summary>
    public Thickness DropZoneMargin { get; set; }

    public CornerRadius DropZoneCornerRadius { get; set; }

    public double DropTileSize { get; set; }

    public CornerRadius DropTileCornerRadius { get; set; }

    /// <summary>The small buttons of the panes (remove, copy) and the footer buttons.</summary>
    public double IconButtonSize { get; set; }

    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    /// <summary>The live preview's card: its width, padding and radius.</summary>
    public double PreviewCardWidth { get; set; }

    public Thickness PreviewCardPadding { get; set; }

    public CornerRadius PreviewCardCornerRadius { get; set; }

    /// <summary>The preview's billing switch: its height and inner padding.</summary>
    public double BillingHeight { get; set; }

    public Thickness BillingPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        SplitWidth = 640;
        CompactWidth = 520;
        NarrowWidth = 440;
        DesignPaneWidth = 344;
        PaneHeight = 352;
        HeaderHeight = 44;
        HeaderPadding = new Thickness(14, 0, 8, 0);
        HeaderGap = 8;
        GlyphSize = 16;
        FooterPadding = new Thickness(14, 10, 10, 10);
        FooterHeight = 56;
        ShotPadding = 24;
        ShotCornerRadius = new CornerRadius(12);
        BoxCornerRadius = new CornerRadius(6);
        BoxStrokeThickness = 1;
        BoxHighlightStrokeThickness = 1.5;
        BoxRestOpacity = 0.5;
        BoxFillOpacity = 0.14;
        ScanLineThickness = 1;
        ScanBandHeight = 28;
        TagHeight = 18;
        TagPadding = new Thickness(6, 0);
        TagFontSize = 10.5;
        TagCornerRadius = new CornerRadius(6);
        CodeFontSize = 12.5;
        CodeLineHeight = 20;
        CodePadding = new Thickness(0, 10, 14, 14);
        GutterGap = 14;
        HangingIndent = 2;
        WritingTintOpacity = 0.45;
        HighlightTintOpacity = 0.9;
        DropZoneMargin = new Thickness(14, 0, 14, 14);
        DropZoneCornerRadius = new CornerRadius(14);
        DropTileSize = 40;
        DropTileCornerRadius = new CornerRadius(12);
        IconButtonSize = 28;
        ActionHeight = 36;
        ActionPadding = new Thickness(14, 0);
        PreviewCardWidth = 300;
        PreviewCardPadding = new Thickness(22);
        PreviewCardCornerRadius = new CornerRadius(18);
        BillingHeight = 34;
        BillingPadding = new Thickness(3);
    }
}
