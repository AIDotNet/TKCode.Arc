namespace TKCode.Arc;

/// <summary>Default geometry of <see cref="WaterfallPanel"/>.</summary>
[ControlDesignToken]
internal sealed class WaterfallPanelToken : AbstractControlDesignToken
{
    /// <summary>Gap between lanes and between the children of a lane (<c>--space-3</c>).</summary>
    public double Spacing { get; set; }

    /// <summary>Narrowest lane when the lane count is 0 (automatic).</summary>
    public double MinLaneSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Spacing = 12;
        MinLaneSize = 200;
    }
}
