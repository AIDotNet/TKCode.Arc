namespace TKCode.Arc;

/// <summary>Geometry of <see cref="DockPane"/>: the size a tool pane takes when it is first placed along an edge.</summary>
[ControlDesignToken]
internal sealed class DockPaneToken : AbstractControlDesignToken
{
    /// <summary>The default <see cref="DockPane.PreferredSize"/>: 280 (self-designed).</summary>
    public double DefaultPreferredSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DefaultPreferredSize = DockPane.DefaultPreferredSize;
    }
}
