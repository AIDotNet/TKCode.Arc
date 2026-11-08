using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FluidTabItemToken : AbstractControlDesignToken
{
    /// <summary>Padding inside a tab.</summary>
    public Thickness InlinePadding { get; set; }

    /// <summary>Margin of the icon: the space between the icon and the header.</summary>
    public Thickness IconMargin { get; set; }

    /// <summary>Radius of the tab's hover fill (matches the selection highlight).</summary>
    public CornerRadius ItemRadius { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        InlinePadding = new Thickness(14, 8);
        IconMargin = new Thickness(0, 0, 10, 0);
        ItemRadius = new CornerRadius(12);
        FocusRingMargin = new Thickness(-2);
        FocusRingRadius = new CornerRadius(14);
        DisabledOpacity = 0.5;
    }
}
