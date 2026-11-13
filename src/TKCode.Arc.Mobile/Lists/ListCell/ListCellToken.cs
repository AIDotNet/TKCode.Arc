using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="ListCell"/>: a 48px row with 16 by 12 padding and the separator of a <see cref="ListItem"/>.</summary>
[ControlDesignToken]
internal sealed class ListCellToken : AbstractControlDesignToken
{
    /// <summary>Smallest height of a cell: 48, the height of a <see cref="ListItem"/>.</summary>
    public double RowHeight { get; set; }

    /// <summary>Padding inside a cell: 16 at the sides, 12 above and below.</summary>
    public Thickness ContentPadding { get; set; }

    /// <summary>How far the separator is inset from the cell's start: 16, as in a <see cref="ListItem"/>.</summary>
    public Thickness SeparatorInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowHeight = 48;
        ContentPadding = new Thickness(16, 12);
        SeparatorInset = new Thickness(16, 0, 0, 0);
    }
}
