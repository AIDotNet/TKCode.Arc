using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="AgentOrchestrator"/> and the accent-independent tints it derives from the neutrals.
/// Colors that follow the accent, radii and fonts come from ArcDesign tokens in the theme. Paddings are Avalonia thicknesses
/// (left, top, right, bottom).
/// </summary>
[ControlDesignToken]
internal sealed class AgentOrchestratorToken : AbstractControlDesignToken
{
    /// <summary>The card is at most this wide (960) and centered.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>At or below this width of the card the detail panel moves above the log and paddings tighten (640).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Header padding (20 16 16 12).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Header padding in a compact card (16 14 12 10).</summary>
    public Thickness CompactHeaderPadding { get; set; }

    /// <summary>Space around the graph (20 12 20 20).</summary>
    public Thickness GraphPadding { get; set; }

    /// <summary>Space around the graph in a compact card (16 8 16 16).</summary>
    public Thickness CompactGraphPadding { get; set; }

    /// <summary>Padding of the log and the detail panel (20 16).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>Padding of the log and the detail panel in a compact card (16 14).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>The widest a sub-agent node grows (208).</summary>
    public double NodeMaxWidth { get; set; }

    /// <summary>Below this width per node the sub-agents stack under the lead as a tree (156).</summary>
    public double NodeMinWidth { get; set; }

    /// <summary>The lead node's width in the fan layout (248).</summary>
    public double LeadWidth { get; set; }

    /// <summary>Height of a node (112).</summary>
    public double NodeHeight { get; set; }

    /// <summary>Height of a sub-agent node in the tree layout (76).</summary>
    public double TreeNodeHeight { get; set; }

    /// <summary>Space between sub-agent nodes (12).</summary>
    public double NodeGap { get; set; }

    /// <summary>Vertical space between the lead and the sub-agents in the fan layout (72).</summary>
    public double RankGap { get; set; }

    /// <summary>How far tree nodes sit right of the trunk line (28).</summary>
    public double TreeIndent { get; set; }

    /// <summary>Width of a resting edge (1).</summary>
    public double EdgeWidth { get; set; }

    /// <summary>Width of an edge to an agent at work (1.5).</summary>
    public double ActiveEdgeWidth { get; set; }

    /// <summary>Radius of a message dot (4).</summary>
    public double PacketRadius { get; set; }

    /// <summary>Radius of the soft halo around a message dot (9).</summary>
    public double PacketHaloRadius { get; set; }

    /// <summary>How long a message dot takes to travel its line (900ms).</summary>
    public TimeSpan PacketDuration { get; set; }

    /// <summary>Width of the detail panel beside the log (300).</summary>
    public double DetailWidth { get; set; }

    /// <summary>Height of the message log, which scrolls inside it; fixed so the card does not grow while a run plays (260).</summary>
    public double LogHeight { get; set; }

    /// <summary>The tint behind a hovered node: the muted surface at 60%.</summary>
    public Color NodeHoverBackground { get; set; }

    /// <summary>The tint of the graph area: the muted surface at 35%.</summary>
    public Color GraphBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardMaxWidth = 960;
        CompactWidth = 640;
        HeaderPadding = new Thickness(20, 16, 16, 12);
        CompactHeaderPadding = new Thickness(16, 14, 12, 10);
        GraphPadding = new Thickness(20, 12, 20, 20);
        CompactGraphPadding = new Thickness(16, 8, 16, 16);
        SectionPadding = new Thickness(20, 16);
        CompactSectionPadding = new Thickness(16, 14);
        NodeMaxWidth = 208;
        NodeMinWidth = 156;
        LeadWidth = 248;
        NodeHeight = 112;
        TreeNodeHeight = 76;
        NodeGap = 12;
        RankGap = 72;
        TreeIndent = 28;
        EdgeWidth = 1;
        ActiveEdgeWidth = 1.5;
        PacketRadius = 4;
        PacketHaloRadius = 9;
        PacketDuration = TimeSpan.FromMilliseconds(900);
        DetailWidth = 300;
        LogHeight = 260;
        NodeHoverBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.6);
        GraphBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.35);
    }
}
