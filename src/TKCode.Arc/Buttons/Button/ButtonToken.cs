using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ButtonToken : AbstractControlDesignToken
{
    public Thickness InlinePaddingSM { get; set; }
    public Thickness InlinePaddingMD { get; set; }
    public Thickness InlinePaddingLG { get; set; }
    public double HoverOpacity { get; set; }
    public double PressedOpacity { get; set; }
    public double DisabledOpacity { get; set; }
    public double LabelGap { get; set; }
    public double SpinnerSize { get; set; }
    public CornerRadius FocusRingRadius { get; set; }
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Disabled danger label: 55% danger mixed into surface-muted (accent independent).</summary>
    public Color DangerDisabledForeground { get; set; }

    /// <summary>
    /// Ghost hover and press fill on a raised surface: the foreground mixed into surface-raised (7% dark, 5% light), because
    /// surface-muted is only a few steps away from surface-raised and the hover would nearly vanish there.
    /// </summary>
    public Color GhostRaisedHoverBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        InlinePaddingSM = new Thickness(12, 0);
        InlinePaddingMD = new Thickness(16, 0);
        InlinePaddingLG = new Thickness(20, 0);
        HoverOpacity = 0.91;
        PressedOpacity = 0.84;
        DisabledOpacity = 0.52;
        LabelGap = 8;
        SpinnerSize = 16;
        FocusRingRadius = new CornerRadius(22);
        FocusRingMargin = new Thickness(-4);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        DangerDisabledForeground = Oklch.MixOklab(neutrals.SurfaceMuted, neutrals.Danger, 0.55);
        GhostRaisedHoverBackground = Oklch.MixOklab(neutrals.SurfaceRaised, neutrals.Foreground, isDarkMode ? 0.07 : 0.05);
    }
}
