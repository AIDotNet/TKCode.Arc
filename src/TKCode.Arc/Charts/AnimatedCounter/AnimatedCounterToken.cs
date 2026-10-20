using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class AnimatedCounterToken : AbstractControlDesignToken
{
    public Thickness LabelMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LabelMargin = new Thickness(0, 0, 0, 8);
    }
}
