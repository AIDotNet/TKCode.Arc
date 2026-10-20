using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class DialogSurfaceToken : AbstractControlDesignToken
{
    public double PanelWidthSM { get; set; }
    public double PanelWidthMD { get; set; }
    public double PanelWidthLG { get; set; }

    /// <summary>Viewport gutter on each side (Arc: width min(100vw − 32px, 440px)).</summary>
    public Thickness ViewportMargin { get; set; }

    public Thickness HeaderPadding { get; set; }
    public Thickness BodyPadding { get; set; }
    public double HeaderGap { get; set; }
    public Thickness DescriptionMargin { get; set; }
    public Thickness ActionsMargin { get; set; }
    public double CloseButtonSize { get; set; }

    /// <summary>Centres the 32px close button on the title's first line (18px × 1.4 line height).</summary>
    public Thickness CloseButtonMargin { get; set; }

    /// <summary>Close button rest fill: surface-muted at 50% (color-mix with transparent).</summary>
    public Color CloseButtonBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelWidthSM = 360;
        PanelWidthMD = 440;
        PanelWidthLG = 560;
        ViewportMargin = new Thickness(16);
        HeaderPadding = new Thickness(24);
        BodyPadding = new Thickness(24);
        HeaderGap = 20;
        DescriptionMargin = new Thickness(0, 8, 0, 0);
        ActionsMargin = new Thickness(0, 24, 0, 0);
        CloseButtonSize = 32;
        var lineHeight = 18 * 1.4;
        CloseButtonMargin = new Thickness(0, (lineHeight - 32) / 2, 0, (lineHeight - 32) / 2);
        CloseButtonBackground = Oklch.WithAlpha(ArcPalette.Neutrals(isDarkMode).SurfaceMuted, 0.5);
    }
}
