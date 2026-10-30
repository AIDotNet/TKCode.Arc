using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TicketRouter"/>: the card's padding and width, the header, the incoming ticket card, the
/// classifier strip and its chips, the connector, the lanes and their rows. Colors, radii and fonts come from the ArcDesign
/// tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TicketRouterToken : AbstractControlDesignToken
{
    /// <summary>The card's padding and widest size (it fills its container up to this width).</summary>
    public Thickness CardPadding { get; set; }

    public double CardMaxWidth { get; set; }

    /// <summary>The space between the header, the stage, the connector and the lanes.</summary>
    public double SectionGap { get; set; }

    /// <summary>The header: its height, the model mark's tile and glyph, the title, the model name and the status.</summary>
    public double HeaderHeight { get; set; }

    public double MarkTileSize { get; set; }

    public double MarkGlyphSize { get; set; }

    public double TitleFontSize { get; set; }

    public double MetaFontSize { get; set; }

    public double StatusDotSize { get; set; }

    /// <summary>The incoming ticket card: a fixed height, its padding, the channel tile and its glyph.</summary>
    public double TicketHeight { get; set; }

    public Thickness TicketPadding { get; set; }

    public double ChannelTileSize { get; set; }

    public double ChannelGlyphSize { get; set; }

    public double SubjectFontSize { get; set; }

    public double PreviewFontSize { get; set; }

    /// <summary>The classifier strip under the ticket: one row wide, two rows stacked below 600px.</summary>
    public double StripHeight { get; set; }

    public double StripStackedHeight { get; set; }

    /// <summary>A label chip: height, side padding, glyph, text and the space between chips.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipGlyphSize { get; set; }

    public double ChipFontSize { get; set; }

    public double ChipGap { get; set; }

    /// <summary>The confidence meter's track.</summary>
    public double MeterWidth { get; set; }

    public double MeterHeight { get; set; }

    /// <summary>The connector between the stage and the lanes: its height, the hairline and the pulse.</summary>
    public double ConnectorHeight { get; set; }

    public double ConnectorStrokeWidth { get; set; }

    public double PulseStrokeWidth { get; set; }

    /// <summary>The lanes: the space between them, their padding, the header and its type.</summary>
    public double LaneGap { get; set; }

    public Thickness LanePadding { get; set; }

    public double LaneHeaderHeight { get; set; }

    public double LaneLabelFontSize { get; set; }

    /// <summary>A row: its height and the space under it, its padding, the avatar and the type of its two lines.</summary>
    public double RowHeight { get; set; }

    public double RowGap { get; set; }

    public Thickness RowPadding { get; set; }

    public double RowAvatarSize { get; set; }

    public double RowSubjectFontSize { get; set; }

    public double RowMetaFontSize { get; set; }

    /// <summary>A row's height plus the space under it, and that space as the row's margin.</summary>
    public double RowExtent { get; set; }

    public Thickness RowMargin { get; set; }

    /// <summary>The stage: the ticket card over the classifier strip (one row wide, two stacked).</summary>
    public double StageHeight { get; set; }

    public double StageStackedHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardPadding = new Thickness(16);
        CardMaxWidth = 840;
        SectionGap = 12;
        HeaderHeight = 32;
        MarkTileSize = 28;
        MarkGlyphSize = 16;
        TitleFontSize = 14;
        MetaFontSize = 12;
        StatusDotSize = 6;
        TicketHeight = 88;
        TicketPadding = new Thickness(12, 10);
        ChannelTileSize = 24;
        ChannelGlyphSize = 14;
        SubjectFontSize = 14;
        PreviewFontSize = 13;
        StripHeight = 32;
        StripStackedHeight = 64;
        ChipHeight = 24;
        ChipPadding = new Thickness(8, 0);
        ChipGlyphSize = 13;
        ChipFontSize = 12;
        ChipGap = 6;
        MeterWidth = 44;
        MeterHeight = 4;
        ConnectorHeight = 24;
        ConnectorStrokeWidth = 1;
        PulseStrokeWidth = 2;
        LaneGap = 8;
        LanePadding = new Thickness(8);
        LaneHeaderHeight = 24;
        LaneLabelFontSize = 12;
        RowHeight = 40;
        RowGap = 6;
        RowPadding = new Thickness(8, 0);
        RowAvatarSize = 22;
        RowSubjectFontSize = 13;
        RowMetaFontSize = 11;
        RowExtent = RowHeight + RowGap;
        RowMargin = new Thickness(0, 0, 0, RowGap);
        StageHeight = TicketHeight + 8 + StripHeight;
        StageStackedHeight = TicketHeight + 8 + StripStackedHeight;
    }
}
