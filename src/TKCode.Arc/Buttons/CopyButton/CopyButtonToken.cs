using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class CopyButtonToken : AbstractControlDesignToken
{
    public Thickness InlinePadding { get; set; }
    public double IconLabelGap { get; set; }
    public double DisabledOpacity { get; set; }
    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        InlinePadding = new Thickness(12, 0);
        IconLabelGap = 8;
        DisabledOpacity = 0.5;
        FocusRingRadius = new CornerRadius(22);
    }
}
