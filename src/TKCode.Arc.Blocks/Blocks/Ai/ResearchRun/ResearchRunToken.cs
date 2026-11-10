using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="ResearchRun"/>. Colors, radii and fonts come from ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class ResearchRunToken : AbstractControlDesignToken
{
    /// <summary>The widest the run grows (860).</summary>
    public double MaxWidth { get; set; }

    /// <summary>Padding of the card's sections (20 on the sides).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>The narrow card's section padding (16 on the sides).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>At or below this width the plan and the sources stack (640).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Space between the sections of the process (20).</summary>
    public double SectionGap { get; set; }

    /// <summary>Padding of a source row (10, 8).</summary>
    public Thickness SourcePadding { get; set; }

    /// <summary>The height of a source's reading line (2).</summary>
    public double MeterHeight { get; set; }

    /// <summary>The diameter of a stage dot (10).</summary>
    public double StageDotSize { get; set; }

    /// <summary>The width of the connector before a stage (28).</summary>
    public double StageConnectorWidth { get; set; }

    /// <summary>The height of a citation pill (18).</summary>
    public double CitationHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxWidth = 860;
        SectionPadding = new Thickness(20, 16);
        CompactSectionPadding = new Thickness(16, 14);
        CompactWidth = 640;
        SectionGap = 20;
        SourcePadding = new Thickness(10, 8);
        MeterHeight = 2;
        StageDotSize = 10;
        StageConnectorWidth = 28;
        CitationHeight = 18;
    }
}
