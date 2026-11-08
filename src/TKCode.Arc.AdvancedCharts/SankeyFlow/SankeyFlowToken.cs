namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SankeyFlowToken : AbstractControlDesignToken
{
    /// <summary>Thickness of a node bar along the flow.</summary>
    public double NodeThickness { get; set; }

    /// <summary>Space between neighbouring nodes of a column.</summary>
    public double NodeGap { get; set; }

    /// <summary>Corner radius of a node bar.</summary>
    public double NodeRadius { get; set; }

    /// <summary>Radius of one drifting particle.</summary>
    public double ParticleRadius { get; set; }

    /// <summary>Space between the stage names and the plot, and between the plot and the particle toggle.</summary>
    public double FigureGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        NodeThickness = 12;
        NodeGap = 14;
        NodeRadius = 3;
        ParticleRadius = 1.75;
        FigureGap = 10;
    }
}
