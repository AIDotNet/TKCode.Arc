using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="Table"/> (values from Arc's sortable-data-table.module.css).</summary>
[ControlDesignToken]
internal sealed class TableToken : AbstractControlDesignToken
{
    /// <summary>Header row height including its 1px rule (th height 44).</summary>
    public double HeaderHeight { get; set; }

    /// <summary>Body row height including its 1px divider (td height 52).</summary>
    public double RowHeight { get; set; }

    /// <summary>Row height of the compact (narrow) layout (min-height 64).</summary>
    public double CompactRowHeight { get; set; }

    /// <summary>Header strip height of the compact layout (32px pills plus 4px padding and the rule).</summary>
    public double CompactHeaderHeight { get; set; }

    /// <summary>Space above and below the content of a row that grew for wrapped cells.</summary>
    public double CellBlockPadding { get; set; }

    /// <summary>Space above and below the content of a grown row in the compact layout.</summary>
    public double CompactCellBlockPadding { get; set; }

    /// <summary>Widest natural (Auto) width of a wrapping column, so long text wraps instead of widening the table.</summary>
    public double WrapColumnMaxWidth { get; set; }

    /// <summary>Inline cell padding (space-4).</summary>
    public double CellInlinePadding { get; set; }

    /// <summary>Leading padding of the first column when the checkbox column is shown (space-2).</summary>
    public double PrimaryInsetSelectable { get; set; }

    /// <summary>Width of the checkbox column.</summary>
    public double SelectColumnWidth { get; set; }

    /// <summary>Height of the empty and loading rows.</summary>
    public double EmptyRowHeight { get; set; }

    public double FooterHeight { get; set; }
    public Thickness FooterInset { get; set; }
    public double CountGap { get; set; }
    public double ClearButtonHeight { get; set; }
    public Thickness ClearButtonInset { get; set; }

    /// <summary>Keeps the loading veil below the header row.</summary>
    public Thickness LoadingInset { get; set; }

    /// <summary>The table scrolls horizontally below this width (table min-width 560).</summary>
    public double MinTableWidth { get; set; }

    /// <summary>Below this width rows fold into two lines and the header becomes a strip of sort pills.</summary>
    public double CompactBreakpoint { get; set; }

    /// <summary>Fill of the odd rows when <see cref="Table.IsStriped"/> is on (the surface, a little toward the text color).</summary>
    public Color StripeBackground { get; set; }

    /// <summary>The same stripe over the tinted sorted column.</summary>
    public Color StripeColumnBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        StripeBackground = Oklch.MixOklab(n.Surface, n.Foreground, 0.02);
        StripeColumnBackground = Oklch.MixOklab(n.SurfaceMuted, n.Foreground, 0.02);
        HeaderHeight = 44;
        RowHeight = 52;
        CompactRowHeight = 64;
        CompactHeaderHeight = 41;
        CellBlockPadding = 14;
        CompactCellBlockPadding = 10;
        WrapColumnMaxWidth = 320;
        CellInlinePadding = 16;
        PrimaryInsetSelectable = 8;
        SelectColumnWidth = 44;
        EmptyRowHeight = 120;
        FooterHeight = 44;
        FooterInset = new Thickness(16, 0, 8, 0);
        CountGap = 4;
        ClearButtonHeight = 30;
        ClearButtonInset = new Thickness(12, 0);
        LoadingInset = new Thickness(0, 44, 0, 0);
        MinTableWidth = 560;
        CompactBreakpoint = 620;
    }
}
