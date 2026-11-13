using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="LeadScoring"/>. Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class LeadScoringToken : AbstractControlDesignToken
{
    /// <summary>At or below this width the detail panel moves under the table (860).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The block's padding (24) and its compact form (16).</summary>
    public Thickness Padding { get; set; }

    public Thickness CompactPadding { get; set; }

    /// <summary>The detail panel's width beside the table (320).</summary>
    public double DetailWidth { get; set; }

    /// <summary>The score ring's diameter (64) and stroke (6).</summary>
    public double RingDiameter { get; set; }

    public double RingThickness { get; set; }

    /// <summary>The score bar in a table cell: 56 wide, 4 high.</summary>
    public double CellBarWidth { get; set; }

    public double CellBarHeight { get; set; }

    /// <summary>The table's height before it scrolls (440).</summary>
    public double TableHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CompactWidth = 860;
        Padding = new Thickness(24);
        CompactPadding = new Thickness(16);
        DetailWidth = 320;
        RingDiameter = 64;
        RingThickness = 6;
        CellBarWidth = 56;
        CellBarHeight = 4;
        TableHeight = 440;
    }
}
