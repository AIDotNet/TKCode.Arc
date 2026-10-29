
namespace TKCode.Arc;

/// <summary>DonutChart geometry (Arc: 24px ring, center lines keep a body line height).</summary>
[ControlDesignToken]
internal sealed class DonutChartToken : AbstractControlDesignToken
{
    /// <summary>Default ring thickness (the <c>RingThickness</c> property overrides it).</summary>
    public double RingThicknessDefault { get; set; }

    /// <summary>Height kept by the center label and meta lines so readout swaps never move the number.</summary>
    public double CenterLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RingThicknessDefault = 24;
        CenterLineHeight = 12 * 1.4;
    }
}
