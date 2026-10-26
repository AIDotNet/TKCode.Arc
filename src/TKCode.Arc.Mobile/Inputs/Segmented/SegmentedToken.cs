using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Segmented"/>: a track with 2px padding and 10px corners around the segments.</summary>
[ControlDesignToken]
internal sealed class SegmentedToken : AbstractControlDesignToken
{
    /// <summary>Padding between the track and the segments: 2.</summary>
    public Thickness TrackPadding { get; set; }

    /// <summary>Corner radius of the track: 10.</summary>
    public CornerRadius TrackRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TrackPadding = new Thickness(2);
        TrackRadius = new CornerRadius(10);
    }
}
