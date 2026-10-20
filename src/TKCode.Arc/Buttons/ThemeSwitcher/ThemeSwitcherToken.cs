using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ThemeSwitcherToken : AbstractControlDesignToken
{
    /// <summary>Side of the icon-only square (Arc 38px).</summary>
    public double IconOnlyExtent { get; set; }

    /// <summary>Corner radius of the icon-only square (Arc 12px).</summary>
    public CornerRadius IconOnlyRadius { get; set; }

    /// <summary>The 20px wrap that carries the icon nudge.</summary>
    public double IconWrapExtent { get; set; }

    /// <summary>Gap between the icon and the text.</summary>
    public double IconTextGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        IconOnlyExtent = 38;
        IconOnlyRadius = new CornerRadius(12);
        IconWrapExtent = 20;
        IconTextGap = 8;
    }
}
