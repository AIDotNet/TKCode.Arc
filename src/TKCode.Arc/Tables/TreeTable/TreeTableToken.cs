using Avalonia;
using Avalonia.Media;

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

    /// <summary>Fill of the odd rows when <see cref="TreeTable.IsStriped"/> is on.</summary>
    public Color StripeBackground { get; set; }

    /// <summary>Fill of a striped row under the pointer or the keyboard focus (a step darker than the stripe).</summary>
    public Color StripeHoverBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        StripeBackground = Oklch.MixOklab(n.Surface, n.Foreground, 0.02);
        StripeHoverBackground = Oklch.MixOklab(n.SurfaceMuted, n.Foreground, 0.03);
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
