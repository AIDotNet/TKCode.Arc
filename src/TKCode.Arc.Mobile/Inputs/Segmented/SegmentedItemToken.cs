using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="SegmentedItem"/>: a 36px segment with 8px corners.</summary>
[ControlDesignToken]
internal sealed class SegmentedItemToken : AbstractControlDesignToken
{
    /// <summary>Height of a segment: 36 (the track adds 2 on each side, so the control is 40 high).</summary>
    public double ItemHeight { get; set; }

    /// <summary>Padding inside a segment: 12 at the sides.</summary>
    public Thickness ItemPadding { get; set; }

    /// <summary>Corner radius of a segment: 8.</summary>
    public CornerRadius ItemRadius { get; set; }

    /// <summary>Opacity while a finger is on an unselected segment: 0.6.</summary>
    public double PressedOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ItemHeight = 36;
        ItemPadding = new Thickness(12, 0);
        ItemRadius = new CornerRadius(8);
        PressedOpacity = 0.6;
    }
}
