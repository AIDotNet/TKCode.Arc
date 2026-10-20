using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SlopeChartToken : AbstractControlDesignToken
{
    /// <summary>Plot height per item when no height is given (at least 200).</summary>
    public double RowPitch { get; set; }

    /// <summary>Smallest distance between two value labels in one column.</summary>
    public double LabelGap { get; set; }

    /// <summary>Below this width labels step down a size and the columns narrow.</summary>
    public double NarrowBreakpoint { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowPitch = 44;
        LabelGap = 20;
        NarrowBreakpoint = 440;
    }
}
