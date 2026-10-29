using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class CommandPaletteDialogToken : AbstractControlDesignToken
{
    /// <summary>The scrim behind the popup: a lighter dim than a dialog's (oklch(10% 0 0 / 0.46)), in both appearances.</summary>
    public Color ScrimColor { get; set; }

    /// <summary>Preferred palette width; the window minus 16px on each side wins in a narrow window.</summary>
    public double PanelWidth { get; set; }

    /// <summary>Distance of the palette from the window top as a fraction of the window height.</summary>
    public double PanelTopRatio { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ScrimColor = Oklch.ToColor(0.10, 0, 0, 0.32);
        PanelWidth = 560;
        PanelTopRatio = 0.13;
    }
}
