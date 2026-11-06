using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of floating panes (self-designed, spec 5.5 and 9.3): a floating window or card is at least 320×240 and opens
/// with its top-left corner 24px left of and 16px above the pointer; a floating card has a 32px title bar, a 14px resize
/// grip in its bottom-right corner and its tree inset by 6px. Surfaces, radii and shadows come from the ArcDesign tokens.
/// </summary>
[ControlDesignToken]
internal sealed class DockFloatingWindowToken : AbstractControlDesignToken
{
    /// <summary>Smallest width of a floating window or card: 320.</summary>
    public double MinWidth { get; set; }

    /// <summary>Smallest height of a floating window or card: 240.</summary>
    public double MinHeight { get; set; }

    /// <summary>How far left of the pointer a pane dropped outside the layout opens: 24.</summary>
    public double OpenOffsetX { get; set; }

    /// <summary>How far above the pointer a pane dropped outside the layout opens: 16.</summary>
    public double OpenOffsetY { get; set; }

    /// <summary>Height of a floating card's title bar: 32.</summary>
    public double CardTitleHeight { get; set; }

    /// <summary>The square resize grip in a floating card's bottom-right corner: 14.</summary>
    public double CardGripSize { get; set; }

    /// <summary>Space around the tree inside a floating card: 6 (0 at the top, under the title bar).</summary>
    public Thickness CardContentPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MinWidth = 320;
        MinHeight = 240;
        OpenOffsetX = 24;
        OpenOffsetY = 16;
        CardTitleHeight = 32;
        CardGripSize = 14;
        CardContentPadding = new Thickness(6, 0, 6, 6);
    }
}
