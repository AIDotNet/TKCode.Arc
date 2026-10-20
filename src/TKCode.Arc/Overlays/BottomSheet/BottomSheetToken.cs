using AtomUI.Theme.DesignTokens;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class BottomSheetToken : AbstractControlDesignToken
{
    /// <summary>Arc sheet overlay: light oklch(16% 0 0 / 0.34), dark oklch(0% 0 0 / 0.52). Its opacity follows the sheet.</summary>
    public Color ScrimColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ScrimColor = isDarkMode ? Oklch.ToColor(0, 0, 0, 0.52) : Oklch.ToColor(0.16, 0, 0, 0.34);
    }
}
