using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AgentStatus"/> (Arc Agent status, design reference), as measured: a 40px pill whose head has
/// 7px before the 24px orb and 14px after the timer, inline buttons 30px high, a panel at least 384px wide with 6 / 12 /
/// 12px padding, 28px step rows and 36px footer buttons. Colors, shadows and type come from the ArcDesign tokens in the
/// theme; the shimmer's resting color is an accent-independent mix of the text and surface colors.
/// </summary>
[ControlDesignToken]
internal sealed class AgentStatusToken : AbstractControlDesignToken
{
    /// <summary>The pill's height (40), its corner radius (20) and the open panel's corner radius (22).</summary>
    public double PillHeight { get; set; }

    public CornerRadius PillCornerRadius { get; set; }

    public CornerRadius PanelCornerRadius { get; set; }

    /// <summary>The head inside the 1px border: radius 19 (the hover fill and the inset focus ring follow it).</summary>
    public CornerRadius HeadCornerRadius { get; set; }

    /// <summary>The narrowest open panel (384) and the container width under which the pill turns compact (400).</summary>
    public double PanelMinWidth { get; set; }

    public double CompactBreakpoint { get; set; }

    /// <summary>The panel body's padding (6 above, 12 at the sides and below) and the gap between its blocks (12).</summary>
    public Thickness BodyPadding { get; set; }

    public double BodyGap { get; set; }

    /// <summary>Inline buttons in the pill: 30 high, radius 15, 12 at the sides; the panel's buttons: 36 high, radius 18.</summary>
    public double InlineButtonHeight { get; set; }

    public CornerRadius InlineButtonCornerRadius { get; set; }

    public double PanelButtonHeight { get; set; }

    public CornerRadius PanelButtonCornerRadius { get; set; }

    public Thickness ButtonPadding { get; set; }

    /// <summary>The focus ring of a button: 2px, 2px outside (margin −4), radius 4 more than the button's.</summary>
    public Thickness ButtonFocusRingMargin { get; set; }

    /// <summary>The label's letter spacing (Inter 14/20, −0.14).</summary>
    public double LabelLetterSpacing { get; set; }

    /// <summary>The working label's resting color under the shimmer: half text, half surface.</summary>
    public Color ShimmerBaseColor { get; set; }

    /// <summary>How much accent the base of an active or waiting segment takes over the border color (24%).</summary>
    public double ActiveSegmentMix { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PillHeight = 40;
        PillCornerRadius = new CornerRadius(20);
        PanelCornerRadius = new CornerRadius(22);
        HeadCornerRadius = new CornerRadius(19);
        PanelMinWidth = 384;
        CompactBreakpoint = 400;
        BodyPadding = new Thickness(12, 6, 12, 12);
        BodyGap = 12;
        InlineButtonHeight = 30;
        InlineButtonCornerRadius = new CornerRadius(15);
        PanelButtonHeight = 36;
        PanelButtonCornerRadius = new CornerRadius(18);
        ButtonPadding = new Thickness(12, 0);
        ButtonFocusRingMargin = new Thickness(-4);
        LabelLetterSpacing = -0.14;
        var n = ArcPalette.Neutrals(isDarkMode);
        ShimmerBaseColor = Oklch.MixOklab(n.Foreground, n.Surface, 0.5);
        ActiveSegmentMix = 0.24;
    }
}
