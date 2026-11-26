namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="TouchSlider"/>: 44px touch height, 6px track, 28px thumb and disabled opacity.</summary>
[ControlDesignToken]
internal sealed class TouchSliderToken : AbstractControlDesignToken
{
    /// <summary>Minimum interactive height of the slider: 44.</summary>
    public double TouchTarget { get; set; }

    /// <summary>Height of the horizontal track bar: 6.</summary>
    public double TrackHeight { get; set; }

    /// <summary>Diameter of the circular thumb: 28.</summary>
    public double ThumbSize { get; set; }

    /// <summary>Opacity while disabled: 0.4.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        TouchTarget = 44;
        TrackHeight = 6;
        ThumbSize = 28;
        DisabledOpacity = 0.4;
    }
}
