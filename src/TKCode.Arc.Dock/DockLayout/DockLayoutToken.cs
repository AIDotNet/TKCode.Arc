using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="DockLayout"/> (self-designed): 6px gaps between the group cards (the gap is the divider's grab
/// area), 6px around the layout, 32px auto-hide strips, and a flyout that covers at most 70% of the layout.
/// </summary>
[ControlDesignToken]
internal sealed class DockLayoutToken : AbstractControlDesignToken
{
    /// <summary>Gap between neighbouring groups: 6.</summary>
    public double Gap { get; set; }

    /// <summary>Space around the whole layout: 6.</summary>
    public Thickness Padding { get; set; }

    /// <summary>Width (left and right) or height (top and bottom) of an auto-hide strip: 32.</summary>
    public double StripThickness { get; set; }

    /// <summary>Largest share of the layout an auto-hide flyout covers: 0.7.</summary>
    public double FlyoutMaxShare { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Gap = 6;
        Padding = new Thickness(6);
        StripThickness = 32;
        FlyoutMaxShare = 0.7;
    }
}
