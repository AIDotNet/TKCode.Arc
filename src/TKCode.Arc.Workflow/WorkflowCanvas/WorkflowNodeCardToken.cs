using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent colors of a <see cref="WorkflowNodeCard"/> (visual rules from Arc's free hero-section
/// block, HeroRelay, hero-relay.module.css <c>.node</c>): the two sizes, the frame, the row, the state marks, the faded
/// skipped card and the card's shadow. The running colors mix the accent in code with the shares here.
/// </summary>
[ControlDesignToken]
internal sealed class WorkflowNodeCardToken : AbstractControlDesignToken
{
    /// <summary>A one-row card: 300 × 60 (Arc's main steps).</summary>
    public double StandardWidth { get; set; }

    public double StandardHeight { get; set; }

    /// <summary>A two-row card: 200 × 84 (Arc's action steps).</summary>
    public double CompactWidth { get; set; }

    public double CompactHeight { get; set; }

    /// <summary>
    /// Cards on a narrow canvas (Arc HeroRelay <c>LAYOUTS.narrow</c>): 344 × 52 for one row, 108 × 72 for two; a two-row
    /// card's padding tightens to 10 and its row gap to 6 (Arc's container query under 560).
    /// </summary>
    public double NarrowStandardWidth { get; set; }

    public double NarrowStandardHeight { get; set; }

    public double NarrowCompactWidth { get; set; }

    public double NarrowCompactHeight { get; set; }

    public Thickness NarrowCompactInlinePadding { get; set; }

    public double NarrowCompactRowGap { get; set; }

    /// <summary>A selected card's halo: 3px of the subtle accent just outside the card, its corners 3 rounder.</summary>
    public Thickness SelectionHaloMargin { get; set; }

    public Thickness SelectionHaloThickness { get; set; }

    public CornerRadius SelectionHaloRadius { get; set; }

    /// <summary>The keyboard focus ring: 2px, 2px outside the card, its corners 4 rounder.</summary>
    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    /// <summary>The card's corners: 14.</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>Space inside the card's left and right edges: 14.</summary>
    public Thickness InlinePadding { get; set; }

    /// <summary>Space around content from a node template (<see cref="WorkflowNodeCard.ContentTemplate"/>): 14 by 12.</summary>
    public Thickness CustomContentPadding { get; set; }

    /// <summary>Space between icon, text and state: 12.</summary>
    public double ContentGap { get; set; }

    /// <summary>Space between the two rows of a compact card: 8.</summary>
    public double CompactRowGap { get; set; }

    /// <summary>The icon's box: 20; a Lucide geometry draws at 17 inside it with a 1.75 stroke.</summary>
    public double NodeIconSize { get; set; }

    /// <summary>The title's size: 13.</summary>
    public double TitleFontSize { get; set; }

    /// <summary>The check, minus and alert marks: 14, 2px stroke.</summary>
    public double StateGlyphSize { get; set; }

    /// <summary>Space between the time and the check: 6.</summary>
    public double StateGap { get; set; }

    /// <summary>The waiting ring: 10 across, 1.5 stroke, in the wire color.</summary>
    public double PendingRingSize { get; set; }

    public double PendingRingThickness { get; set; }

    public Color PendingRingColor { get; set; }

    /// <summary>The running ring: 13 across, 1.5 stroke; its track is the accent at 22%, a quarter in the accent turns every 0.7s.</summary>
    public double SpinnerSize { get; set; }

    public double SpinnerTrackAccentShare { get; set; }

    /// <summary>A skipped card fades to 0.5 and loses its shadow.</summary>
    public double SkippedOpacity { get; set; }

    /// <summary>A running card's border mixes 70% accent into the border, its fill 4% accent into the surface.</summary>
    public double RunningBorderAccentShare { get; set; }

    public double RunningFillAccentShare { get; set; }

    /// <summary>A failed card's border: the border with 50% of the danger color.</summary>
    public Color FailedBorderColor { get; set; }

    /// <summary>
    /// Arc's node shadow: light <c>0 1 1 rgb(20 24 40 / .04), 0 8 24 -12 rgb(20 24 40 / .14)</c>; dark a 1px top highlight
    /// (foreground 7%) over <c>0 12 32 -14 rgb(0 0 0 / .7)</c>.
    /// </summary>
    public BoxShadows CardShadow { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        StandardWidth = 300;
        StandardHeight = 60;
        CompactWidth = 200;
        CompactHeight = 84;
        NarrowStandardWidth = 344;
        NarrowStandardHeight = 52;
        NarrowCompactWidth = 108;
        NarrowCompactHeight = 72;
        NarrowCompactInlinePadding = new Thickness(10, 0);
        NarrowCompactRowGap = 6;
        SelectionHaloMargin = new Thickness(-3);
        SelectionHaloThickness = new Thickness(3);
        SelectionHaloRadius = new CornerRadius(17);
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(18);
        CardRadius = new CornerRadius(14);
        InlinePadding = new Thickness(14, 0);
        CustomContentPadding = new Thickness(14, 12);
        ContentGap = 12;
        CompactRowGap = 8;
        NodeIconSize = 20;
        TitleFontSize = 13;
        StateGlyphSize = 14;
        StateGap = 6;
        PendingRingSize = 10;
        PendingRingThickness = 1.5;
        PendingRingColor = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.20 : 0.17);
        SpinnerSize = 13;
        SpinnerTrackAccentShare = 0.22;
        SkippedOpacity = 0.5;
        RunningBorderAccentShare = 0.70;
        RunningFillAccentShare = 0.04;
        FailedBorderColor = Oklch.MixOklab(n.Border, n.Danger, 0.5);
        CardShadow = isDarkMode
            ? new BoxShadows(
                new BoxShadow { IsInset = true, OffsetY = 1, Color = Oklch.WithAlpha(n.Foreground, 0.07) },
                [new BoxShadow { OffsetY = 12, Blur = 32, Spread = -14, Color = Color.FromArgb(0xB3, 0, 0, 0) }])
            : new BoxShadows(
                new BoxShadow { OffsetY = 1, Blur = 1, Color = Color.FromArgb(0x0A, 20, 24, 40) },
                [new BoxShadow { OffsetY = 8, Blur = 24, Spread = -12, Color = Color.FromArgb(0x24, 20, 24, 40) }]);
    }
}
