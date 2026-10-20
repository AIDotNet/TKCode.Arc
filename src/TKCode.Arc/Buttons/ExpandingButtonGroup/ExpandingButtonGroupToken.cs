using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ExpandingButtonGroupToken : AbstractControlDesignToken
{
    /// <summary>Pill padding (Arc <c>--ebg-pad</c> 3px).</summary>
    public Thickness PillInset { get; set; }

    /// <summary>Gap between actions.</summary>
    public double ActionGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PillInset = new Thickness(3);
        ActionGap = 2;
    }
}
