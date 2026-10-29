using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class CountdownToken : AbstractControlDesignToken
{
    /// <summary>Finished compact pill: 35% success into the border, 9% into the surface (accent independent).</summary>
    public Color CompleteBorder { get; set; }

    public Color CompleteBackground { get; set; }

    public double CompactMinHeight { get; set; }
    public Thickness CompactPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CompleteBorder = Oklch.MixOklab(n.Border, n.Success, 0.35);
        CompleteBackground = Oklch.MixOklab(n.Surface, n.Success, 0.09);
        CompactMinHeight = 32;
        CompactPadding = new Thickness(12, 0);
    }
}
