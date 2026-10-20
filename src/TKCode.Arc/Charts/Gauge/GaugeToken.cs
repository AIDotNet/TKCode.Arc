using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class GaugeToken : AbstractControlDesignToken
{
    /// <summary>Largest ring width (Arc: <c>min(100%, 176px)</c>, height 0.9 × width).</summary>
    public double DialMaxWidth { get; set; }

    /// <summary>Ring stroke in the 100 × 90 view box, so it scales with the ring.</summary>
    public double RingThickness { get; set; }

    /// <summary>Space between the ring and the caption.</summary>
    public double CaptionGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DialMaxWidth = 176;
        RingThickness = 9;
        CaptionGap = 10;
    }
}
