using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="WorkflowMinimap"/>: 200 × 140 with an 8px margin around the map, the zoom bar's radius 10, step
/// blocks with corners of 2 and a view frame with corners of 3. Colors and shadow come from the ArcDesign tokens in the
/// theme.
/// </summary>
[ControlDesignToken]
internal sealed class WorkflowMinimapToken : AbstractControlDesignToken
{
    /// <summary>The map's size: 200 × 140.</summary>
    public double MapWidth { get; set; }

    public double MapHeight { get; set; }

    /// <summary>Space between the map's edge and the workflow drawn in it: 8.</summary>
    public Thickness MapPadding { get; set; }

    /// <summary>The map's corners: 10, as the zoom bar's.</summary>
    public CornerRadius MapRadius { get; set; }

    /// <summary>A step's block: corners of 2.</summary>
    public double NodeRadius { get; set; }

    /// <summary>The frame of the part the canvas shows: corners of 3.</summary>
    public double ViewportRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MapWidth = 200;
        MapHeight = 140;
        MapPadding = new Thickness(8);
        MapRadius = new CornerRadius(10);
        NodeRadius = 2;
        ViewportRadius = 3;
    }
}
