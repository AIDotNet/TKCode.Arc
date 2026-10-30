using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="PiiRedaction"/>: the card's width, radius, padding and gaps (and their tighter values below
/// 400px), the message and reply boxes, the marks under the text, the type cells, the flow strip with its travelling dot,
/// and the footer's buttons. Colors and fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class PiiRedactionToken : AbstractControlDesignToken
{
    /// <summary>The card fills its container up to this width.</summary>
    public double CardMaxWidth { get; set; }

    public CornerRadius CardCornerRadius { get; set; }

    public Thickness CardPadding { get; set; }

    /// <summary>The card's padding below 400px.</summary>
    public Thickness CardPaddingCompact { get; set; }

    /// <summary>The space between the card's sections, and below 400px.</summary>
    public double SectionGap { get; set; }

    public double SectionGapCompact { get; set; }

    /// <summary>The head: the shield glyph, the title and the Simulated tag.</summary>
    public double HeadGlyphSize { get; set; }

    public double TitleFontSize { get; set; }

    public double TagHeight { get; set; }

    public Thickness TagPadding { get; set; }

    public double TagFontSize { get; set; }

    /// <summary>The small caps line over a box ("Message", "Safe prompt") and its row height.</summary>
    public double SectionLabelFontSize { get; set; }

    public double SectionLabelHeight { get; set; }

    /// <summary>The message and reply boxes: radius, padding, the text's size and line height.</summary>
    public CornerRadius BoxCornerRadius { get; set; }

    public Thickness BoxPadding { get; set; }

    public double TextFontSize { get; set; }

    public double TextLineHeight { get; set; }

    /// <summary>The empty message field's minimum height (three lines).</summary>
    public double MessageMinHeight { get; set; }

    /// <summary>A mark under a value or a placeholder: its radius and how far it reaches past the glyphs.</summary>
    public CornerRadius MarkCornerRadius { get; set; }

    public Thickness MarkOutset { get; set; }

    /// <summary>The type cells: height, radius, padding, the gap between them and the dot before the name.</summary>
    public double CellHeight { get; set; }

    public CornerRadius CellCornerRadius { get; set; }

    public Thickness CellPadding { get; set; }

    public double CellGap { get; set; }

    public double CellDotSize { get; set; }

    public double CellFontSize { get; set; }

    /// <summary>The flow strip: its height, the node glyphs, the connector's hairline and the travelling dot.</summary>
    public double FlowHeight { get; set; }

    public double FlowGlyphSize { get; set; }

    public double FlowNodeGap { get; set; }

    public double FlowLineWidth { get; set; }

    public double FlowDotSize { get; set; }

    public double FlowConnectorMinWidth { get; set; }

    /// <summary>The model's mark above the reply.</summary>
    public double ModelMarkSize { get; set; }

    /// <summary>The footer: its height, the status glyph and the fixed width of the primary action and Reset.</summary>
    public double FooterHeight { get; set; }

    public double StatusGlyphSize { get; set; }

    public double PrimaryWidth { get; set; }

    public double ResetWidth { get; set; }

    /// <summary>How much of the type color tints a mark (the alpha of the fill).</summary>
    public double MarkTintOpacity { get; set; }

    /// <summary>How much of the type color tints a cell that found something.</summary>
    public double CellTintOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 640;
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(20);
        CardPaddingCompact = new Thickness(14);
        SectionGap = 14;
        SectionGapCompact = 10;
        HeadGlyphSize = 18;
        TitleFontSize = 15;
        TagHeight = 22;
        TagPadding = new Thickness(8, 0);
        TagFontSize = 11;
        SectionLabelFontSize = 12;
        SectionLabelHeight = 28;
        BoxCornerRadius = new CornerRadius(14);
        BoxPadding = new Thickness(14, 12);
        TextFontSize = 14;
        TextLineHeight = 22;
        MessageMinHeight = 3 * TextLineHeight;
        MarkCornerRadius = new CornerRadius(5);
        MarkOutset = new Thickness(2, 1);
        CellHeight = 52;
        CellCornerRadius = new CornerRadius(12);
        CellPadding = new Thickness(10, 7);
        CellGap = 8;
        CellDotSize = 6;
        CellFontSize = 12;
        FlowHeight = 32;
        FlowGlyphSize = 16;
        FlowNodeGap = 6;
        FlowLineWidth = 1;
        FlowDotSize = 6;
        FlowConnectorMinWidth = 24;
        ModelMarkSize = 16;
        FooterHeight = 36;
        StatusGlyphSize = 16;
        PrimaryWidth = 104;
        ResetWidth = 80;
        MarkTintOpacity = isDarkMode ? 0.28 : 0.18;
        CellTintOpacity = isDarkMode ? 0.16 : 0.1;
    }
}
