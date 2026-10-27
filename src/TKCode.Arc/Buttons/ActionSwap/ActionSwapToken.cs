using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ActionSwapToken : AbstractControlDesignToken
{
    /// <summary>Inline padding per size (Arc <c>--space-4 / 5 / 6</c>).</summary>
    public Thickness InlinePaddingSM { get; set; }
    public Thickness InlinePaddingMD { get; set; }
    public Thickness InlinePaddingLG { get; set; }

    /// <summary>The 16px leading icon slot.</summary>
    public double IconExtent { get; set; }

    /// <summary>Gap between the icon and the label (Arc <c>--space-2</c>).</summary>
    public double LabelGap { get; set; }

    /// <summary>Disabled opacity.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Success tone: fill (success 10% over surface), border (25% over border) and hovered border (42%).</summary>
    public Color SuccessFill { get; set; }
    public Color SuccessEdge { get; set; }
    public Color SuccessEdgeHover { get; set; }

    /// <summary>Warning tone: fill (12%), border (30%) and hovered border (48%).</summary>
    public Color WarningFill { get; set; }
    public Color WarningEdge { get; set; }
    public Color WarningEdgeHover { get; set; }

    /// <summary>Failure face: fill (danger 10% over surface), border (25%) and hovered border (42%).</summary>
    public Color DangerFill { get; set; }
    public Color DangerEdge { get; set; }
    public Color DangerEdgeHover { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        InlinePaddingSM = new Thickness(16, 0);
        InlinePaddingMD = new Thickness(20, 0);
        InlinePaddingLG = new Thickness(24, 0);
        IconExtent = 16;
        LabelGap = 8;
        DisabledOpacity = 0.6;
        // color-mix(in oklab, tone p%, base) is MixOklab(tone, base, 1 - p).
        SuccessFill = Oklch.MixOklab(n.Success, n.Surface, 0.90);
        SuccessEdge = Oklch.MixOklab(n.Success, n.Border, 0.75);
        SuccessEdgeHover = Oklch.MixOklab(n.Success, n.Border, 0.58);
        WarningFill = Oklch.MixOklab(n.Warning, n.Surface, 0.88);
        WarningEdge = Oklch.MixOklab(n.Warning, n.Border, 0.70);
        WarningEdgeHover = Oklch.MixOklab(n.Warning, n.Border, 0.52);
        DangerFill = Oklch.MixOklab(n.Danger, n.Surface, 0.90);
        DangerEdge = Oklch.MixOklab(n.Danger, n.Border, 0.75);
        DangerEdgeHover = Oklch.MixOklab(n.Danger, n.Border, 0.58);
    }
}
