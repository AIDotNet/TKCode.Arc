using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="InboxSidebar"/>'s header and label dots.</summary>
[ControlDesignToken]
internal sealed class InboxSidebarToken : AbstractControlDesignToken
{
    /// <summary>Gap between the toggle row and Compose.</summary>
    public double HeaderGap { get; set; }

    /// <summary>Side of the rail toggle.</summary>
    public double ToggleSize { get; set; }

    /// <summary>Space inside Compose; its left edge keeps the pencil on the rail.</summary>
    public Thickness ComposePadding { get; set; }

    /// <summary>Diameter of a label's dot.</summary>
    public double LabelDot { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderGap = 12;
        ToggleSize = 32;
        ComposePadding = new Thickness(12, 0);
        LabelDot = 8;
    }
}
