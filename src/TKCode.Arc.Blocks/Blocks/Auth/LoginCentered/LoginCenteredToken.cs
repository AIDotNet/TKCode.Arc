// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): login-centered.module.css.
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of the <see cref="LoginCentered"/> frame: the two 64px bars, the stage around the card and the card's
/// width, with the container-query values (narrow at 520 and below, tiny at 400 and below). Colors, radii, fonts and
/// shadows come from ArcDesign tokens in the theme; the card's views are measured in <c>LoginCenteredMetrics</c>.
/// </summary>
[ControlDesignToken]
internal sealed class LoginCenteredToken : AbstractControlDesignToken
{
    /// <summary>The height of the framed block (<c>min-height: 640px</c>); a full-screen block fills its host instead.</summary>
    public double FrameMinHeight { get; set; }

    /// <summary>The top and bottom bars share one height (64), so the stage center and the ring center stay one point.</summary>
    public double BarHeight { get; set; }

    /// <summary>The bars' side padding: 24, 16 when narrow.</summary>
    public Thickness BarPadding { get; set; }

    public Thickness BarPaddingNarrow { get; set; }

    /// <summary>The gap between the parts of a bar (16).</summary>
    public double BarGap { get; set; }

    /// <summary>The stage around the card: 24 above and below, 16 at the sides; 16 all round when narrow, 12 at the sides when tiny.</summary>
    public Thickness StagePadding { get; set; }

    public Thickness StagePaddingNarrow { get; set; }

    public Thickness StagePaddingTiny { get; set; }

    /// <summary>The card is at most 420 wide (<c>width: min(100%, 420px)</c>).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The product mark (20 × 20) and the 8px gap before the name.</summary>
    public double MarkSize { get; set; }

    public double MarkGap { get; set; }

    /// <summary>The mode switch: 4 between prompt and button; the button's sides 12, 8 when tiny.</summary>
    public double SwitchGap { get; set; }

    public Thickness SwitchPadding { get; set; }

    public Thickness SwitchPaddingTiny { get; set; }

    /// <summary>The footer links: at least 32 high, 8 at the sides, 4 apart.</summary>
    public double LegalLinkHeight { get; set; }

    public Thickness LegalLinkPadding { get; set; }

    public double LegalGap { get; set; }

    /// <summary>Body copy keeps the root's tracking: −0.01em of 16px, inherited as −0.16px by every smaller line.</summary>
    public double BodyLetterSpacing { get; set; }

    /// <summary>The keyboard focus ring around the bar's pill buttons (2px, 2 outside).</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FrameMinHeight = 640;
        BarHeight = 64;
        BarPadding = new Thickness(24, 0);
        BarPaddingNarrow = new Thickness(16, 0);
        BarGap = 16;
        StagePadding = new Thickness(16, 24);
        StagePaddingNarrow = new Thickness(16);
        StagePaddingTiny = new Thickness(12, 16);
        CardMaxWidth = 420;
        MarkSize = 20;
        MarkGap = 8;
        SwitchGap = 4;
        SwitchPadding = new Thickness(12, 0);
        SwitchPaddingTiny = new Thickness(8, 0);
        LegalLinkHeight = 32;
        LegalLinkPadding = new Thickness(8, 0);
        LegalGap = 4;
        BodyLetterSpacing = -0.16;
        FocusRingMargin = new Thickness(-4);
    }
}
