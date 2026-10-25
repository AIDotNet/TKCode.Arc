using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>TreeTable geometry (Arc's <c>--tt-row</c>, <c>--tt-indent</c>, <c>--tt-chevron</c>).</summary>
[ControlDesignToken]
internal sealed class TreeTableToken : AbstractControlDesignToken
{
    public double RowHeight { get; set; }
    public double HeaderHeight { get; set; }
    public double IndentWidth { get; set; }
    public double ChevronExtent { get; set; }
    public double CheckColumnWidth { get; set; }
    public double TreeCellMargin { get; set; }
    public double CellInlinePadding { get; set; }
    public Thickness BodyPadding { get; set; }
    public CornerRadius TableCornerRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowHeight = 44;
        HeaderHeight = 40;
        IndentWidth = 20;
        ChevronExtent = 24;
        CheckColumnWidth = 44;
        TreeCellMargin = 10;
        CellInlinePadding = 12;
        BodyPadding = new Thickness(0, 0, 0, 6);
        TableCornerRadius = new CornerRadius(26);
    }
}
