using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MeetingSummary"/>: the card's width and padding, the header's mark tile and type, the sections
/// and their lines, the action rows (checkbox hit area, owner avatar, send buttons), the timestamps, the quote card and
/// the two breakpoints. Colors, fonts, radii and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class MeetingSummaryToken : AbstractControlDesignToken
{
    /// <summary>The card's widest width; it fills its container up to this.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The narrowest width the card is designed for.</summary>
    public double CardMinWidth { get; set; }

    /// <summary>The header's padding.</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>The body's padding (the notes, the summarizing pass, error and empty).</summary>
    public Thickness BodyPadding { get; set; }

    /// <summary>The footer's padding.</summary>
    public Thickness FooterPadding { get; set; }

    /// <summary>The platform mark's tile and the glyph inside it.</summary>
    public double MarkTileSize { get; set; }

    public double MarkGlyphSize { get; set; }

    /// <summary>The space between the mark tile, the title block and the attendees.</summary>
    public Thickness TitleMargin { get; set; }

    /// <summary>The title: 15/22.</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    /// <summary>The meta line under the title and under action items: 12/16.</summary>
    public double MetaFontSize { get; set; }

    /// <summary>Where the attendees go below the narrow breakpoint (under the date).</summary>
    public Thickness NarrowAttendeesMargin { get; set; }

    /// <summary>The space between sections and between a heading and its lines.</summary>
    public double SectionGap { get; set; }

    public double HeadingGap { get; set; }

    /// <summary>A section heading: 12px medium, muted.</summary>
    public double HeadingFontSize { get; set; }

    /// <summary>A note line: 14/21.</summary>
    public double LineFontSize { get; set; }

    public double LineLineHeight { get; set; }

    /// <summary>A line's highlight reaches past the text by its padding (the negative margin keeps the text aligned).</summary>
    public Thickness RowPadding { get; set; }

    public Thickness RowMargin { get; set; }

    /// <summary>The dot in front of a Summary or Decisions line.</summary>
    public double BulletSize { get; set; }

    public Thickness BulletMargin { get; set; }

    /// <summary>The checkbox: a 16px square in a 32px hit area, placed so the square lines up with the dots and the first text line.</summary>
    public double CheckHitSize { get; set; }

    public double CheckSize { get; set; }

    public double CheckRingSize { get; set; }

    public Thickness CheckMargin { get; set; }

    /// <summary>The owner's avatar under an action item.</summary>
    public double OwnerAvatarSize { get; set; }

    public double OwnerAvatarFontSize { get; set; }

    /// <summary>The timestamp button: 22px high, mono 11.5px.</summary>
    public double StampHeight { get; set; }

    public double StampFontSize { get; set; }

    public Thickness StampPadding { get; set; }

    public Thickness StampMargin { get; set; }

    /// <summary>A send button: 26px high, square when idle.</summary>
    public double SendHeight { get; set; }

    public double SendFontSize { get; set; }

    /// <summary>The quote card: its widest width and padding, the speaker's avatar and the passage's type (13/20).</summary>
    public double QuoteMaxWidth { get; set; }

    public Thickness QuotePadding { get; set; }

    public double QuoteAvatarSize { get; set; }

    public double QuoteFontSize { get; set; }

    public double QuoteLineHeight { get; set; }

    /// <summary>How far the quote card sits from its line, and how far it travels while it fades in.</summary>
    public double QuoteGap { get; set; }

    public double QuoteEnterOffset { get; set; }

    /// <summary>The placeholder bars under the summarizing label.</summary>
    public double SkeletonHeight { get; set; }

    /// <summary>Below this width the attendee stack moves under the date.</summary>
    public double NarrowBreakpoint { get; set; }

    /// <summary>Below this width owners show their first name.</summary>
    public double CompactBreakpoint { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 640;
        CardMinWidth = 320;
        HeaderPadding = new Thickness(20, 18, 20, 16);
        BodyPadding = new Thickness(20, 16, 20, 18);
        FooterPadding = new Thickness(20, 8, 12, 8);
        MarkTileSize = 36;
        MarkGlyphSize = 18;
        TitleMargin = new Thickness(12, 0, 12, 0);
        TitleFontSize = 15;
        TitleLineHeight = 22;
        MetaFontSize = 12;
        NarrowAttendeesMargin = new Thickness(12, 10, 0, 0);
        SectionGap = 18;
        HeadingGap = 6;
        HeadingFontSize = 12;
        LineFontSize = 14;
        LineLineHeight = 21;
        RowPadding = new Thickness(8, 4, 6, 4);
        RowMargin = new Thickness(-8, 0, -6, 0);
        BulletSize = 5;
        BulletMargin = new Thickness(2, 8, 11, 0);
        CheckHitSize = 32;
        CheckSize = 16;
        CheckRingSize = 22;
        CheckMargin = new Thickness(-8, -5.5, 2, -5.5);
        OwnerAvatarSize = 18;
        OwnerAvatarFontSize = 8;
        StampHeight = 22;
        StampFontSize = 11.5;
        StampPadding = new Thickness(6, 0);
        StampMargin = new Thickness(8, -0.5, 0, 0);
        SendHeight = 26;
        SendFontSize = 12;
        QuoteMaxWidth = 320;
        QuotePadding = new Thickness(12, 10, 12, 12);
        QuoteAvatarSize = 20;
        QuoteFontSize = 13;
        QuoteLineHeight = 20;
        QuoteGap = 6;
        QuoteEnterOffset = 3;
        SkeletonHeight = 10;
        NarrowBreakpoint = 460;
        CompactBreakpoint = 520;
    }
}
