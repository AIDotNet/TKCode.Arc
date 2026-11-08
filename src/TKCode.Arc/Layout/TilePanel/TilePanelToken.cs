namespace TKCode.Arc;

/// <summary>Default geometry of <see cref="TilePanel"/>.</summary>
[ControlDesignToken]
internal sealed class TilePanelToken : AbstractControlDesignToken
{
    /// <summary>Gap between tiles (<c>--space-2</c>).</summary>
    public double Spacing { get; set; }

    /// <summary>Smallest cell when the column count is automatic, and the cell size when nothing else sets it.</summary>
    public double MinCellSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Spacing = 8;
        MinCellSize = 96;
    }
}
