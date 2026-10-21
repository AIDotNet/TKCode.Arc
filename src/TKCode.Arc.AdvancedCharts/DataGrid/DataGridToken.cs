using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry and motion of <see cref="DataGrid"/> (values from Arc's data grid, used as a design reference).</summary>
[ControlDesignToken]
internal sealed class DataGridToken : AbstractControlDesignToken
{
    /// <summary>Row height of the compact density.</summary>
    public double RowHeightCompact { get; set; }

    /// <summary>Row height of the standard density.</summary>
    public double RowHeightStandard { get; set; }

    /// <summary>Row height of the comfortable density.</summary>
    public double RowHeightComfortable { get; set; }

    /// <summary>Height of the header row.</summary>
    public double HeaderRowHeight { get; set; }

    /// <summary>Height of the filter row.</summary>
    public double FilterRowHeight { get; set; }

    /// <summary>Height of the totals row.</summary>
    public double TotalsRowHeight { get; set; }

    /// <summary>Width of the row gutter with row numbers.</summary>
    public double GutterWidth { get; set; }

    /// <summary>Width of the row gutter without row numbers.</summary>
    public double GutterWidthNarrow { get; set; }

    /// <summary>Widest a column can be resized.</summary>
    public double ColumnMaxWidth { get; set; }

    /// <summary>Width change of Alt + arrow keys on a header.</summary>
    public double ColumnResizeStep { get; set; }

    /// <summary>Narrowest a pinned column is squeezed to on a narrow grid.</summary>
    public double PinnedColumnMinWidth { get; set; }

    /// <summary>Side of the fill handle square.</summary>
    public double FillHandleSize { get; set; }

    /// <summary>Width of the active cell ring.</summary>
    public double ActiveCellRingWidth { get; set; }

    /// <summary>Corner radius of the selection, the active cell and the fill preview.</summary>
    public CornerRadius SelectionCornerRadius { get; set; }

    /// <summary>Accent opacity of the selection fill.</summary>
    public double SelectionFillOpacity { get; set; }

    /// <summary>Accent share of the checked-row tint.</summary>
    public double CheckedRowFillOpacity { get; set; }

    /// <summary>Accent opacity of the wash that flashes pasted and filled cells.</summary>
    public double WashOpacity { get; set; }

    /// <summary>Width of the shadow at the pinned edge while scrolled.</summary>
    public double PinnedEdgeShadowWidth { get; set; }

    /// <summary>Inline padding of cell text.</summary>
    public double CellInlinePadding { get; set; }

    /// <summary>Cell text size of the compact density.</summary>
    public double CompactCellFontSize { get; set; }

    /// <summary>Height of the toolbar.</summary>
    public double ToolbarHeight { get; set; }

    /// <summary>Height of the toolbar buttons and the search field.</summary>
    public double ToolButtonHeight { get; set; }

    /// <summary>Width of the search field.</summary>
    public double SearchWidth { get; set; }

    /// <summary>Height of the status bar.</summary>
    public double StatusBarHeight { get; set; }

    /// <summary>Indent of each group level.</summary>
    public double GroupIndent { get; set; }

    /// <summary>Duration of the wash.</summary>
    public TimeSpan WashDuration { get; set; }

    /// <summary>Time rows slide to their new place after a sort.</summary>
    public TimeSpan SortSlideWindow { get; set; }

    /// <summary>Time a status message stays.</summary>
    public TimeSpan StatusMessageDuration { get; set; }

    /// <summary>Time a status message with an action (Undo) stays.</summary>
    public TimeSpan StatusActionDuration { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowHeightCompact = 32;
        RowHeightStandard = 40;
        RowHeightComfortable = 48;
        HeaderRowHeight = 40;
        FilterRowHeight = 40;
        TotalsRowHeight = 40;
        GutterWidth = 48;
        GutterWidthNarrow = 40;
        ColumnMaxWidth = 560;
        ColumnResizeStep = 16;
        PinnedColumnMinWidth = 96;
        FillHandleSize = 8;
        ActiveCellRingWidth = 2;
        SelectionCornerRadius = new CornerRadius(2);
        SelectionFillOpacity = 0.09;
        CheckedRowFillOpacity = 0.05;
        WashOpacity = 0.2;
        PinnedEdgeShadowWidth = 10;
        CellInlinePadding = 12;
        CompactCellFontSize = 13;
        ToolbarHeight = 52;
        ToolButtonHeight = 32;
        SearchWidth = 220;
        StatusBarHeight = 34;
        GroupIndent = 16;
        WashDuration = TimeSpan.FromMilliseconds(900);
        SortSlideWindow = TimeSpan.FromMilliseconds(700);
        StatusMessageDuration = TimeSpan.FromMilliseconds(2400);
        StatusActionDuration = TimeSpan.FromSeconds(6);
    }
}
