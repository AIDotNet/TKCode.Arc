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

    /// <summary>The bar across a Busy dot (inside the 10px dot and its 2px ring).</summary>
    public double StatusBarWidth { get; set; }

    public double StatusBarHeight { get; set; }

    /// <summary>Outer hairline around the status dot: 70% border.</summary>
    public BoxShadows StatusHalo { get; set; }

    // Appended (AvatarTokenKind numbers tokens in declaration order).

    /// <summary>LargePlus: a 64px circle, a 13px dot inset 2px so its center stays near the circle's edge.</summary>
    public double DiameterLGPlus { get; set; }

    public double StatusSizeLGPlus { get; set; }
    public Thickness StatusMarginLGPlus { get; set; }

    /// <summary>
    /// The Busy bar at the other dot sizes: like <see cref="StatusBarWidth"/> in the medium dot, it spans the dot's inner
    /// circle less 1px at each end.
    /// </summary>
    public double StatusBarWidthSM { get; set; }

    public double StatusBarWidthLG { get; set; }
    public double StatusBarWidthLGPlus { get; set; }
    public double StatusBarWidthXL { get; set; }

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
        StatusBarWidth = 4;
        StatusBarHeight = 1.5;
        var halo = Oklch.WithAlpha(ArcPalette.Neutrals(isDarkMode).Border, 0.7);
        StatusHalo = new BoxShadows(new BoxShadow { Spread = 1, Color = halo });
        DiameterLGPlus = 64;
        StatusSizeLGPlus = 13;
        StatusMarginLGPlus = new Thickness(0, 0, 2, 2);
        StatusBarWidthSM = 3;
        StatusBarWidthLG = 6;
        StatusBarWidthLGPlus = 7;
        StatusBarWidthXL = 8;
    }
}
