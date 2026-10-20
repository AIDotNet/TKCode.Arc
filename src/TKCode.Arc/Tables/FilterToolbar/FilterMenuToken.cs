using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="FilterMenu"/>: the Add filter trigger and its two-step panel.</summary>
[ControlDesignToken]
internal sealed class FilterMenuToken : AbstractControlDesignToken
{
    public Thickness TriggerInset { get; set; }
    public double TriggerGlyphExtent { get; set; }
    public double TriggerGap { get; set; }

    /// <summary>Panel width (16rem).</summary>
    public double PanelWidth { get; set; }
    public Thickness PanelInset { get; set; }

    /// <summary>The value list scrolls past min(20rem, 55vh).</summary>
    public double ListMaxHeight { get; set; }

    public double HeadHeight { get; set; }
    public Thickness HeadInset { get; set; }
    public double ItemHeight { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    /// <summary>Pressed trigger surface: surface-muted mixed with 55% border (accent independent).</summary>
    public Color PressedFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerInset = new Thickness(12, 0);
        TriggerGlyphExtent = 15;
        TriggerGap = 8;
        PanelWidth = 256;
        PanelInset = new Thickness(6);
        ListMaxHeight = 320;
        HeadHeight = 32;
        HeadInset = new Thickness(10, 0, 8, 0);
        ItemHeight = 36;
        FocusRingRadius = new CornerRadius(21);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        PressedFill = Oklch.MixOklab(neutrals.SurfaceMuted, neutrals.Border, 0.55);
    }
}
