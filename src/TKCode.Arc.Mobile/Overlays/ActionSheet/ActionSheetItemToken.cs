using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="ActionSheetItem"/>: a 56px row with 16px side padding.</summary>
[ControlDesignToken]
internal sealed class ActionSheetItemToken : AbstractControlDesignToken
{
    /// <summary>Smallest height of a row: 56.</summary>
    public double RowHeight { get; set; }

    /// <summary>Padding inside a row: 16 at the sides.</summary>
    public Thickness ContentPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowHeight = 56;
        ContentPadding = new Thickness(16, 0);
    }
}
