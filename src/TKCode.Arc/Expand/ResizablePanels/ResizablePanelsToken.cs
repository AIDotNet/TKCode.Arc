
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ResizablePanelsToken : AbstractControlDesignToken
{
    /// <summary>Width a divider takes in the layout (Arc: a one pixel line; the grab area overlaps the panes).</summary>
    public double DividerThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DividerThickness = 1;
    }
}
