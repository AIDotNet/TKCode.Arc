using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="EnrichColumn"/>: the card's radius and gutters (20px, 16px below 440px), the top bar, the header
/// and row heights (rows grow to 60px below 440px so values wrap to two lines), the logo and status icon sizes, the row
/// controls, the editor and the footer. Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class EnrichColumnToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The left and right gutter of every row.</summary>
    public Thickness Gutter { get; set; }

    /// <summary>The gutter below 440px.</summary>
    public Thickness NarrowGutter { get; set; }

    /// <summary>The top bar's height.</summary>
    public double TopBarHeight { get; set; }

    /// <summary>The column header row's height (the AI column's name over its prompt).</summary>
    public double HeaderHeight { get; set; }

    /// <summary>A row's height.</summary>
    public double RowHeight { get; set; }

    /// <summary>A row's height below 440px, where values wrap to two lines.</summary>
    public double NarrowRowHeight { get; set; }

    /// <summary>The space between the row column and the AI column.</summary>
    public double ColumnGap { get; set; }

    /// <summary>The logo before a row's name.</summary>
    public double LogoSize { get; set; }

    /// <summary>The monogram square's radius.</summary>
    public CornerRadius LogoCornerRadius { get; set; }

    /// <summary>The status icon of a cell.</summary>
    public double StatusSize { get; set; }

    /// <summary>The space between a cell's icon, its content and its row control.</summary>
    public double CellGap { get; set; }

    /// <summary>A cell's text: 13/18 (two lines of 18 below 440px).</summary>
    public double CellFontSize { get; set; }

    public double CellLineHeight { get; set; }

    /// <summary>The source link's and the prompt's smaller text.</summary>
    public double MetaFontSize { get; set; }

    /// <summary>The square row controls (run, retry, edit).</summary>
    public double IconButtonSize { get; set; }

    public double IconSize { get; set; }

    public CornerRadius IconButtonCornerRadius { get; set; }

    /// <summary>The column button in the top bar.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    /// <summary>The editor's padding.</summary>
    public Thickness EditorPadding { get; set; }

    /// <summary>The footer's height.</summary>
    public double FooterHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(16);
        Gutter = new Thickness(20, 0);
        NarrowGutter = new Thickness(16, 0);
        TopBarHeight = 56;
        HeaderHeight = 52;
        RowHeight = 44;
        NarrowRowHeight = 60;
        ColumnGap = 16;
        LogoSize = 16;
        LogoCornerRadius = new CornerRadius(4);
        StatusSize = 16;
        CellGap = 8;
        CellFontSize = 13;
        CellLineHeight = 18;
        MetaFontSize = 12;
        IconButtonSize = 26;
        IconSize = 14;
        IconButtonCornerRadius = new CornerRadius(8);
        ActionHeight = 30;
        ActionPadding = new Thickness(12, 0);
        EditorPadding = new Thickness(20, 14, 20, 16);
        FooterHeight = 44;
    }
}
