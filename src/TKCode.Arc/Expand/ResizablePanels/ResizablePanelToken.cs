
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ResizablePanelToken : AbstractControlDesignToken
{
    /// <summary>Default minimum width in px where a pane starts to resist (Arc: 80).</summary>
    public double DefaultMinWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DefaultMinWidth = ResizablePanelsMath.DefaultMin;
    }
}
