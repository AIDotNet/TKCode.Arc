using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of the mobile <see cref="Switch"/>: a 51 by 31 track, a 27px thumb and a 44px touch target.</summary>
[ControlDesignToken]
internal sealed class SwitchToken : AbstractControlDesignToken
{
    /// <summary>Track width: 51.</summary>
    public double TrackWidth { get; set; }

    /// <summary>Track height: 31.</summary>
    public double TrackHeight { get; set; }

    /// <summary>Space between track and thumb: 2.</summary>
    public Thickness TrackInset { get; set; }

    /// <summary>Thumb diameter: 27.</summary>
    public double ThumbSize { get; set; }

    /// <summary>Thumb margin when on: the track's inner width minus the thumb.</summary>
    public Thickness ThumbOnMargin { get; set; }

    /// <summary>Smallest width and height of the hit area: 44.</summary>
    public double TouchTarget { get; set; }

    /// <summary>Opacity when disabled: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TrackWidth = 51;
        TrackHeight = 31;
        TrackInset = new Thickness(2);
        ThumbSize = 27;
        ThumbOnMargin = new Thickness(20, 0, 0, 0);
        TouchTarget = 44;
        DisabledOpacity = 0.5;
    }
}
