using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="TaskBreakdown"/>. Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class TaskBreakdownToken : AbstractControlDesignToken
{
    /// <summary>At or below this width a subtask row puts its estimate and assignee under its title (600).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The block's padding (24) and its compact form (16).</summary>
    public Thickness Padding { get; set; }

    public Thickness CompactPadding { get; set; }

    /// <summary>A panel's padding (16).</summary>
    public Thickness PanelPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CompactWidth = 600;
        Padding = new Thickness(24);
        CompactPadding = new Thickness(16);
        PanelPadding = new Thickness(16);
    }
}
