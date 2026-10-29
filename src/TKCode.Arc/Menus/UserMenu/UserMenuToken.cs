using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class UserMenuToken : AbstractControlDesignToken
{
    /// <summary>Trigger: a 40px pill with a 4px inset (10px on the right when the name shows).</summary>
    public double TriggerHeight { get; set; }
    public Thickness TriggerInset { get; set; }
    public Thickness NamedTriggerInset { get; set; }
    public double TriggerGap { get; set; }

    /// <summary>Face: 32px avatar with an 11px presence dot ringed by 2px of the trigger surface.</summary>
    public double FaceSize { get; set; }
    public double FaceFontSize { get; set; }
    public double TriggerDotSize { get; set; }
    public double DotRing { get; set; }
    public Thickness DotMargin { get; set; }

    public double NameMaxWidth { get; set; }
    public double ChevronSize { get; set; }
    public Thickness ChevronMargin { get; set; }

    /// <summary>Pressed trigger: surface-muted mixed with 8% foreground.</summary>
    public Color TriggerPressedBackground { get; set; }

    /// <summary>Gap between the avatar and the panel.</summary>
    public double PanelGap { get; set; }

    /// <summary>Below this window width the menu opens as a bottom sheet and the trigger shows the avatar only.</summary>
    public double CompactBreakpoint { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        TriggerHeight = 40;
        TriggerInset = new Thickness(4);
        NamedTriggerInset = new Thickness(4, 4, 10, 4);
        TriggerGap = 8;
        FaceSize = 32;
        FaceFontSize = 11.5;
        TriggerDotSize = 11;
        DotRing = 2;
        DotMargin = new Thickness(0, 0, -3, -3);
        NameMaxWidth = 176;
        ChevronSize = 16;
        ChevronMargin = new Thickness(-2, 0, 0, 0);
        TriggerPressedBackground = Oklch.MixOklab(n.SurfaceMuted, n.Foreground, 0.08);
        PanelGap = 8;
        CompactBreakpoint = 640;
    }
}
