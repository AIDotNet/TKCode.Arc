using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class PopoverSurfaceToken : AbstractControlDesignToken
{
    public double PanelMinWidth { get; set; }
    public double PanelMaxWidth { get; set; }
    public Thickness PanelPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelMinWidth = 192;
        PanelMaxWidth = 352;
        PanelPadding = new Thickness(16);
    }
}
