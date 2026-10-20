using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ToastToken : AbstractControlDesignToken
{
    /// <summary>Widest toast (26rem); narrower containers shrink it.</summary>
    public double MaxToastWidth { get; set; }

    /// <summary>Surface padding (14px, 16px on the icon side).</summary>
    public Thickness SurfacePadding { get; set; }

    /// <summary>Gap after the icon (0.75rem).</summary>
    public Thickness IconMargin { get; set; }

    /// <summary>Round status icon (30px).</summary>
    public double IconDiameter { get; set; }

    /// <summary>Check mark size (16px, stroke 2.25).</summary>
    public double CheckSize { get; set; }

    /// <summary>Centers the title's first line on the 30px icon.</summary>
    public Thickness CopyPadding { get; set; }

    public double CopyLineHeight { get; set; }

    /// <summary>Close button (32px round).</summary>
    public double CloseDiameter { get; set; }

    public Thickness CloseMargin { get; set; }

    /// <summary>Surface: surface mixed 6% toward the page background.</summary>
    public Color SurfaceColor { get; set; }

    /// <summary>Icon disc: success 10% over surface.</summary>
    public Color IconFill { get; set; }

    /// <summary>Icon ring: success 24% over border.</summary>
    public Color IconRing { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxToastWidth = 416;
        SurfacePadding = new Thickness(16, 14, 14, 14);
        IconMargin = new Thickness(0, 0, 12, 0);
        IconDiameter = 30;
        CheckSize = 16;
        CopyPadding = new Thickness(0, 5, 0, 0);
        CopyLineHeight = 14 * 1.4;
        CloseDiameter = 32;
        CloseMargin = new Thickness(12, -1, -2, 0);
        var n = ArcPalette.Neutrals(isDarkMode);
        SurfaceColor = Oklch.MixOklab(n.Surface, n.Background, 0.06);
        IconFill = Oklch.MixOklab(n.Surface, n.Success, 0.10);
        IconRing = Oklch.MixOklab(n.Border, n.Success, 0.24);
    }
}
