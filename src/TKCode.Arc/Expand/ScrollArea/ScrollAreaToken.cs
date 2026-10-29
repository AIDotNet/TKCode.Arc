using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ScrollAreaToken : AbstractControlDesignToken
{
    /// <summary>Hit area of an overlay track (Arc: 11px around a 5px pill).</summary>
    public double TrackThickness { get; set; }

    /// <summary>Inset keyboard focus ring (the area clips its content, so the ring is drawn inside).</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TrackThickness = 11;
        FocusRingMargin = new Thickness(0);
    }
}
