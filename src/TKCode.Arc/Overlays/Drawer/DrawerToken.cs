using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class DrawerToken : AbstractControlDesignToken
{
    /// <summary>Arc drawer overlay: oklch(10% 0 0 / 0.38), lighter than the dialog's.</summary>
    public Color ScrimColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ScrimColor = Oklch.ToColor(0.10, 0, 0, 0.38);
    }
}
