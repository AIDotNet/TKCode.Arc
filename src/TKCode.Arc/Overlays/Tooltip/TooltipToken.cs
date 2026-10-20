using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TooltipToken : AbstractControlDesignToken
{
    public double BubbleMaxWidth { get; set; }
    public Thickness BubblePadding { get; set; }

    /// <summary>Hairline: 14% background mixed into foreground (so the bubble edge reads on both themes).</summary>
    public Color BubbleBorderColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BubbleMaxWidth = 240;
        BubblePadding = new Thickness(16, 12);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        BubbleBorderColor = Oklch.MixOklab(neutrals.Foreground, neutrals.Background, 0.14);
    }
}
