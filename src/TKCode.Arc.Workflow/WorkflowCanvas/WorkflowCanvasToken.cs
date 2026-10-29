using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent colors of the <see cref="WorkflowCanvas"/> (visual rules from Arc's free hero-section
/// block, HeroRelay, hero-relay.module.css): the dot grid, the wires and the drawn data, the spacing of an arranged
/// workflow and the timing of the data's draw. Accent mixes are computed in code from ArcDesign colors and the shares here.
/// </summary>
[ControlDesignToken]
internal sealed class WorkflowCanvasToken : AbstractControlDesignToken
{
    /// <summary>Distance between grid dots in canvas units: 22 (Arc <c>background-size: 22px 22px</c>).</summary>
    public double GridSpacing { get; set; }

    /// <summary>Dot radius in screen pixels: 1 (Arc <c>radial-gradient(… 1px, transparent 1.5px)</c>).</summary>
    public double GridDotRadius { get; set; }

    /// <summary>Dot color: the foreground at 13% (17% dark).</summary>
    public Color GridDotColor { get; set; }

    /// <summary>The grid hides when zooming out brings the dots closer than this on screen: 8.</summary>
    public double GridMinScreenSpacing { get; set; }

    /// <summary>A wire's stroke: 1.25 (Arc <c>.wire</c>).</summary>
    public double WireThickness { get; set; }

    /// <summary>Wire color: the foreground at 17% (20% dark).</summary>
    public Color WireColor { get; set; }

    /// <summary>The drawn data's stroke in the accent: 1.75, round caps (Arc <c>.flow</c>).</summary>
    public double FlowThickness { get; set; }

    /// <summary>Dash and gap of a skipped wire in pixels: 3 and 4 (Arc <c>stroke-dasharray: 3 4</c>).</summary>
    public double SkippedDashLength { get; set; }

    public double SkippedGapLength { get; set; }

    /// <summary>Opacity of a skipped wire: 0.7.</summary>
    public double SkippedWireOpacity { get; set; }

    /// <summary>
    /// Shortest curve handle: 40. Arc's handles are half the gap between the ends; short, flat or backward edges get
    /// longer ones so they still leave and enter vertically.
    /// </summary>
    public double BezierMinHandle { get; set; }

    /// <summary>The rounded corners of a step edge: 8.</summary>
    public double StepCornerRadius { get; set; }

    /// <summary>Space between the ranks of an arranged workflow: 52 (Arc's 60 high steps every 112).</summary>
    public double RankGap { get; set; }

    /// <summary>Space below a step that fans out to several steps: 72 (Arc's branch at 224 + 60, actions at 356).</summary>
    public double FanOutRankGap { get; set; }

    /// <summary>Space between neighbouring steps of a rank: 20 (Arc's 200 wide actions every 220).</summary>
    public double NodeGap { get; set; }

    /// <summary>
    /// The narrow layout (Arc HeroRelay <c>LAYOUTS.narrow</c>, a canvas under 520 wide): 26 between ranks, 36 below a
    /// fan-out, 10 between neighbours.
    /// </summary>
    public double NarrowRankGap { get; set; }

    public double NarrowFanOutRankGap { get; set; }

    public double NarrowNodeGap { get; set; }

    /// <summary>How much one zoom step (buttons, + and − keys) zooms: 1.25.</summary>
    public double ZoomStep { get; set; }

    /// <summary>How long the data takes to draw along an edge: 400ms (Arc's edge phases run 380–440ms), in-out curve.</summary>
    public TimeSpan FlowDuration { get; set; }

    /// <summary>The canvas background is the page background with this share of the accent: 0.02 (Arc <c>.hero</c>).</summary>
    public double CanvasAccentShare { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        GridSpacing = 22;
        GridDotRadius = 1;
        GridDotColor = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.17 : 0.13);
        GridMinScreenSpacing = 8;
        WireThickness = 1.25;
        WireColor = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.20 : 0.17);
        FlowThickness = 1.75;
        SkippedDashLength = 3;
        SkippedGapLength = 4;
        SkippedWireOpacity = 0.7;
        BezierMinHandle = 40;
        StepCornerRadius = 8;
        RankGap = 52;
        FanOutRankGap = 72;
        NodeGap = 20;
        NarrowRankGap = 26;
        NarrowFanOutRankGap = 36;
        NarrowNodeGap = 10;
        ZoomStep = 1.25;
        FlowDuration = TimeSpan.FromMilliseconds(400);
        CanvasAccentShare = 0.02;
    }
}
