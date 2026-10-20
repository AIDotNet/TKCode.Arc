using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ButtonGroupToken : AbstractControlDesignToken
{
    /// <summary>Chevron segment width: the control height minus 6 (38 / 30px).</summary>
    public double TriggerExtentMD { get; set; }
    public double TriggerExtentSM { get; set; }

    /// <summary>Chevron segment height inside the 1px border (42 / 34px).</summary>
    public double TriggerHeightMD { get; set; }
    public double TriggerHeightSM { get; set; }

    /// <summary>Highlight radius, concentric with the group: radius-control − 3px inset − 1px border.</summary>
    public CornerRadius HighlightRadius { get; set; }

    /// <summary>Outline highlight while pressed: surface-muted mixed with 30% border-strong.</summary>
    public Color OutlinePressedHighlightColor { get; set; }

    /// <summary>Solid group: highlight (background 13% into foreground) and its pressed step (22%).</summary>
    public Color SolidHighlightColor { get; set; }
    public Color SolidPressedHighlightColor { get; set; }

    /// <summary>Solid group: hairline dividers (background 20% into foreground).</summary>
    public Color SolidDividerColor { get; set; }

    /// <summary>Solid group: the chevron's quieter ink (background 72% into foreground).</summary>
    public Color SolidMutedInk { get; set; }

    /// <summary>Whole group disabled.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Menu minimum width (13rem) and gap below the chevron.</summary>
    public double MenuMinWidth { get; set; }
    public double MenuGap { get; set; }

    public double GlyphExtent { get; set; }
    public double GlyphStroke { get; set; }

    /// <summary>Divider insets of the chevron segment (top / bottom 11 or 9px, sides 10px when vertical).</summary>
    public Thickness DividerMarginMD { get; set; }
    public Thickness DividerMarginSM { get; set; }
    public Thickness DividerMarginVertical { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        TriggerExtentMD = 38;
        TriggerExtentSM = 30;
        TriggerHeightMD = 42;
        TriggerHeightSM = 34;
        HighlightRadius = new CornerRadius(14);
        OutlinePressedHighlightColor = Oklch.MixOklab(n.SurfaceMuted, n.BorderStrong, 0.30);
        SolidHighlightColor = Oklch.MixOklab(n.Foreground, n.Background, 0.13);
        SolidPressedHighlightColor = Oklch.MixOklab(n.Foreground, n.Background, 0.22);
        SolidDividerColor = Oklch.MixOklab(n.Foreground, n.Background, 0.20);
        SolidMutedInk = Oklch.MixOklab(n.Foreground, n.Background, 0.72);
        DisabledOpacity = 0.52;
        MenuMinWidth = 208;
        MenuGap = 6;
        GlyphExtent = 16;
        GlyphStroke = 1.75;
        DividerMarginMD = new Thickness(-0.5, 11, 0, 11);
        DividerMarginSM = new Thickness(-0.5, 9, 0, 9);
        DividerMarginVertical = new Thickness(10, -0.5, 10, 0);
    }
}
