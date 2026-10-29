using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SettingsCommand"/>, its rows, key caps and the choice pill.</summary>
[ControlDesignToken]
internal sealed class SettingsCommandToken : AbstractControlDesignToken
{
    /// <summary>The narrowest the palette gets (320, so it fits a 360px phone with margins).</summary>
    public double PanelMinWidth { get; set; }

    /// <summary>The default <see cref="SettingsCommand.ResultsMaxHeight"/> (360).</summary>
    public double ResultsMaxHeight { get; set; }

    /// <summary>Height of the search row (56).</summary>
    public double SearchRowHeight { get; set; }

    /// <summary>Space at the sides of the search row, so the glyph and text line up with the rows' icons and headers.</summary>
    public Thickness SearchRowInset { get; set; }

    /// <summary>The search glyph's side (18).</summary>
    public double SearchGlyph { get; set; }

    /// <summary>Space around the search glyph: centers it over the row icon tiles and leaves the gap to the text.</summary>
    public Thickness SearchGlyphMargin { get; set; }

    /// <summary>Gap before the Escape key cap.</summary>
    public Thickness EscapeCapMargin { get; set; }

    /// <summary>Space around the result rows (8).</summary>
    public Thickness ResultsInset { get; set; }

    /// <summary>Space around a group title (12, 10, 12, 4).</summary>
    public Thickness GroupHeaderInset { get; set; }

    /// <summary>Height of a result row (48).</summary>
    public double RowHeight { get; set; }

    /// <summary>Space at the sides of a row's content.</summary>
    public Thickness RowInset { get; set; }

    /// <summary>Corner radius of the active-row highlight (12).</summary>
    public CornerRadius RowRadius { get; set; }

    /// <summary>The icon tile's side (30).</summary>
    public double TileSide { get; set; }

    /// <summary>The icon tile's corner radius (9).</summary>
    public CornerRadius TileRadius { get; set; }

    /// <summary>The glyph's side inside the tile (16).</summary>
    public double TileGlyph { get; set; }

    /// <summary>Gap after the icon tile.</summary>
    public Thickness TileMargin { get; set; }

    /// <summary>Gap between the header and the description.</summary>
    public double TextGap { get; set; }

    /// <summary>Gap before a row's switch, choice or key caps.</summary>
    public Thickness AccessoryMargin { get; set; }

    /// <summary>Space around the no-results message (32).</summary>
    public Thickness EmptyInset { get; set; }

    /// <summary>Height of the footer (40).</summary>
    public double FooterHeight { get; set; }

    /// <summary>Space at the sides of the footer.</summary>
    public Thickness FooterInset { get; set; }

    /// <summary>Gap between two footer hints.</summary>
    public double HintGap { get; set; }

    /// <summary>Gap between a hint's key caps and its text.</summary>
    public double HintLabelGap { get; set; }

    /// <summary>The widest the change notice gets before it is trimmed.</summary>
    public double NoticeMaxWidth { get; set; }

    /// <summary>Gap between the footer hints and the change notice.</summary>
    public Thickness NoticeMargin { get; set; }

    /// <summary>The notice's check glyph (13).</summary>
    public double NoticeGlyph { get; set; }

    /// <summary>Gap after the notice's check glyph.</summary>
    public Thickness NoticeGlyphMargin { get; set; }

    /// <summary>Length of the result list's edge fades (20).</summary>
    public double ResultsFadeLength { get; set; }

    /// <summary>Key cap height (20).</summary>
    public double KeyCapHeight { get; set; }

    /// <summary>Key cap minimum width (20).</summary>
    public double KeyCapMinWidth { get; set; }

    /// <summary>Space at the sides of a key cap's label.</summary>
    public Thickness KeyCapInset { get; set; }

    /// <summary>Key cap corner radius (6).</summary>
    public CornerRadius KeyCapRadius { get; set; }

    /// <summary>Key cap edge: 1px with a 2px bottom.</summary>
    public Thickness KeyCapEdge { get; set; }

    /// <summary>Glyph side inside a key cap (12).</summary>
    public double KeyCapGlyph { get; set; }

    /// <summary>Gap between the caps of one shortcut.</summary>
    public double KeyCapGap { get; set; }

    /// <summary>Height of the choice pill (28).</summary>
    public double PillHeight { get; set; }

    /// <summary>Side of the arrows inside the choice pill (24).</summary>
    public double PillStepSide { get; set; }

    /// <summary>Glyph side of the choice pill arrows (14).</summary>
    public double PillStepGlyph { get; set; }

    /// <summary>Space inside the choice pill border, around the arrows.</summary>
    public Thickness PillInset { get; set; }

    /// <summary>Space at the sides of the choice value.</summary>
    public Thickness PillValueMargin { get; set; }

    /// <summary>Line height of text-xs (1.4 × 12).</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Line height of text-sm (1.4 × 14).</summary>
    public double BodyLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelMinWidth = 320;
        ResultsMaxHeight = 360;
        SearchRowHeight = 56;
        // Rows start at 8 (results inset) + 12 (row inset): the 30px tile spans 20–50 and the header starts at 62.
        SearchRowInset = new Thickness(20, 0);
        SearchGlyph = 18;
        SearchGlyphMargin = new Thickness(6, 0, 18, 0);
        EscapeCapMargin = new Thickness(12, 0, 0, 0);
        ResultsInset = new Thickness(8);
        GroupHeaderInset = new Thickness(12, 10, 12, 4);
        RowHeight = 48;
        RowInset = new Thickness(12, 0);
        RowRadius = new CornerRadius(12);
        TileSide = 30;
        TileRadius = new CornerRadius(9);
        TileGlyph = 16;
        TileMargin = new Thickness(0, 0, 12, 0);
        TextGap = 1;
        AccessoryMargin = new Thickness(12, 0, 0, 0);
        EmptyInset = new Thickness(32);
        FooterHeight = 40;
        FooterInset = new Thickness(20, 0, 16, 0);
        HintGap = 16;
        HintLabelGap = 6;
        NoticeMaxWidth = 260;
        NoticeMargin = new Thickness(12, 0, 0, 0);
        NoticeGlyph = 13;
        NoticeGlyphMargin = new Thickness(0, 0, 6, 0);
        ResultsFadeLength = 20;
        KeyCapHeight = 20;
        KeyCapMinWidth = 20;
        KeyCapInset = new Thickness(5, 0);
        KeyCapRadius = new CornerRadius(6);
        KeyCapEdge = new Thickness(1, 1, 1, 2);
        KeyCapGlyph = 12;
        KeyCapGap = 3;
        PillHeight = 28;
        PillStepSide = 24;
        PillStepGlyph = 14;
        PillInset = new Thickness(1);
        PillValueMargin = new Thickness(2, 0);
        SmallLineHeight = 12 * 1.4;
        BodyLineHeight = 14 * 1.4;
    }
}
