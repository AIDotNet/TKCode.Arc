using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SegmentedControlToken : AbstractControlDesignToken
{
    public Thickness TrackInset { get; set; }
    public double SegmentGap { get; set; }
    public Thickness SegmentInset { get; set; }
    public CornerRadius SegmentRadius { get; set; }
    public double AccessoryGap { get; set; }

    /// <summary>The pill's 1px stroke is centered half a pixel inside the segment.</summary>
    public CornerRadius SelectionRadius { get; set; }

    /// <summary>Width of the edge fade on a side that has more to scroll.</summary>
    public double EdgeFade { get; set; }

    /// <summary>Room left beside the selected segment when it is scrolled into view.</summary>
    public double ScrollRoom { get; set; }
    public Thickness FocusRingMargin { get; set; }
    public CornerRadius FocusRingRadius { get; set; }
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TrackInset = new Thickness(3);
        SegmentGap = 2;
        SegmentInset = new Thickness(12, 0);
        SegmentRadius = new CornerRadius(14);
        AccessoryGap = 6;
        SelectionRadius = new CornerRadius(13.5);
        EdgeFade = 20;
        ScrollRoom = 20;
        FocusRingMargin = new Thickness(-2);
        FocusRingRadius = new CornerRadius(16);
        DisabledOpacity = 0.5;
    }
}
