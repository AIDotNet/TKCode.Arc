using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class DialogToken : AbstractControlDesignToken
{
    /// <summary>Arc dialog overlay: oklch(10% 0 0 / 0.46) in both appearances.</summary>
    public Color ScrimColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ScrimColor = Oklch.ToColor(0.10, 0, 0, 0.46);
    }
}
