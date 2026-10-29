using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class AvatarToken : AbstractControlDesignToken
{
    public double DiameterSM { get; set; }
    public double DiameterMD { get; set; }
    public double DiameterLG { get; set; }
    public double DiameterXL { get; set; }
    public double InitialsFontSizeSM { get; set; }
    public double StatusSizeSM { get; set; }
    public double StatusSizeMD { get; set; }
    public double StatusSizeLG { get; set; }
    public double StatusSizeXL { get; set; }
    public Thickness StatusRingSM { get; set; }
    public Thickness StatusRing { get; set; }
    public Thickness StatusMarginXL { get; set; }

    /// <summary>Outer hairline around the status dot: 70% border.</summary>
    public BoxShadows StatusHalo { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DiameterSM = 28;
        DiameterMD = 36;
        DiameterLG = 48;
        DiameterXL = 88;
        InitialsFontSizeSM = 10;
        StatusSizeSM = 8;
        StatusSizeMD = 10;
        StatusSizeLG = 12;
        StatusSizeXL = 14;
        StatusRingSM = new Thickness(1.5);
        StatusRing = new Thickness(2);
        StatusMarginXL = new Thickness(0, 0, 4, 4);
        var halo = Oklch.WithAlpha(ArcPalette.Neutrals(isDarkMode).Border, 0.7);
        StatusHalo = new BoxShadows(new BoxShadow { Spread = 1, Color = halo });
    }
}
