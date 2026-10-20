using AtomUI.Theme.DesignTokens;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class DropdownMenuToken : AbstractControlDesignToken
{
    /// <summary>Gap between the trigger icon, label and chevron (space-2).</summary>
    public double TriggerGap { get; set; }

    public double TriggerIconSize { get; set; }

    /// <summary>Chevron: 15px, 1.8 stroke.</summary>
    public double ChevronSize { get; set; }
    public double ChevronStroke { get; set; }

    /// <summary>Gap between the trigger and the menu.</summary>
    public double MenuGap { get; set; }

    /// <summary>Pressed trigger: surface-muted mixed with 55% border (color only; the anchor never scales).</summary>
    public Color TriggerPressedBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        TriggerGap = 8;
        TriggerIconSize = 16;
        ChevronSize = 15;
        ChevronStroke = 1.8;
        MenuGap = 6;
        TriggerPressedBackground = Oklch.MixOklab(n.SurfaceMuted, n.Border, 0.55);
    }
}
