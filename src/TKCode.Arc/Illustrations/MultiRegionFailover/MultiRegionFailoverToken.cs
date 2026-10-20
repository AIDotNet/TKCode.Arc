using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="MultiRegionFailover"/> (colors come from ArcDesign tokens).</summary>
[ControlDesignToken]
internal sealed class MultiRegionFailoverToken : AbstractControlDesignToken
{
    /// <summary>Corner radius of the node cards.</summary>
    public double NodeRadius { get; set; }

    /// <summary>Diameter of a travelling packet's core.</summary>
    public double PacketSize { get; set; }

    /// <summary>Inner padding of the frame around the diagram and caption.</summary>
    public Thickness FramePadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        NodeRadius = 12;
        PacketSize = 7;
        FramePadding = new Thickness(20, 20, 20, 14);
    }
}
