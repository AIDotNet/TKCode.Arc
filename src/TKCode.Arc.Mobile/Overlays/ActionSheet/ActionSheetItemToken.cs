using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="ActionSheetItem"/>: a 56px row with 16px side padding and the check mark of a checked row.</summary>
[ControlDesignToken]
internal sealed class ActionSheetItemToken : AbstractControlDesignToken
{
    /// <summary>Smallest height of a row: 56.</summary>
    public double RowHeight { get; set; }

    /// <summary>Padding inside a row: 16 at the sides.</summary>
    public Thickness ContentPadding { get; set; }

    // Appended (ActionSheetItemTokenKind numbers tokens in declaration order).

    /// <summary>Size of the check mark of a checked row: 20.</summary>
    public double CheckExtent { get; set; }

    /// <summary>Margin around the label of a checked row, so it stays centered and clear of the check mark: 28 at the sides.</summary>
    public Thickness CheckedContentMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowHeight = 56;
        ContentPadding = new Thickness(16, 0);
        CheckExtent = 20;
        CheckedContentMargin = new Thickness(28, 0);
    }
}
