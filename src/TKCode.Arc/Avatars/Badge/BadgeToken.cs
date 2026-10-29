using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class BadgeToken : AbstractControlDesignToken
{
    public double PillHeightMD { get; set; }
    public double PillHeightSM { get; set; }
    public Thickness InlinePaddingMD { get; set; }
    public Thickness InlinePaddingSM { get; set; }
    public double LabelFontSizeSM { get; set; }
    public double ContentGapMD { get; set; }
    public double ContentGapSM { get; set; }

    /// <summary>Opacity of the accent ring laid over the border for the info tone (24% accent into border).</summary>
    public double InfoRingOpacity { get; set; }

    /// <summary>Info hover: 32% accent ring and 10% accent wash over surface.</summary>
    public double InfoHoverRingOpacity { get; set; }

    public double InfoHoverWashOpacity { get; set; }

    // Tone fills are color-mixes with neutral colors only (accent independent).
    public Color SuccessBackground { get; set; }
    public Color SuccessBorder { get; set; }
    public Color WarningBackground { get; set; }
    public Color WarningBorder { get; set; }
    public Color DangerBackground { get; set; }
    public Color DangerBorder { get; set; }
    public Color NeutralHoverBackground { get; set; }
    public Color NeutralHoverBorder { get; set; }
    public Color SuccessHoverBackground { get; set; }
    public Color SuccessHoverBorder { get; set; }
    public Color WarningHoverBackground { get; set; }
    public Color WarningHoverBorder { get; set; }
    public Color DangerHoverBackground { get; set; }
    public Color DangerHoverBorder { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PillHeightMD = 26;
        PillHeightSM = 22;
        InlinePaddingMD = new Thickness(10, 0);
        InlinePaddingSM = new Thickness(8, 0);
        LabelFontSizeSM = 11;
        ContentGapMD = 5;
        ContentGapSM = 4;
        InfoRingOpacity = 0.24;
        InfoHoverRingOpacity = 0.32;
        InfoHoverWashOpacity = 0.10;
        var n = ArcPalette.Neutrals(isDarkMode);
        SuccessBackground = Oklch.MixOklab(n.Surface, n.Success, 0.10);
        SuccessBorder = Oklch.MixOklab(n.Border, n.Success, 0.25);
        WarningBackground = Oklch.MixOklab(n.Surface, n.Warning, 0.11);
        WarningBorder = Oklch.MixOklab(n.Border, n.Warning, 0.27);
        DangerBackground = Oklch.MixOklab(n.Surface, n.Danger, 0.10);
        DangerBorder = Oklch.MixOklab(n.Border, n.Danger, 0.26);
        NeutralHoverBackground = Oklch.MixOklab(n.Surface, n.TextSecondary, 0.10);
        NeutralHoverBorder = Oklch.MixOklab(n.Border, n.TextSecondary, 0.32);
        SuccessHoverBackground = Oklch.MixOklab(n.Surface, n.Success, 0.10);
        SuccessHoverBorder = Oklch.MixOklab(n.Border, n.Success, 0.32);
        WarningHoverBackground = Oklch.MixOklab(n.Surface, n.Warning, 0.10);
        WarningHoverBorder = Oklch.MixOklab(n.Border, n.Warning, 0.32);
        DangerHoverBackground = Oklch.MixOklab(n.Surface, n.Danger, 0.10);
        DangerHoverBorder = Oklch.MixOklab(n.Border, n.Danger, 0.32);
    }
}
