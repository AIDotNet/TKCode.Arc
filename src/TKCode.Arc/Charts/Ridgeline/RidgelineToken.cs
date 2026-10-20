using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class RidgelineToken : AbstractControlDesignToken
{
    /// <summary>How far a lifted ridge rises.</summary>
    public double Lift { get; set; }

    /// <summary>Height reserved for the value axis under the last row.</summary>
    public double AxisHeight { get; set; }

    /// <summary>Approximate pixels per axis tick.</summary>
    public double TickSpacing { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Lift = 6;
        AxisHeight = 28;
        TickSpacing = 72;
    }
}
